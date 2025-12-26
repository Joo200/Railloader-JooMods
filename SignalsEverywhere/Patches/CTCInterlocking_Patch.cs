using System.Collections.Generic;
using System.Linq;
using Core.Diagnostics;
using HarmonyLib;
using SignalsEverywhere.Signals;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(CTCInterlocking))]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCInterlocking_Patch
{
    [HarmonyPatch("IsNextInterlockingTrafficAgainst")]
    private bool IsNextInterlockingTrafficAgainstPrefix(CTCInterlocking __instance, ref bool __result, IEnumerable<CTCBlock> blocks, CTCDirection direction,
        CTCTrafficFilter trafficFilter, IDiagnosticCollector diagnostics)
    {
        foreach (CTCBlock block in blocks)
        {
            CTCIntermediate im = block.Intermediate;
            if (im == null) continue;
            var sig = im.NextExternalSignalForDirection(direction);
            if (sig == null) continue;
            var co = sig.GetComponentInParent<CTCCrossover>();
            if (co == null) continue;

            if (co.IsTrafficAgainst(block, trafficFilter))
            {
                __result = true;
                return false;
            }
        }

        return true;
    }
}