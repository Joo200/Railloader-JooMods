using System.Collections.Generic;
using HarmonyLib;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

/// <summary>
/// Some built-in intermediates (TV-WH) sit on the CTC feature object instead of a module, so they can't be patched,
/// and they are a parent of every block in their feature. Two things go wrong when a patch adds an interlocking next
/// to one, or takes a block out of an intermediate:
/// - the intermediate still names the old interlocking's signal as its next signal, although that interlocking no
///   longer has the intermediate's end block as an outlet, so aspect checks on its signals throw;
/// - a block takes the intermediate above it as its own, and a block with an intermediate never carries a traffic
///   direction, so the new interlocking's signals clear without a route and nothing stops opposing moves.
/// After building, relink such intermediates (also when a patch replaced the signal they pointed at) and detach blocks
/// the intermediate doesn't list. The base game has neither case, so built-in signals are unaffected.
/// </summary>
public static class IntermediateRepair
{
    private static readonly Serilog.ILogger logger = Serilog.Log.ForContext(typeof(IntermediateRepair));

    public static void Apply()
    {
        var interlockings = Object.FindObjectsOfType<CTCInterlocking>(true);
        var signals = Object.FindObjectsOfType<CTCSignal>(true);
        foreach (var intermediate in Object.FindObjectsOfType<CTCIntermediate>(true))
        {
            Relink(intermediate, CTCDirection.Left, interlockings, signals);
            Relink(intermediate, CTCDirection.Right, interlockings, signals);
        }

        // Blocks in modules that were reused woke up before their intermediate was patched.
        foreach (var block in Object.FindObjectsOfType<CTCBlock>(true))
            Detach(block);
    }

    public static void Detach(CTCBlock block)
    {
        var intermediate = block.Intermediate;
        if (intermediate == null || intermediate.blocks == null || intermediate.blocks.Contains(block))
            return;

        AccessTools.PropertySetter(typeof(CTCBlock), nameof(CTCBlock.Intermediate)).Invoke(block, new object?[] { null });
        logger.Information($"Block {block.id} isn't one of intermediate {intermediate.name}'s blocks; it now carries its own traffic direction");
    }

    private static void Relink(CTCIntermediate intermediate, CTCDirection direction, CTCInterlocking[] interlockings, CTCSignal[] signals)
    {
        var next = direction == CTCDirection.Left ? intermediate.nextSignalLeft : intermediate.nextSignalRight;
        var end = intermediate.BlockAtEnd(direction);
        if (end == null)
            return;
        // A patch that replaces the next signal (for example an auto signal redefined as a predicate signal with the
        // same id) destroys the object the intermediate points at. Unity reports a destroyed object as null, but the
        // reference itself isn't; a next signal that was never set is left alone.
        var destroyed = !ReferenceEquals(next, null) && next == null;
        // Signals may not be active yet, so look up their interlocking the way they will.
        var current = next == null ? null : next.GetComponentInParent<CTCInterlocking>(true);
        if (!destroyed && (current == null || HasOutlet(current, end)))
            return;

        var found = new List<CTCSignal>();
        foreach (var interlocking in interlockings)
        {
            if (interlocking == current || !HasOutlet(interlocking, end))
                continue;
            foreach (var signal in signals)
            {
                if (signal.direction == direction && signal.GetComponentInParent<CTCInterlocking>(true) == interlocking)
                    found.Add(signal);
            }
        }

        // A junction can have more than one signal facing this way (one on the main, one on a branch leg). Keep the ones
        // whose routes come in by the outlet holding the end block.
        if (found.Count > 1)
        {
            var byRoute = found.FindAll(signal => EntersBy(signal, end, direction));
            if (byRoute.Count == 1)
                found = byRoute;
        }

        if (found.Count != 1)
        {
            logger.Warning($"Intermediate {intermediate.name}: {direction} next signal {Describe(next, current)} doesn't lead on from " +
                           $"end block {end.id}; found {found.Count} replacement signals, leaving it");
            return;
        }

        if (direction == CTCDirection.Left)
            intermediate.nextSignalLeft = found[0];
        else
            intermediate.nextSignalRight = found[0];
        logger.Information($"Intermediate {intermediate.name}: {direction} next signal {Describe(next, current)} -> {found[0].id}, " +
                           $"whose interlocking has end block {end.id} as an outlet");
    }

    private static string Describe(CTCSignal? next, CTCInterlocking? current) =>
        next == null ? "(removed)" : $"{next.id} ({current?.id})";

    /// <summary>
    /// Whether one of the signal's routes is entered from an outlet holding the block by a train moving this way (a
    /// leftbound train enters a route by its outletRight).
    /// </summary>
    private static bool EntersBy(CTCSignal signal, CTCBlock block, CTCDirection direction)
    {
        if (signal is not CTCAutoSignal auto || auto.interlockingRouteMapping == null)
            return false;
        var interlocking = signal.GetComponentInParent<CTCInterlocking>(true);
        if (interlocking == null || interlocking.routes == null)
            return false;
        foreach (var index in auto.interlockingRouteMapping)
        {
            if (index < 0 || index >= interlocking.routes.Count)
                continue;
            var route = interlocking.routes[index];
            var entering = direction == CTCDirection.Left ? route.outletRight : route.outletLeft;
            if (entering >= 0 && entering < interlocking.outlets.Count && interlocking.outlets[entering].blocks != null &&
                interlocking.outlets[entering].blocks.Contains(block))
                return true;
        }
        return false;
    }

    private static bool HasOutlet(CTCInterlocking interlocking, CTCBlock block)
    {
        foreach (var outlet in interlocking.outlets)
        {
            if (outlet.blocks != null && outlet.blocks.Contains(block))
                return true;
        }
        return false;
    }
}
