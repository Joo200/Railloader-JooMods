#nullable disable
#pragma warning disable CS0649 // set by JSON

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Progression;
using HarmonyLib;
using Model.Ops;
using Newtonsoft.Json;
using UnityEngine;

namespace SignalsEverywhere.Rr2026;

// Lets signal mods add their own signal milestone on Railroader 2026.1, where milestones and map features are scene
// objects (no definition, map data or Relay script can add one). Signal mods declare them in a milestones.json in their
// own folder:
//   { "mapFeatures": [ { "identifier": "signals-alarka", "displayName": "Signals - Alarka Branch",
//                        "defaultEnableInSandbox": true } ],
//     "milestones": [ { "identifier": "tetz95-signals-alarka", "title": "Signals: Alarka Branch", "description": "...",
//                       "cost": 2500, "prerequisites": [ "signals-bk-hw", "alarka" ],
//                       "enableFeaturesOnComplete": [ "signals-alarka" ],
//                       "industryComponentId": "tetz95-cochran-signal-site.site-signals",
//                       "deliveries": [ { "loadId": "rr/signal-parts", "carTypeFilter": "XM*", "count": 3 } ] } ] }
//
// A map feature is added to the MapFeatureManager before it collects its features. A milestone is a delivery milestone,
// like the game's own signal milestones, added to the running progression before it is configured; its delivery site
// (a ProgressionIndustryComponent) comes from the mod's map data (Relay Data.rl modifyMap). A milestone whose
// prerequisites, map features or delivery site the progression doesn't have (another start or map) is left out.
// "progressions" (optional) limits a milestone to those progression ids. Pair the map feature with a
// map-feature-gates.json gate so the mod's signal modules follow it.
internal static class Milestones
{
    private sealed class FeatureDef
    {
        public string Identifier;
        public string DisplayName;
        public string Description;
        public bool DefaultEnableInSandbox = true;
    }

    private sealed class DeliveryDef
    {
        public string LoadId;
        public string CarTypeFilter;
        public int Count = 1;
    }

    private sealed class MilestoneDef
    {
        public string Identifier;
        public List<string> Progressions;
        public string Title;
        public string Description;
        public int Cost;
        public List<string> Prerequisites = new List<string>();
        public List<string> EnableFeaturesOnComplete = new List<string>();
        public string IndustryComponentId;
        public List<DeliveryDef> Deliveries = new List<DeliveryDef>();
        [JsonIgnore] public string Source;
    }

    private sealed class Config
    {
        public List<FeatureDef> MapFeatures = new List<FeatureDef>();
        public List<MilestoneDef> Milestones = new List<MilestoneDef>();
    }

    private const string ObjectPrefix = "SignalsEverywhere ";

    private static readonly List<FeatureDef> Features = new List<FeatureDef>();
    private static readonly List<MilestoneDef> MilestoneDefs = new List<MilestoneDef>();
    private static readonly HashSet<string> Reported = new HashSet<string>();

    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext(typeof(Milestones));

