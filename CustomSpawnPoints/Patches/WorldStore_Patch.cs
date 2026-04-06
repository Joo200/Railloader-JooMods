using Game.Messages;
using Game.Persistence;
using HarmonyLib;

namespace CustomSpawnPoints.Patches;

[HarmonyPatch(typeof(WorldStore))]
[HarmonyPatchCategory("CustomSpawnPoints")]
public class WorldStore_Patch
{
    private static StringPropertyValue ?progressionBefore;
    
    [HarmonyPrefix]
    [HarmonyPatch("Migrate", typeof(Snapshot))]
    public static void FixMigrationOfProgressionBefore(Snapshot snapshot)
    {
        progressionBefore = null;
        if (!snapshot.Properties.TryGetValue("_game", out var dict1))
            return;
        var runtime = dict1.ToRuntime();
        if (runtime["mode"].IntValue == 1 && snapshot.Properties.TryGetValue("_progression", out var progression))
        {
            if (progression.TryGetValue("progression", out var val) && val is StringPropertyValue strVal)
            {
                progressionBefore = strVal;
            }
        }
    }
    
    [HarmonyPostfix]
    [HarmonyPatch("Migrate", typeof(Snapshot))]
    public static void FixMigrationOfProgressionAfter(Snapshot snapshot)
    {
        if (progressionBefore == null || !snapshot.Properties.TryGetValue("_game", out var dict1))
            return;
        var runtime = dict1.ToRuntime();
        if (runtime["mode"].IntValue == 1 && snapshot.Properties.TryGetValue("_progression", out var progression))
        {
            progression["progression"] = progressionBefore;
        }
        progressionBefore = null;
    }
    
}