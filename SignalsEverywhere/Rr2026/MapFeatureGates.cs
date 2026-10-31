using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Progression;
using Newtonsoft.Json;
using UnityEngine;

namespace SignalsEverywhere.Rr2026;

// Ties SignalsEverywhere modules to a map feature (their own signal milestone), so they stay off until it is bought.
// Signal mods declare it in a map-feature-gates.json in their own folder:
//   { "gates": [ { "mapFeature": "signals-alarka", "fallbackMapFeature": "alarka", "feature": "BK-AJ-HW",
//                  "modules": [ "CN-W", ... ] } ] }
//
// From Tetz's SignalsEverywhere Fixes (on Legacy it reads the gates; on Railroader 2026.1 SignalsEverywhere does):
// gates are read from the folders of the mods the game has loaded.
//
// fallbackMapFeature is used instead when the running progression has no milestone that unlocks mapFeature (a start
// the mod doesn't add the milestone to), so the signals still come on with the line.
//
// The modules stay where SignalsEverywhere puts them, under "<feature> Module", which the stock signal milestone
// switches on; so they need both the signal milestone and the map feature. Each module object is added to the map
// feature's gameObjectsEnableOnUnlock, which MapFeatureManager switches on and off as the feature unlocks, and is
// switched to match the feature now. SignalsEverywhere switches modules back on whenever it rebuilds them, so this is
// rechecked every two seconds.
internal static class MapFeatureGates
{
    private sealed class Gate
    {
        public string MapFeature;
        public string FallbackMapFeature;
        public string Feature;
        public List<string> Modules = new List<string>();
        [JsonIgnore] public string Source;
    }

    private sealed class Config
    {
        public List<Gate> Gates = new List<Gate>();
    }

    private static readonly List<Gate> Gates = new List<Gate>();
    private static readonly HashSet<string> Reported = new HashSet<string>();
    private static float nextCheck;

    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext(typeof(MapFeatureGates));

    internal static void Load(string modsDirectory, Func<string, bool> isLoaded)
    {
        Gates.Clear();
        foreach (string directory in Directory.GetDirectories(modsDirectory))
        {
            string path = Path.Combine(directory, "map-feature-gates.json");
            if (!File.Exists(path) || !Mixintos.IsLoadedMod(directory, isLoaded))
            {
                continue;
            }
            try
            {
                string source = Path.GetFileName(directory);
                foreach (Gate gate in JsonConvert.DeserializeObject<Config>(File.ReadAllText(path))?.Gates ?? new List<Gate>())
                {
                    if (string.IsNullOrEmpty(gate.MapFeature) || string.IsNullOrEmpty(gate.Feature) || gate.Modules.Count == 0)
                    {
                        Logger.Information($"Skipping a map feature gate in {path}: it needs a mapFeature, a feature and modules.");
                        continue;
                    }
                    gate.Source = source;
                    Gates.Add(gate);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Couldn't read {path}");
            }
        }
        foreach (IGrouping<string, Gate> source in Gates.GroupBy(g => g.Source))
        {
            Logger.Information($"{source.Key}: {source.Sum(g => g.Modules.Count)} signal module(s) gated by {string.Join(", ", source.Select(g => g.MapFeature).Distinct())}.");
        }
    }

    internal static void Update()
    {
        if (Gates.Count == 0 || Time.unscaledTime < nextCheck)
        {
            return;
        }
        nextCheck = Time.unscaledTime + 2f;
        try
        {
            Apply();
        }
        catch (Exception ex)
        {
            if (Reported.Add("exception"))
            {
                Logger.Error(ex, "Map feature gate check failed");
            }
        }
    }

    // The map feature that governs this gate in the running game: mapFeature when the progression has a milestone that
    // unlocks it (or there is no progression, e.g. sandbox), otherwise fallbackMapFeature.
    private static string Effective(Gate gate, Dictionary<string, MapFeature> features)
    {
        bool defined = features.ContainsKey(gate.MapFeature);
        Progression progression = Progression.Shared;
        bool sold = progression == null || progression.Milestones == null
            || progression.Milestones.Any(m => m != null
                && ((m.enableFeaturesOnStart?.Any(f => f != null && f.identifier == gate.MapFeature) ?? false)
                    || (m.enableFeaturesOnComplete?.Any(f => f != null && f.identifier == gate.MapFeature) ?? false)));
        string id = defined && sold ? gate.MapFeature : gate.FallbackMapFeature ?? (defined ? gate.MapFeature : null);
        if (id != null && Reported.Add("uses:" + gate.MapFeature + ":" + id))
        {
            Logger.Information(id == gate.MapFeature
                ? $"{gate.Feature} modules follow map feature '{id}'."
                : $"No progression section unlocks '{gate.MapFeature}' here; {gate.Feature} modules follow '{id}' instead.");
        }
        return id;
    }

    private static void Apply()
    {
        Dictionary<string, MapFeature> features = null;
        Dictionary<string, Transform> featureRoots = null;
        foreach (Gate gate in Gates)
        {
            features ??= UnityEngine.Object.FindObjectsByType<MapFeature>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(f => !string.IsNullOrEmpty(f.identifier))
                .GroupBy(f => f.identifier).ToDictionary(g => g.Key, g => g.First());
            string id = Effective(gate, features);
            if (id == null || !features.TryGetValue(id, out MapFeature mapFeature))
            {
                continue;   // no map loaded yet, or a route without this feature
            }
            // The feature not in use (the gate can switch once the progression has loaded) must not hold the modules.
            string otherId = id == gate.MapFeature ? gate.FallbackMapFeature : gate.MapFeature;
            MapFeature other = otherId != null && features.TryGetValue(otherId, out MapFeature m) ? m : null;
            featureRoots ??= UnityEngine.Object.FindObjectsByType<Track.Signals.CTCMapFeatureTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First().transform);
            if (!featureRoots.TryGetValue(gate.Feature + " Module", out Transform root))
            {
                continue;   // SignalsEverywhere hasn't built signals yet
            }
            foreach (string module in gate.Modules)
            {
                Transform t = root.Find(module);
                if (t == null)
                {
                    continue;
                }
                GameObject go = t.gameObject;
                GameObject[] list = mapFeature.gameObjectsEnableOnUnlock ?? Array.Empty<GameObject>();
                if (!list.Contains(go))
                {
                    mapFeature.gameObjectsEnableOnUnlock = list.Where(g => g != null).Append(go).ToArray();
                }
                if (other?.gameObjectsEnableOnUnlock != null && other.gameObjectsEnableOnUnlock.Contains(go))
                {
                    other.gameObjectsEnableOnUnlock = other.gameObjectsEnableOnUnlock.Where(g => g != go).ToArray();
                }
                if (go.activeSelf != mapFeature.Unlocked)
                {
                    go.SetActive(mapFeature.Unlocked);
                    string key = $"{module}:{mapFeature.Unlocked}";
                    if (Reported.Add(key))
                    {
                        Logger.Information($"{gate.Feature}/{module} {(mapFeature.Unlocked ? "on" : "off")}: map feature '{id}' is {(mapFeature.Unlocked ? "unlocked" : "locked")}.");
                    }
                }
            }
        }
    }
}

// Runs the gate check (every two seconds) for the whole session.
internal class MapFeatureGatesUpdater : MonoBehaviour
{
    private void Update() => MapFeatureGates.Update();
}
