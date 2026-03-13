using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SignalsEverywhere.Patching;
using Track;
using Track.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Signals;

public class SignalCreator
{
    private static readonly Serilog.ILogger logger = Serilog.Log.ForContext<SignalCreator>();

    public JsonSerializer Serializer;
    
    public JObject? OriginalData { get; private set; }
    public JObject? PatchedData { get; private set; }
    
    private SignalsEverywhere _instance;
    
    public SignalCreator(SignalsEverywhere instance)
    {
        _instance = instance;
        JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
        DefaultContractResolver contractResolver = new DefaultContractResolver();
        CamelCaseNamingStrategy caseNamingStrategy = new CamelCaseNamingStrategy();
        caseNamingStrategy.ProcessDictionaryKeys = false;
        contractResolver.NamingStrategy = caseNamingStrategy;
        serializerSettings.ContractResolver = contractResolver;
        serializerSettings.Converters = new List<JsonConverter>(2)
        {
            new Vector3Converter(),
            new StringEnumConverter()
        };
        Serializer = JsonSerializer.CreateDefault(serializerSettings);
    }

    public void CreateSignals(PatchHelper mergedJson, CTCPanelController instance)
    {
        List<string> keys = mergedJson.Value.Properties().Select(p => p.Name).ToList();
        Transform? root = GetCtcRoot(instance);
        if (root == null)
        {
            logger.Error("Couldn't find CTC root");
            return;
        }
        // var jsonRoot = Deserialize(root);
        foreach (var section in keys)
        {
            var old = root.Find(section + " Module");
            if (old != null)
            {
                if (old.gameObject.GetComponent<CTCMapFeatureTarget>() == null)
                    old.gameObject.AddComponent<CTCMapFeatureTarget>();
                logger.Debug($"Found existing feature target '{section} Module'");
            }
            else
            {
                GameObject go = new GameObject(section + " Module");
                go.transform.SetParent(root, false);
                go.AddComponent<CTCMapFeatureTarget>();
                logger.Debug($"Creating feature target '{section} Module'");
            }
        }
    }

    private Transform? GetCtcRoot(CTCPanelController instance)
    {
        var storage = instance.GetComponentInParent<SignalStorage>(true);
        if (storage == null)
        {
            logger.Error("Couldn't find SignalStorage");
            return null;
        }
        return storage.transform;
    }

