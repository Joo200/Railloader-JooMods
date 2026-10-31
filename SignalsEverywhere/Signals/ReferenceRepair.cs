using System.Collections.Generic;
using System.Linq;
using Track.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Signals;

/// <summary>
/// When a module is rebuilt, its signals and blocks are destroyed and created again with the same ids. Objects in
/// modules that weren't rebuilt keep pointing at the destroyed ones: a neighbouring interlocking's or crossover's
/// outlets (next signal and blocks), predicate signals' heads and conditions, intermediates and auto signals. Unity
/// reports a destroyed object as null, so an interlocking treats such a next signal as missing and its signal never
/// shows better than Approach. After a build, point every such reference at the live object with the same id.
/// </summary>
public static class ReferenceRepair
{
    private static readonly Serilog.ILogger logger = Serilog.Log.ForContext(typeof(ReferenceRepair));

    private static Dictionary<string, CTCSignal> _signals = new();
    private static Dictionary<string, CTCBlock> _blocks = new();
    private static Dictionary<string, CTCInterlocking> _interlockings = new();
    private static int _fixed;

    public static void Apply()
    {
        _signals = new Dictionary<string, CTCSignal>();
        foreach (var signal in Object.FindObjectsOfType<CTCSignal>(true))
        {
            // An interlocking's signal wins over an intermediate one with the same id.
            if (signal != null && !string.IsNullOrEmpty(signal.id)
                && (!_signals.TryGetValue(signal.id, out var have)
                    || (have.GetComponentInParent<CTCInterlocking>(true) == null && signal.GetComponentInParent<CTCInterlocking>(true) != null)))
                _signals[signal.id] = signal;
        }
        _blocks = Object.FindObjectsOfType<CTCBlock>(true)
            .Where(b => b != null && !string.IsNullOrEmpty(b.id))
            .GroupBy(b => b.id).ToDictionary(g => g.Key, g => g.First());
        _interlockings = Object.FindObjectsOfType<CTCInterlocking>(true)
            .Where(i => i != null && !string.IsNullOrEmpty(i.id))
            .GroupBy(i => i.id).ToDictionary(g => g.Key, g => g.First());
        _fixed = 0;

        foreach (var interlocking in _interlockings.Values)
        {
            for (int i = 0; interlocking.outlets != null && i < interlocking.outlets.Count; i++)
            {
                var outlet = interlocking.outlets[i];
                outlet.nextSignal = Live(outlet.nextSignal, $"interlocking {interlocking.id} outlet {i}");
                Fix(outlet.blocks, $"interlocking {interlocking.id} outlet {i}");
                interlocking.outlets[i] = outlet;
            }
        }
        foreach (var crossover in Object.FindObjectsOfType<CTCCrossover>(true))
        {
            for (int i = 0; crossover.outlets != null && i < crossover.outlets.Count; i++)
            {
                var outlet = crossover.outlets[i];
                outlet.nextSignal = Live(outlet.nextSignal, $"crossover {crossover.id} outlet {i}");
                Fix(outlet.blocks, $"crossover {crossover.id} outlet {i}");
                crossover.outlets[i] = outlet;
            }
            foreach (var route in crossover.routes ?? new List<CTCCrossover.Route>())
                Fix(route.usedBlocks, $"crossover {crossover.id} route");
        }
        foreach (var intermediate in Object.FindObjectsOfType<CTCIntermediate>(true))
        {
            intermediate.nextSignalLeft = Live(intermediate.nextSignalLeft, $"intermediate {intermediate.name}");
            intermediate.nextSignalRight = Live(intermediate.nextSignalRight, $"intermediate {intermediate.name}");
            Fix(intermediate.signals, $"intermediate {intermediate.name}");
            Fix(intermediate.blocks, $"intermediate {intermediate.name}");
        }
        foreach (var signal in Object.FindObjectsOfType<CTCPredicateSignal>(true))
        {
            for (int h = 0; signal.heads != null && h < signal.heads.Count; h++)
            {
                var head = signal.heads[h];
                head.nextSignal = Live(head.nextSignal, $"signal {signal.id} head {h}");
                foreach (var predicate in head.predicates ?? new List<CTCPredicateSignal.Predicate>())
                {
                    predicate.interlocking = Live(predicate.interlocking, $"signal {signal.id} head {h}");
                    Fix(predicate.blocks, $"signal {signal.id} head {h}");
                }
            }
        }
        foreach (var signal in Object.FindObjectsOfType<CTCAutoSignal>(true))
            Fix(signal.blocks, $"signal {signal.id}");

        if (_fixed > 0)
            logger.Information($"Relinked {_fixed} references to rebuilt signals, blocks and interlockings");
        _signals = new Dictionary<string, CTCSignal>();
        _blocks = new Dictionary<string, CTCBlock>();
        _interlockings = new Dictionary<string, CTCInterlocking>();
    }

    // A destroyed object compares equal to null, but the reference isn't null and its id can still be read.
    private static bool Destroyed(Object? o) => !ReferenceEquals(o, null) && o == null;

    private static T Live<T>(T o, string where) where T : Object
    {
        if (!Destroyed(o))
            return o;
        string? id = o switch
        {
            CTCSignal s => s.id,
            CTCBlock b => b.id,
            CTCInterlocking i => i.id,
            _ => null
        };
        Object? live = null;
        if (id != null)
        {
            if (o is CTCSignal && _signals.TryGetValue(id, out var signal)) live = signal;
            else if (o is CTCBlock && _blocks.TryGetValue(id, out var block)) live = block;
            else if (o is CTCInterlocking && _interlockings.TryGetValue(id, out var interlocking)) live = interlocking;
        }
        if (live == null)
        {
            logger.Warning($"{where}: '{id}' was destroyed and has no replacement");
            return o;
        }
        _fixed++;
        logger.Debug($"{where}: relinked '{id}' to the rebuilt one");
        return (T)live;
    }

    private static void Fix<T>(List<T>? list, string where) where T : Object
    {
        for (int i = 0; list != null && i < list.Count; i++)
            list[i] = Live(list[i], where);
    }
}
