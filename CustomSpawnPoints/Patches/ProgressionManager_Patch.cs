using System.Collections.Generic;
using System.Linq;
using CustomSpawnPoints.Config;
using Game.Progression;
using HarmonyLib;
using Serilog;
using UnityEngine;

namespace CustomSpawnPoints.Patches;

[HarmonyPatch(typeof(MapFeatureManager))]
[HarmonyPatchCategory("CustomSpawnPoints")]
public class ProgressionManager_Patch
{
    private static readonly AccessTools.FieldRef<Progression, MapFeature[]> enableFeaturesAtStart = AccessTools.FieldRefAccess<Progression, MapFeature[]>("enableFeaturesAtStart"); 
    
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    public static void InjectProgression(MapFeatureManager __instance)
    {
        foreach (var desc in CustomSpawnPointsMod.Shared.SpawnPoints)
        {
            var existing = Object.FindObjectsOfType<Progression>(true).FirstOrDefault(i => i.identifier == desc.progressionId);
            if (existing != null)
            {
                Log.Information($"Updating {desc.progressionId} because it already exists");
                existing.mapFeatureManager = __instance;
                UpdateMapFeaturesAtStart(existing, desc);
                continue;
            }
            Log.Information($"Injecting {desc.progressionId}");
            var progressionsObj = GameObject.Find("Progressions");
            var go = new GameObject(desc.identifier);
            go.transform.SetParent(progressionsObj.transform, false);
            var clone = go.AddComponent<Progression>();
            clone.name = desc.name;
            clone.identifier = desc.progressionId;
            clone.mapFeatureManager = __instance;
            UpdateMapFeaturesAtStart(clone, desc);
        } 
    }

    private static void UpdateMapFeaturesAtStart(Progression progression, SerializedSetupDescriptor desc)
    {
        List<MapFeature> features = new();
        foreach (var feature in desc.enabledFeatures)
        {
            var feat = progression.mapFeatureManager.AvailableFeatures.FirstOrDefault(e => e.identifier == feature);
            if (feat != null)
            {
                Log.Information($"Found matching feature for this progression {feat.name}");
                features.Add(feat);
            }
        }
        enableFeaturesAtStart(progression) = features.ToArray();
    }
}
