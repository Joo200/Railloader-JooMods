using System.Linq;
using Game.Progression;
using Game.State;
using HarmonyLib;
using Serilog;

namespace CustomSpawnPoints.Patches;

[HarmonyPatch(typeof(StateManager))]
[HarmonyPatchCategory("CustomSpawnPoints")]
public class StateManager_Patch
{
    private static readonly AccessTools.FieldRef<StateManager, SetupDescriptor> _setupDescriptor = AccessTools.FieldRefAccess<StateManager, SetupDescriptor>(nameof(_setupDescriptor)); 
    
    [HarmonyPatch("ApplyGameSetup")]
    [HarmonyPrefix]
    public static void InjectCustomSpawnPoints(StateManager __instance, GameSetup? gameSetup)
    {
        foreach (var serialized  in CustomSpawnPointsMod.Shared.SpawnPoints)
        {
            serialized.Build();
        }
    }
}
