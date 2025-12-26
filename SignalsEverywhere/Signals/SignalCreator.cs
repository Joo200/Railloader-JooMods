using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Railloader;
using Track;
using Track.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Signals;

public class SignalCreator
{
    private static readonly Serilog.ILogger logger = Serilog.Log.ForContext<SignalCreator>();
    
    private SignalsEverywhere _instance;
    
    private Dictionary<string, Dictionary<string, SerializedCTCModule>> _markers = new();
    private IReadOnlyDictionary<string, string>? _touchers = new Dictionary<string, string>();
    
    public SignalCreator(SignalsEverywhere instance)
    {
        _instance = instance;
    }

    public void CreateSignals(IEnumerable<ModMixinto> mixintos, CTCPanelController instance)
    {
        var mergedJson = new JObject();
        foreach (var modMixinto in mixintos)
        {
            var json = JObject.Parse(File.ReadAllText(modMixinto.Mixinto));
            if (json == null)
            {
                logger.Warning($"Failed to parse mixinto {modMixinto.Mixinto} for signals");
                continue;
            }
            mergedJson.Merge(json, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union, MergeNullValueHandling = MergeNullValueHandling.Ignore });
        }

        mergedJson.Remove("$schema");
        _markers = mergedJson.ToObject<Dictionary<string, Dictionary<string, SerializedCTCModule>>>();
        if (_markers == null)
        {
            logger.Warning($"Failed to parse mixintos for signals");
            return;
        }
        PrepareFeatures(instance);
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

    private CTCPatchingContext CreateContext()
    {
        CTCPatchingContext ctx = new CTCPatchingContext(logger, _touchers);
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

    private void PrepareFeatures(CTCPanelController instance)
    {
        Transform? root = GetCtcRoot(instance);
        if (root == null)
        {
            logger.Error("Couldn't find CTC root");
            return;
        }
        // var jsonRoot = Deserialize(root);
        foreach (var section in _markers)
        {
            var existing = root.Find(section.Key + " Module");
            if (existing != null)
            {
                logger.Information($"Destroying existing feature target '{section.Key} Module'");
                existing.DestroyAllChildren();
                Object.Destroy(existing);
            }
            
            GameObject go = new GameObject(section.Key + " Module");
            go.transform.SetParent(root, false);

            logger.Information($"Creating feature target '{section.Key} Module'");
            go.AddComponent<CTCMapFeatureTarget>();
        }
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
            var name = featureTarget.gameObject.name;
            Dictionary<string, SerializedCTCModule> modules = new();
            for (int i = 0; i < featureTarget.transform.childCount; i++)
            {
                var dir = featureTarget.transform.GetChild(i);
                modules.Add(dir.name, new SerializedCTCModule(dir.gameObject));
            }
            features.Add(name, modules);
        }
        return JObject.FromObject(features);
    }

    public void BuildSignals(CTCPanelController instance)
    {
        Transform? root = GetCtcRoot(instance);
        if (root == null)
        {
            logger.Error("Couldn't find CTC root");
            return;
        }

        var ctx = CreateContext();

        List<GameObject> gos = new();

        foreach (var section in _markers)
        {
            var go = root.Find(section.Key + " Module");
            if (go == null)
            {
                logger.Error($"Couldn't find feature target '{section.Key} Module'");
                continue;
            }
            foreach (var module in section.Value)
            {
                gos.Add(CreateModule(go, ctx, module.Key, module.Value, "signals." + section.Key + "." + module.Key));
            }
        }

        foreach (var section in _markers)
        {
            foreach (var module in section.Value)
            {
                module.Value.Finalize(ctx);
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

    private GameObject CreateModule(Transform root, CTCPatchingContext ctx, string id, SerializedCTCModule module, string jsonPath)
    {
        module.Id = id;
        
        GameObject go = new GameObject(id + " Module");
        go.SetActive(false);
        go.transform.SetParent(root, false);

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
}
