using System.Collections.Generic;
using HarmonyLib;
using Track;
using Track.Signals;

namespace SignalsEverywhere.Patches;

/// <summary>
/// A control point whose switch leads onto track that isn't bought yet (a line behind a milestone, such as a branch
/// leaving a CTC main) throws when that switch is coded: the game decodes the switch's legs, the leg on the locked
/// track comes back null, and its check for cars on the switch (TrainController.CarOnSwitch) dereferences it, so the
/// panel's Code button fails with a NullReferenceException. Refuse such a code the way the game refuses an impossible
/// one (no route). Only a switch that has to move is checked, as the game does, so routes through the control point
/// that leave the switch as it lies still code.
/// </summary>
[HarmonyPatch(typeof(CTCInterlocking))]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCInterlocking_CodeSwitchChanges_Patch
{
    private static readonly Serilog.ILogger logger = Serilog.Log.ForContext(typeof(CTCInterlocking_CodeSwitchChanges_Patch));

    [HarmonyPatch(nameof(CTCInterlocking.CodeSwitchChanges))]
    [HarmonyPrefix]
    private static bool CodeSwitchChangesPrefix(CTCInterlocking __instance, List<(TrackNode, SwitchSetting)> switchSettings,
        ref CTCInterlocking.CodeFailureReason reason, ref bool __result)
    {
        var graph = TrainController.Shared?.graph;
        if (graph == null || switchSettings == null)
            return true;
        foreach (var (node, setting) in switchSettings)
        {
            if (node == null || node.isThrown == (setting == SwitchSetting.Reversed))
                continue;
            if (graph.DecodeSwitchAt(node, out _, out var a, out var b) && a != null && b != null)
                continue;
            logger.Information($"Interlocking {__instance.id}: switch {node.id} leads onto track that isn't available; refusing the code");
            reason = CTCInterlocking.CodeFailureReason.NoRoute;
            __result = false;
            return false;
        }
        return true;
    }
}
