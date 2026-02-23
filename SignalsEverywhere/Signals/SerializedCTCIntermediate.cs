using System.Collections.Generic;
using System.Linq;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class SerializedCTCIntermediate
{
    public string Id { get; set; }
    public List<string> Blocks { get; set; }
    public List<string> Signals { get; set; }
    public string? SignalLeft { get; set; }
    public string? SignalRight { get; set; }

    public SerializedCTCIntermediate() {}
    
    public SerializedCTCIntermediate(CTCIntermediate intermediate)
    {
        Id = intermediate.name;
        Blocks = intermediate.blocks.Select(b => b.id).ToList();
        Signals = intermediate.signals.Select(s => s.id).ToList();
        SignalLeft = intermediate.nextSignalLeft?.id;
        SignalRight = intermediate.nextSignalRight?.id;
    }

    public void CreateFor(GameObject parent, CTCPatchingContext ctx)
    {
        CTCIntermediate intermediate = parent.GetComponent<CTCIntermediate>() ?? parent.AddComponent<CTCIntermediate>();
        intermediate.name = Id;
        ctx.Intermediates[Id] = intermediate;
    }
    
    public void ApplyTo(CTCIntermediate intermediate, CTCPatchingContext ctx)
    {
        intermediate.blocks = ctx.GetBlocks(Blocks);
        intermediate.signals = Signals.Select(ctx.GetSignal).Where(e => e != null).ToList();
        intermediate.nextSignalLeft = ctx.GetSignal(SignalLeft);
        intermediate.nextSignalRight = ctx.GetSignal(SignalRight);
    }
}