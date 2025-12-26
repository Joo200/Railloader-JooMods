using HarmonyLib;
using RollingStock.ContinuousControls;
using Track.Signals;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(RadialAnimatedControl), "OnEnable")]
[HarmonyPatchCategory("SignalsEverywhere")]
public class RadialAnimatedControl_ShiftClick_Patch
{
    [HarmonyPostfix]
    private static void OnEnable(RadialAnimatedControl __instance)
    {
        if (__instance.GetComponentInParent<CTCPanelKnob>() != null || __instance.GetComponent<CTCPanelKnob>() != null)
        {
            __instance.shiftActivateToggles = true;
        }
    }
}
