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
    
    [HarmonyPatch("GetSetupDescriptor")]
    [HarmonyPrefix]
    public static void InjectCustomSpawnPoints(StateManager __instance)
    {
        var matched = CustomSpawnPointsMod.Shared.SpawnPoints.FirstOrDefault(i => i.identifier == __instance.Storage.SetupId);
        if (matched == null)
        {
            Log.Information($"No match found for setup {__instance.Storage.SetupId}");
            return;
        }

        Log.Information($"Matched {matched.name} to {__instance.Storage.SetupId}");
        var desc = matched.Build();
        _setupDescriptor(__instance) = desc;
    }
}
