using System.Collections.Generic;
using StrangeCustoms.Tracks;
using Track.Signals;

namespace SignalsEverywhere.Signals;

public class CTCPatchingContext(Serilog.ILogger logger, IReadOnlyDictionary<string, string> changedEntries)
    : PatchingContext(logger, changedEntries)
{
    public Dictionary<string, CTCIntermediate> Intermediates { get; set; } = new();
    public Dictionary<string, CTCInterlocking> Interlockings { get; set; } = new();
    public Dictionary<string, CTCCrossover> Crossovers { get; set; } = new();
    
    public Dictionary<string, CTCPredicateSignal> PredicateSignals { get; set; } = new();
    public Dictionary<string, CTCAutoSignal> AutoSignals { get; set; } = new();
    public Dictionary<string, CTCBlock> Blocks { get; set; } = new();

    public CTCSignal? GetSignal(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (AutoSignals.TryGetValue(id, out var autoSignal))
            return autoSignal;
        if (PredicateSignals.TryGetValue(id, out var predicateSignal)) 
            return predicateSignal;
        logger.Warning($"Signal with ID '{id}' not found in patching context.");
        return null;
    }

    public List<CTCBlock> GetBlocks(List<string> ids) 
    {
        List<CTCBlock> blocks = new();
        if (ids == null) return blocks;
        foreach (var id in ids)
        {
            if (Blocks.TryGetValue(id, out var block))
                blocks.Add(block);
            else
                logger.Warning($"Block with ID '{id}' not found in patching context.");
        }
        return blocks;
    }
}
