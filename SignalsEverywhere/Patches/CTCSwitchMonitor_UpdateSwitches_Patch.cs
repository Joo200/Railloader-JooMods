using System.Collections.Generic;
using System.Linq;
using Game.State;
using HarmonyLib;
using SignalsEverywhere.Signals;
using Track;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(CTCSwitchMonitor))]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCSwitchMonitor_UpdateSwitches_Patch
{
    [HarmonyPrefix]
    [HarmonyPatch("UpdateSwitchesForCTC")]
    private static bool UpdateSwitchesForCTC(CTCSwitchMonitor __instance, SystemMode mode)
    {
        if (Graph.Shared == null)
        {
            return false;
        }
        HashSet<TrackNode> hashSet = Graph.Shared.Nodes.Where(n => Graph.Shared.IsSwitch(n)).ToHashSet();
        HashSet<TrackNode> trackNodeSet = new HashSet<TrackNode>();
        if (mode == SystemMode.CTC)
        {
            foreach (var node in Object.FindObjectsOfType<CTCInterlocking>().Where(i => i.isActiveAndEnabled)
                         .SelectMany(s => s.switchSets).SelectMany(s => s.switchNodes))
            {
                if (hashSet.Contains(node))
                {
                    hashSet.Remove(node);
                    trackNodeSet.Add(node);
                }
            }

            foreach (var node in Object.FindObjectsOfType<CTCCrossover>().Where(i => i.isActiveAndEnabled)
                         .SelectMany(s => s.switchSets).SelectMany(s => s.switchNodes))
            {
                if (hashSet.Contains(node))
                {
                    hashSet.Remove(node);
                    trackNodeSet.Add(node);
                }
            }
        }
        
        foreach (var trackNode in hashSet)
        {
            if (trackNode.IsCTCSwitch)
            {
                trackNode.IsCTCSwitch = false;
                Graph.Shared.OnNodeDidChange(trackNode);
            }
        }

        foreach (var trackNode in trackNodeSet)
        {
            if (!trackNode.IsCTCSwitch)
            {
                trackNode.IsCTCSwitch = true;
                Graph.Shared.OnNodeDidChange(trackNode);
            }
        }

        return false;
    }

    [HarmonyPostfix]
    [HarmonyPatch("ObserveSwitches")]
    public static void PostfixObserveSwitches(CTCSwitchMonitor __instance)
    {
        if (!StateManager.IsHost)
            return;

        foreach (var crossover in Object.FindObjectsOfType<CTCCrossover>().Where(i => i.isActiveAndEnabled))
        {
            foreach (var node in crossover.switchSets.SelectMany(s => s.switchNodes))
            {
                node.OnDidChangeThrown = () =>
                {
                    CTCPanelController.Shared.GetComponentInParent<SignalStorage>().SetSwitchPosition(node.id,
                        node.isThrown ? SwitchSetting.Reversed : SwitchSetting.Normal);
                    foreach (var block in crossover.Blocks)
                    {
                        block.MarkSwitchNodeUnlocked(node.id, node.IsCTCSwitchUnlocked);
                    }
                };
                foreach (CTCBlock ctcBlock in crossover.Blocks)
                    ctcBlock.MarkSwitchNodeUnlocked(node.id, node.IsCTCSwitchUnlocked);
            }
        }
    }
}