    private CTCPatchingContext CreateContext(IReadOnlyDictionary<string, string>? touchers = null)
    {
        CTCPatchingContext ctx = new CTCPatchingContext(logger, touchers);
        foreach (CTCBlock block in Object.FindObjectsByType<CTCBlock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ctx.Blocks[block.id] = block;
        foreach (CTCPredicateSignal signal in Object.FindObjectsByType<CTCPredicateSignal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ctx.PredicateSignals[signal.id] = signal;
        foreach (CTCAutoSignal signal in Object.FindObjectsByType<CTCAutoSignal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ctx.AutoSignals[signal.id] = signal;
        foreach (CTCIntermediate intermediate in Object.FindObjectsByType<CTCIntermediate>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ctx.Intermediates[intermediate.name] = intermediate;
        foreach (CTCInterlocking interlocking in Object.FindObjectsByType<CTCInterlocking>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ctx.Interlockings[interlocking.id] = interlocking;

        ctx.NodesById = new();
        foreach (TrackNode node in Object.FindObjectsByType<TrackNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ctx.NodesById[node.id] = node;
        ctx.SegmentsById = new();
        foreach (TrackSegment segment in Object.FindObjectsByType<TrackSegment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ctx.SegmentsById[segment.id] = segment;
        ctx.SpansById = new();
        foreach (TrackSpan span in Object.FindObjectsByType<TrackSpan>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ctx.SpansById[span.id] = span;

        return ctx;
    }

    public JObject? Deserialize()
    {
        Transform? root = GetCtcRoot(CTCPanelController.Shared);
        if (root == null)
        {
            logger.Error("Couldn't find CTC root");
            return null;
        }
        return Deserialize(root);
    }
    
    public JObject Deserialize(Transform root)
    {
        Dictionary<string, Dictionary<string, SerializedCTCModule>> features = new();
        foreach (var featureTarget in root.GetComponentsInChildren<CTCMapFeatureTarget>(true))
        {
            var name = featureTarget.gameObject.name.Replace(" Module", "");
            logger.Debug($"Found feature target: {name}");
            Dictionary<string, SerializedCTCModule> modules = new();
            for (int i = 0; i < featureTarget.transform.childCount; i++)
            {
                logger.Debug($"Deserializing module {featureTarget.transform.GetChild(i).name}");
                var dir = featureTarget.transform.GetChild(i);
                modules.Add(dir.name, new SerializedCTCModule(dir.gameObject));
            }
            features.Add(name, modules);
        }
        logger.Information($"Deserialized {features.Count} features with {features.Sum(f => f.Value.Count)} modules");
        return JObject.FromObject(features, Serializer);
    }

    public void BuildSignals(CTCPanelController instance)
    {
        Transform? root = GetCtcRoot(instance);
        if (root == null)
        {
            logger.Error("Couldn't find CTC root");
            return;
        }
        
        if (OriginalData == null)
            OriginalData = Deserialize(root);
        else
            logger.Information("Reusing original signals data");
        var patched = SignalsEverywhere.Shared.GetMixintoJson("signals", OriginalData);

        PatchedData = patched.Value;
        var ctx = CreateContext(patched.TouchedByPath);
        
        List<GameObject> gos = new();

        var result = patched.Value.ToObject<Dictionary<string, Dictionary<string, SerializedCTCModule>>>();
        if (result == null)
            throw new Exception("Couldn't deserialize signals");
        
        // TODO: This is some ugly and wonky implementation. But it's needed.
        // Triple signals in ALJ have their second head removed so we force br-we here.
        var triplePrefab = Object.FindObjectsOfType<CTCPredicateSignal>(true)
            .FirstOrDefault(s => s.id == "br-we");
        if (triplePrefab != null)
            SignalPrefabStore.Shared.StorePrefab(triplePrefab, SignalPrefabStore.DefaultType, SignalHeadConfiguration.Triple);
        else
            logger.Warning("Couldn't find triple prefab for triple signal");
        
        foreach (var section in result)
        {
            var go = root.Find(section.Key + " Module");
            if (go == null)
            {
                logger.Error($"Couldn't find feature target '{section.Key} Module'");
                continue;
            }
            foreach (var module in section.Value)
            {
                if (ctx.ElementModified(section.Key + "." + module.Key))
                {
                    var resultGo = CreateModule(go, ctx, module.Key, module.Value, section.Key + "." + module.Key);
                    if (resultGo != null)
                        gos.Add(resultGo);
                }
            }
        }

        foreach (var section in result)
        {
            foreach (var module in section.Value)
            {
                if (module.Value != null)
                {
                    module.Value.Finalize(ctx, section.Key + "." + module.Key);
                }
            }
        }
        
        foreach (var go in gos)
        {
            go.SetActive(true);
        }
        
        Graph.Shared.RebuildCollections();
        AccessTools.Field(typeof(CTCPanelController), "_cachedBlocks").SetValue(instance, null);
        AccessTools.Field(typeof(CTCPanelController), "_cachedInterlockings").SetValue(instance, null);
        
        logger.Information($"Finished creating signals: Created {gos.Count} modules, {ctx.Blocks.Count} blocks, " +
                           $"{ctx.PredicateSignals.Count} predicate signals, {ctx.AutoSignals.Count} auto signals, " +
                           $"{ctx.Interlockings.Count} interlockings, {ctx.Crossovers.Count} crossovers, " +
                           $"{ctx.Intermediates.Count} intermediates");
    }

    private GameObject? CreateModule(Transform root, CTCPatchingContext ctx, string id, SerializedCTCModule? module, string jsonPath)
    {
        GameObject go;
        if (root.Find(id) != null)
        {
            logger.Debug($"Reusing module {id}");
            go = root.Find(id).gameObject;
            go.SetActive(false);
            if (go.GetComponent<CTCMapFeatureTarget>() == null)
                go.AddComponent<CTCMapFeatureTarget>();
        }
        else
        {
            logger.Debug($"Creating module {id}");
            go = new GameObject(id);
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.AddComponent<CTCMapFeatureTarget>();
        }

        if (module == null)
        {
            Object.DestroyImmediate(go);
            return null;
        }
        
        module.Id = id;
        
        try
        {
            module.Initialize(go, ctx, jsonPath);
        }
        catch (Exception ex)
        {
            logger.Error($"Unable to create module {id}: {ex.Message}: {ex}");
        }

        return go;
    }

    public string DumpData()
    {
        if (OriginalData == null)
            return "No signals loaded. Check the logs for errors.";
        var path = Path.Combine(SignalsEverywhere.Shared.ModDirectory, "signal-old.json");
        using (StreamWriter streamWriter = new StreamWriter(path))
        using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
            Serializer.Serialize(jsonWriter, OriginalData);

        if (PatchedData == null)
            return "Dumped original data. Patched data contains errors.";
            
        path = Path.Combine(SignalsEverywhere.Shared.ModDirectory, "signal-patched.json");
        using (StreamWriter streamWriter = new StreamWriter(path))
        using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
            Serializer.Serialize(jsonWriter, PatchedData);
            
        return "Successfully dumped signals to mod directory";
    }
}
