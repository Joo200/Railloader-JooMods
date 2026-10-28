using HarmonyLib;
using SignalsEverywhere.Signals;
using Track.Signals;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(CTCBlock))]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCBlock_Awake_Patch
{
    // A block only belongs to an intermediate that lists it; see IntermediateRepair.
    [HarmonyPostfix]
    [HarmonyPatch("Awake")]
    private static void Awake(CTCBlock __instance)
    {
        IntermediateRepair.Detach(__instance);
    }
}
