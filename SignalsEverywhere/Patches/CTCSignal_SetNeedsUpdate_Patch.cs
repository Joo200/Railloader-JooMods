using HarmonyLib;
using SignalsEverywhere.Signals;
using Track.Signals;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(CTCSignal))]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCSignal_SetNeedsUpdate_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(CTCSignal.IsIntermediate), MethodType.Getter)]
    private static bool IsIntermediate(CTCSignal __instance, ref bool __result)
    {
        __result = __instance.Interlocking == null && __instance.GetComponentInParent<CTCCrossover>() == null;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(CTCSignal.DisplayName), MethodType.Getter)]
    private static bool DisplayName(CTCSignal __instance, ref string __result)
    {
        if (__instance.Interlocking != null)
            __result = __instance.Interlocking.displayName;
        else if (__instance.GetComponentInParent<CTCCrossover>() != null)
            __result = __instance.GetComponentInParent<CTCCrossover>().displayName;
        else
            __result = "Intermediate";
        if (__instance.transform.rotation.x != 0) 
            __result += " (burp)";
        return false;
    }
}
