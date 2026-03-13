using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.Progression;
using HarmonyLib;
using Serilog;

namespace CustomSpawnPoints.Patches;

[HarmonyPatch(typeof(Progression))]
[HarmonyPatchCategory("CustomSpawnPoints")]
public class Progression_Patch
{
    [HarmonyPatch("UpdateSectionStates")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> UpdateSectionStates_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var setFeatureEnablesMethod = AccessTools.Method(typeof(MapFeatureManager), nameof(MapFeatureManager.SetFeatureEnables));
        var interceptorMethod = AccessTools.Method(typeof(Progression_Patch), nameof(SetFeatureEnables_Interceptor));

        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo mi && mi == setFeatureEnablesMethod)
            {
                codes[i].opcode = OpCodes.Call;
                codes[i].operand = interceptorMethod;
                break;
            }
        }

        return codes;
    }

    public static void SetFeatureEnables_Interceptor(MapFeatureManager instance, Dictionary<string, bool> featureEnables)
    {
        Log.Debug("Intercepting MapFeatureManager.SetFeatureEnables");
        featureEnables = new();
        foreach (var section in Progression.Shared.Sections)
        {
            foreach (MapFeature mapFeature in section.enableFeaturesOnAvailable)
                if (!featureEnables.ContainsKey(mapFeature.identifier) || section.Available)
                    featureEnables[mapFeature.identifier] = section.Available;
            foreach (MapFeature mapFeature in section.enableFeaturesOnUnlock)
                if (!featureEnables.ContainsKey(mapFeature.identifier) || section.Unlocked)
                    featureEnables[mapFeature.identifier] = section.Unlocked;
            foreach (var mapFeature in section.disableFeaturesOnUnlock)
                if (!featureEnables.ContainsKey(mapFeature.identifier) || section.Unlocked)
                    featureEnables[mapFeature.identifier] = !section.Unlocked;
        }

        instance.SetFeatureEnables(featureEnables);
    }
}