    internal static void Load(string modsDirectory, Func<string, bool> isLoaded)
    {
        Features.Clear();
        MilestoneDefs.Clear();
        foreach (string directory in Directory.GetDirectories(modsDirectory))
        {
            string path = Path.Combine(directory, "milestones.json");
            if (!File.Exists(path) || !Mixintos.IsLoadedMod(directory, isLoaded))
            {
                continue;
            }
            try
            {
                string source = Path.GetFileName(directory);
                Config config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path)) ?? new Config();
                Features.AddRange(config.MapFeatures.Where(f => !string.IsNullOrEmpty(f.Identifier)));
                foreach (MilestoneDef milestone in config.Milestones)
                {
                    if (string.IsNullOrEmpty(milestone.Identifier) || string.IsNullOrEmpty(milestone.IndustryComponentId) || milestone.Deliveries.Count == 0)
                    {
                        Logger.Information($"Skipping a milestone in {path}: it needs an identifier, an industryComponentId and deliveries.");
                        continue;
                    }
                    milestone.Source = source;
                    MilestoneDefs.Add(milestone);
                }
                Logger.Information($"{source}: {config.MapFeatures.Count} map feature(s), {config.Milestones.Count} milestone(s).");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Couldn't read {path}");
            }
        }
    }

    // Before MapFeatureManager.Awake collects the features under it.
    internal static void AddFeatures(MapFeatureManager manager)
    {
        foreach (FeatureDef def in Features)
        {
            string name = ObjectPrefix + def.Identifier;
            if (manager.transform.Find(name) != null
                || manager.GetComponentsInChildren<MapFeature>(true).Any(f => f.identifier == def.Identifier))
            {
                continue;
            }
            var go = new GameObject(name);
            go.transform.SetParent(manager.transform, false);
            MapFeature feature = go.AddComponent<MapFeature>();
            feature.identifier = def.Identifier;
            feature.displayName = def.DisplayName;
            feature.description = def.Description;
            feature.defaultEnableInSandbox = def.DefaultEnableInSandbox;
            feature.prerequisites = Array.Empty<MapFeature>();
            feature.trackGroupsEnableOnUnlock = Array.Empty<string>();
            feature.trackGroupsAvailableOnUnlock = Array.Empty<string>();
            feature.gameObjectsEnableOnUnlock = Array.Empty<GameObject>();
            Logger.Information($"Added map feature '{def.Identifier}'.");
        }
    }

    // Before Progression.Configure collects the milestones under it.
    internal static void AddMilestones(Progression progression)
    {
        Milestone[] existing = progression.GetComponentsInChildren<Milestone>(true);
        Dictionary<string, MapFeature> features = MapFeatureManager.Shared == null
            ? new Dictionary<string, MapFeature>()
            : MapFeatureManager.Shared.AvailableFeatures.Where(f => f != null && !string.IsNullOrEmpty(f.identifier))
                .GroupBy(f => f.identifier).ToDictionary(g => g.Key, g => g.First());
        var milestones = existing.Where(m => !string.IsNullOrEmpty(m.Identifier)).GroupBy(m => m.Identifier).ToDictionary(g => g.Key, g => g.First());
        foreach (MilestoneDef def in MilestoneDefs)
        {
            if (milestones.ContainsKey(def.Identifier)
                || (def.Progressions != null && def.Progressions.Count > 0 && !def.Progressions.Contains(progression.identifier)))
            {
                continue;
            }
            string missing = string.Join(", ", def.Prerequisites.Where(p => !milestones.ContainsKey(p)).Select(p => $"milestone '{p}'")
                .Concat(def.EnableFeaturesOnComplete.Where(f => !features.ContainsKey(f)).Select(f => $"map feature '{f}'")));
            if (OpsController.Shared?.AllIndustryComponents.OfType<ProgressionIndustryComponent>().Any(c => c.Identifier == def.IndustryComponentId) != true)
            {
                missing += (missing.Length > 0 ? ", " : "") + $"delivery site '{def.IndustryComponentId}'";
            }
            if (missing.Length > 0)
            {
                if (Reported.Add(progression.identifier + ":" + def.Identifier))
                {
                    Logger.Information($"{def.Source}: milestone '{def.Identifier}' left out of progression '{progression.identifier}': it has no {missing}.");
                }
                continue;
            }

            var go = new GameObject(ObjectPrefix + def.Identifier);
            go.transform.SetParent(progression.transform, false);
            DeliveryMilestone milestone = go.AddComponent<DeliveryMilestone>();
            var traverse = Traverse.Create(milestone);
            traverse.Field("identifier").SetValue(def.Identifier);
            traverse.Field("title").SetValue(def.Title ?? def.Identifier);
            traverse.Field("description").SetValue(def.Description ?? "");
            traverse.Field("cost").SetValue(def.Cost);
            traverse.Field("prerequisites").SetValue(def.Prerequisites.Select(p => milestones[p]).ToList());
            traverse.Field("industryComponentId").SetValue(def.IndustryComponentId);
            traverse.Field("deliveries").SetValue(def.Deliveries.Select(d => new DeliveryMilestone.Delivery
            {
                loadId = d.LoadId,
                carTypeFilter = new CarTypeFilter(d.CarTypeFilter ?? ""),
                count = d.Count,
                direction = DeliveryMilestone.Delivery.Direction.LoadToIndustry
            }).ToArray());
            milestone.enableFeaturesOnStart = new List<MapFeature>();
            milestone.enableFeaturesOnComplete = def.EnableFeaturesOnComplete.Select(f => features[f]).ToList();
            milestone.disableFeaturesOnComplete = new List<MapFeature>();
            milestones[def.Identifier] = milestone;
            Logger.Information($"{def.Source}: added milestone '{def.Identifier}' to progression '{progression.identifier}'.");
        }
    }
}

[HarmonyPatch(typeof(MapFeatureManager), "Awake")]
[HarmonyPatchCategory("SignalsEverywhere")]
internal static class MapFeatureManagerAwakePatch
{
    private static void Prefix(MapFeatureManager __instance)
    {
        try
        {
            Milestones.AddFeatures(__instance);
        }
        catch (Exception ex)
        {
            Serilog.Log.ForContext(typeof(Milestones)).Error(ex, "Adding map features failed");
        }
    }
}

[HarmonyPatch(typeof(Progression), nameof(Progression.Configure))]
[HarmonyPatchCategory("SignalsEverywhere")]
internal static class ProgressionConfigurePatch
{
    private static void Prefix(Progression __instance)
    {
        try
        {
            Milestones.AddMilestones(__instance);
        }
        catch (Exception ex)
        {
            Serilog.Log.ForContext(typeof(Milestones)).Error(ex, "Adding milestones failed");
        }
    }
}
