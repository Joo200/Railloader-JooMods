using System;
using System.Collections.Generic;
using System.Linq;
using Game.State;
using HarmonyLib;
using Serilog;
using SignalsEverywhere.Signals;
using Track;
using Track.Signals;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(CTCAutoSignal))]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCAutoSignal_CalculateAspect_Patch
{
    
    [HarmonyPostfix]
    [HarmonyPatch("OnEnable")]
    private static void OnEnable(CTCAutoSignal __instance)
    {
        if (!StateManager.IsHost)
            return;
        var crossover = __instance.GetComponentInParent<CTCCrossover>(true);
        if (crossover == null)
            return;

        var storage = __instance.GetComponentInParent<SignalStorage>(true);
        if (storage == null)
        {
            Log.Error($"Signal {__instance.id} has no SignalStorage");
            return;
        }
        
        Log.Information($"Registering signal {__instance.id} for crossover {crossover.id}");
        foreach (var switchSet in crossover.switchSets)
        {
            foreach (var switchNode in switchSet.switchNodes)
                __instance.UpdateSignalOnChange<SwitchSetting>(storage.ObserveSwitchPosition, switchNode.id);
        }

        foreach (var group in crossover.signalGroups.FindAll(s => s.signals.Contains(__instance)))
        {
            __instance.UpdateSignalOnChange<CTCTrafficFilter>(storage.ObserveCrossoverGroupDirection, group.groupId);
        }
        foreach (var block in crossover.routes.SelectMany(s => s.usedBlocks))
        {
            __instance.UpdateSignalOnChange<bool>(storage.ObserveBlockOccupancy, block.id);
            __instance.UpdateSignalOnChange<CTCTrafficFilter>(storage.ObserveBlockTrafficFilter, block.id);
        }

        foreach (var block in crossover.outlets.SelectMany(s => s.Blocks))
        {
            __instance.UpdateSignalOnChange<bool>(storage.ObserveBlockOccupancy, block.id);
            __instance.UpdateSignalOnChange<CTCTrafficFilter>(storage.ObserveBlockTrafficFilter, block.id);
        }

        foreach (var outlet in crossover.outlets.Select(s => s.nextSignal))
        {
            if (outlet != null)
                __instance.UpdateSignalOnChange<SignalAspect>(storage.ObserveSignalAspect, outlet.id);
        }
    }

    private static SignalAspect AspectDisplayedBySignal(CTCSignal signal)
    {
        return signal.GetComponentInParent<SignalStorage>().GetSignalAspect(signal.id);
    }
    
    private static SemaphoreHeadController.Aspect AspectForBlockAndNextSignal(
        IReadOnlyCollection<CTCBlock> nextBlocks,
        CTCSignal nextSignal,
        bool lined)
    {
        if (!lined || nextBlocks != null && nextBlocks.Any(b => b.IsOccupied))
            return SemaphoreHeadController.Aspect.Red;
        if (nextSignal == null || !nextSignal.isActiveAndEnabled)
            return SemaphoreHeadController.Aspect.Yellow;
        switch (AspectDisplayedBySignal(nextSignal))
        {
            case SignalAspect.Stop:
                return SemaphoreHeadController.Aspect.Yellow;
            case SignalAspect.Approach:
                return SemaphoreHeadController.Aspect.Green;
            case SignalAspect.Clear:
                return SemaphoreHeadController.Aspect.Green;
            case SignalAspect.DivergingApproach:
                return SemaphoreHeadController.Aspect.Yellow;
            case SignalAspect.DivergingClear:
                return SemaphoreHeadController.Aspect.Yellow;
            case SignalAspect.Restricting:
                return SemaphoreHeadController.Aspect.Yellow;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void CalculateCrossoverAspect(CTCAutoSignal __instance, ref SignalAspect __result, CTCCrossover crossover)
    {
        SemaphoreHeadController.Aspect head0 = SemaphoreHeadController.Aspect.Red;
        SemaphoreHeadController.Aspect head1 = SemaphoreHeadController.Aspect.Red;
        if (__instance.interlockingRouteMapping.Count == 0)
        {
            Log.Error($"Signal {__instance.id} has empty interlockingRouteMapping -- no aspects will be displayed");
        } 
        if (__instance.interlockingRouteMapping.Count >= 1)
        {
            var (blocks, signal, lined) = crossover.BlockAndNextSignal(__instance.id, __instance.interlockingRouteMapping[0], __instance.direction);
            head0 = AspectForBlockAndNextSignal(blocks, signal, lined);
        }
        if (__instance.interlockingRouteMapping.Count >= 2)
        {
            for (int i = 1; i < __instance.interlockingRouteMapping.Count; i++)
            {
                var (blocks, signal, lined) = crossover.BlockAndNextSignal(__instance.id, __instance.interlockingRouteMapping[i], __instance.direction);
                head1 = AspectForBlockAndNextSignal(blocks, signal, lined);
                if (head1 != SemaphoreHeadController.Aspect.Red) 
                    break;
            }
        }
        __result = SignalAspectForHeads(head0, head1);
    }
    
    private static SignalAspect SignalAspectForHeads(
        SemaphoreHeadController.Aspect head0,
        SemaphoreHeadController.Aspect head1)
    {
        if (head0 == SemaphoreHeadController.Aspect.Yellow)
            return SignalAspect.Approach;
        if (head0 == SemaphoreHeadController.Aspect.Green)
            return SignalAspect.Clear;
        if (head1 == SemaphoreHeadController.Aspect.Yellow)
            return SignalAspect.DivergingApproach;
        if (head1 == SemaphoreHeadController.Aspect.Green)
            return SignalAspect.DivergingClear;
        return SignalAspect.Stop;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch("_CalculateAspect")]
    private static void CalculateAspect(
        CTCAutoSignal __instance, 
        ref SignalAspect __result,
        [HarmonyArgument("stopReason")] ref int stopReason)
    {
        var crossover = __instance.GetComponentInParent<CTCCrossover>();
        if (crossover != null)
        {
            CalculateCrossoverAspect(__instance, ref __result, crossover);
            return;
        }
        
        if (__result != SignalAspect.Stop)
        {
            return;
        }

        if (stopReason != 0)
        {
            return;
        }
        if (__instance.Interlocking != null && __instance.interlockingRouteMapping.Count > 2)
        {
            for (int i = 2; i < __instance.interlockingRouteMapping.Count; i++)
            {
                var (blocks, signal, lined) = __instance.Interlocking.BlockAndNexSignal(__instance.interlockingRouteMapping[i], __instance.direction);
                if (!lined)
                {
                    continue;
                }

                if (blocks != null && blocks.Any<CTCBlock>(b => b.IsOccupied))
                {
                    continue;
                }

                Log.Debug($"Route overwrites with Diverging Approach for signal {__instance.id}");
                __result = SignalAspect.DivergingApproach;
                return;
            }
        }
    }
}