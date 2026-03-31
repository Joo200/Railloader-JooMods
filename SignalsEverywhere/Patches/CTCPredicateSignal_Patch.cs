using System;
using System.Collections.Generic;
using Game.State;
using HarmonyLib;
using SignalsEverywhere.Signals;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(CTCPredicateSignal))]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCPredicateSignal_Patch
{
    [HarmonyPatch("OnEnable")]
    [HarmonyPostfix]
    public static void OnEnable_Postfix(CTCPredicateSignal __instance)
    {
        if (!StateManager.IsHost)
            return;

        var extension = __instance.GetComponent<CTCPredicateSignalCrossoverExtension>();
        if (extension == null)
            return;

        var storage = __instance.GetComponentInParent<SignalStorage>();
        if (storage == null)
            return;

        foreach (var head in extension.heads)
        {
            foreach (var predicate in head.predicates)
            {
                if (predicate.crossover != null)
                {
                    __instance.UpdatePredicateSignalOnChange<CTCTrafficFilter>(storage.ObserveCrossoverGroupDirection, predicate.crossoverGroupId);
                }
            }
        }
    }

    [HarmonyPatch("CalculateAspect")]
    [HarmonyPostfix]
    public static void CalculateAspect_Postfix(CTCPredicateSignal __instance, ref SignalAspect __result, ref int stopReason)
    {
        var extension = __instance.GetComponent<CTCPredicateSignalCrossoverExtension>();
        if (extension == null || extension.heads.Count == 0)
            return;

        // If it's already not stop, we might need to downgrade it if crossover predicates are not satisfied.
        // But the original CalculateAspect already calculated based on its predicates.
        // We need to re-evaluate each head.
        
        SignalAspect head0 = SignalAspect.Stop;
        SignalAspect head1 = SignalAspect.Stop;
        SignalAspect head2 = SignalAspect.Stop;

        for (int i = 0; i < __instance.heads.Count; i++)
        {
            var head = __instance.heads[i];
            bool originalSatisfied = IsSatisfied(__instance, head);
            bool extensionSatisfied = extension.IsSatisfied(i);

            SignalAspect aspect = SignalAspect.Stop;
            if (originalSatisfied && extensionSatisfied)
            {
                aspect = (UnityEngine.Object) head.nextSignal == (UnityEngine.Object) null || !head.nextSignal.isActiveAndEnabled 
                    ? SignalAspect.Approach 
                    : (AspectDisplayedBySignal(__instance, head.nextSignal) != SignalAspect.Stop ? SignalAspect.Clear : SignalAspect.Approach);
            }

            if (i == 0) head0 = aspect;
            else if (i == 1) head1 = aspect;
            else if (i == 2) head2 = aspect;
        }

        __result = SignalAspectForHeads(head0, head1, head2);
        if (__result == SignalAspect.Stop)
        {
            stopReason = 0; // CTCSignal.StopReason.None
        }
    }

    private static SignalAspect SignalAspectForHeads(
        SignalAspect head0,
        SignalAspect head1,
        SignalAspect head2)
    {
        if (head0 == SignalAspect.Approach)
            return SignalAspect.Approach;
        if (head0 == SignalAspect.Clear)
            return SignalAspect.Clear;
        if (head1 == SignalAspect.Approach)
            return SignalAspect.DivergingApproach;
        if (head1 == SignalAspect.Clear)
            return SignalAspect.DivergingClear;
        if (head2 == SignalAspect.Approach || head2 == SignalAspect.Clear)
            return SignalAspect.Restricting;
        return SignalAspect.Stop;
    }

    private static bool IsSatisfied(CTCPredicateSignal instance, CTCPredicateSignal.HeadPredicates head)
    {
        return Traverse.Create(instance).Method("IsSatisfied", new[] { typeof(CTCPredicateSignal.HeadPredicates) }).GetValue<bool>(head);
    }

    private static SignalAspect AspectDisplayedBySignal(CTCPredicateSignal instance, CTCSignal signal)
    {
        return Traverse.Create(instance).Method("AspectDisplayedBySignal", new[] { typeof(CTCSignal) }).GetValue<SignalAspect>(signal);
    }
}
