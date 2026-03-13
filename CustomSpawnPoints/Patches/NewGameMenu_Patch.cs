using System.Collections.Generic;
using System.Linq;
using Game.State;
using HarmonyLib;
using Serilog;
using UI.Builder;
using UI.Menu;
using UnityEngine;

namespace CustomSpawnPoints.Patches;

[HarmonyPatch(typeof(NewGameMenu))]
[HarmonyPatchCategory("CustomSpawnPoints")]
public class NewGameMenu_Patch
{
    public static string DefaultStart = "East Whittier Start";
    public static string DefaultProgression = NewGameMenu.ProgressionIdEWH;
    public static string DefaultSetupId = "ewh-steam";
    
    private static readonly AccessTools.FieldRef<NewGameMenu, string> _progressionId = AccessTools.FieldRefAccess<NewGameMenu, string>(nameof(_progressionId)); 
    private static readonly AccessTools.FieldRef<NewGameMenu, string> _setupId = AccessTools.FieldRefAccess<NewGameMenu, string>(nameof(_setupId)); 
    private static readonly AccessTools.FieldRef<NewGameMenu, GameMode> _gameMode = AccessTools.FieldRefAccess<NewGameMenu, GameMode>(nameof(_gameMode));
    
    public readonly struct Entry(string name, string setupId, string progressionId)
    {
        public readonly string name = name;
        public readonly string setupId = setupId;
        public readonly string progressionId = progressionId;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch("BuildMapSelect")]
    public static bool ModifyBuildMapSelect(NewGameMenu __instance, out RectTransform __result, UIPanelBuilder builder)
    {
        Log.Information($"Injecting new starts");
        
        List<Entry> entries = new();
        entries.Add(new Entry(DefaultStart, DefaultSetupId, DefaultProgression));
        foreach (var addedDescription in CustomSpawnPointsMod.Shared.SpawnPoints)
        {
            entries.Add(new Entry(addedDescription.name, addedDescription.identifier, addedDescription.progressionId));
        }

        if (_gameMode(__instance) == GameMode.Company)
        {
            _setupId(__instance) = DefaultSetupId;
            _progressionId(__instance) = DefaultProgression;
        }
        
        __result = builder.AddDropdown(entries.Select(e => e.name).ToList(), 0, e =>
        {
            var selected = entries[e];
            _setupId(__instance) = selected.setupId;
            _progressionId(__instance) = selected.progressionId;
        });
        return false;
    }
    
}