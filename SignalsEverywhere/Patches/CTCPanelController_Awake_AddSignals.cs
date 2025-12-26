using HarmonyLib;
using Serilog;
using Track.Signals;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(CTCPanelController), "Awake")]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCPanelController_Awake_AddSignals
{
    internal static void Prefix(CTCPanelController __instance)
    {
        Log.ForContext(typeof(SignalsEverywhere)).Information("Adding signals");
        SignalsEverywhere.Shared.BuildSignals(__instance);
    }
    
}