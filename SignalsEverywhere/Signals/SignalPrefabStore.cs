using System.Collections.Generic;
using System.Linq;
using Track.Signals;
using UnityEngine;
using Serilog;

namespace SignalsEverywhere.Signals;

public class SignalPrefabStore
{
    public static readonly string DefaultType = "Vanilla";
    
    private readonly Serilog.ILogger _logger = Log.ForContext<SignalPrefabStore>();
    private readonly Dictionary<(string type, SignalHeadConfiguration config), GameObject> _prefabs = new();

    public static SignalPrefabStore Shared { get; } = new();

    private SignalPrefabStore() { }

    public GameObject GetSignalType(string? type, SignalHeadConfiguration config)
    {
        string effectiveType = type ?? DefaultType;
        var key = (effectiveType, config);

        if (_prefabs.TryGetValue(key, out var prefab) && prefab != null)
        {
            return prefab;
        }

        prefab = CreatePrefabFromWorld(effectiveType, config);
        if (prefab != null)
        {
            _prefabs[key] = prefab;
            return prefab;
        }

        // Fallback to any prefab with the right config if specific type not found
        _logger.Warning("Could not find signal prefab of type {Type} with config {Config}. Falling back to default for config.", effectiveType, config);
        var fallbackKey = (DefaultType, config);
        if (_prefabs.TryGetValue(fallbackKey, out var fallbackPrefab) && fallbackPrefab != null)
        {
            return fallbackPrefab;
        }

        fallbackPrefab = CreatePrefabFromWorld(null, config);
        if (fallbackPrefab != null)
        {
            _prefabs[fallbackKey] = fallbackPrefab;
            return fallbackPrefab;
        }

        throw new System.Exception($"Could not find any signal prefab for configuration {config}");
    }

    private GameObject CreatePrefabFromWorld(string? type, SignalHeadConfiguration config)
    {
        _logger.Information("Searching world for signal prefab: Type={Type}, Config={Config}", type ?? "Any", config);

        var signals = Object.FindObjectsOfType<CTCSignal>(true);
        CTCSignal? target = null;

        if (type != null && type != DefaultType)
        {
            target = signals.FirstOrDefault(s => s.headConfiguration == config && s.name == type);
            if (target == null)
            {
                // Try matching without " (Clone)" if it's there
                target = signals.FirstOrDefault(s =>
                    s.headConfiguration == config && s.name.Replace("(Clone)", "").Trim() == type);
            }
        }

        if (target == null)
        {
            target = signals.FirstOrDefault(s => s.headConfiguration == config);
        }

        if (target == null)
        {
            return null;
        }

        _logger.Information("Found source signal: {Name}", target.name);
        return PreparePrefab(target, type, config);
    }

    public void StorePrefab(CTCSignal prefab, string type, SignalHeadConfiguration config)
    {
        var prepared = PreparePrefab(prefab.GetComponent<CTCSignal>(), type, config);
        var key = (type, config);
        _prefabs[key] = prepared;
    }

    public GameObject PreparePrefab(CTCSignal target, string? type, SignalHeadConfiguration config) {
        target.gameObject.SetActive(false);
        GameObject copy = Object.Instantiate(target.gameObject);

        copy.name = (type ?? DefaultType) + "_" + config;
        copy.SetActive(false);
        Object.DontDestroyOnLoad(copy);

        StripComponents(copy);

        return copy;
    }

    private void StripComponents(GameObject root)
    {
        // Strip CTCAutoSignal or CTCPredicateSignal (both are CTCSignals)
        var ctcSignal = root.GetComponent<CTCSignal>();
        if (ctcSignal != null)
        {
            _logger.Debug("Stripping {ComponentType} from prefab", ctcSignal.GetType().Name);
            Object.DestroyImmediate(ctcSignal);
        }

        // Strip MapEnhancer.MapEnhancer+SignalIconColorizer
        var components = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var comp in components)
        {
            if (comp == null) continue;
            if (comp.GetType().FullName == "MapEnhancer.MapEnhancer+SignalIconColorizer")
            {
                _logger.Debug("Stripping {ComponentType} from prefab", comp.GetType().FullName);
                Object.DestroyImmediate(comp);
            }
        }
    }
}
