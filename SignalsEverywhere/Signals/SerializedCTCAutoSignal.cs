using System.Collections.Generic;
using System.Linq;
using StrangeCustoms.Tracks;
using Track;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class SerializedCTCAutoSignal : SerializedCTCSignal
{
    private static CTCAutoSignal? _singlePrefab => Object.FindObjectsOfType<CTCAutoSignal>(true)
        .FirstOrDefault(s => s.headConfiguration == SignalHeadConfiguration.Single);

    private static CTCAutoSignal? _doublePrefab => Object.FindObjectsOfType<CTCAutoSignal>(true)
        .FirstOrDefault(s => s.headConfiguration == SignalHeadConfiguration.Double);


    public List<string> Blocks { get; set; } = new();

    public List<int> InterlockingRouteMapping { get; set; } = new();

    public SerializedCTCAutoSignal() {}
    
    public SerializedCTCAutoSignal(CTCAutoSignal signal) : base(signal)
    {
        
        Blocks = signal.blocks.Select(b => b.id).ToList();
        InterlockingRouteMapping = signal.interlockingRouteMapping;
    }
    
    public void CreateFor(GameObject parent, CTCPatchingContext ctx)
    {
        if (HeadConfiguration == SignalHeadConfiguration.Triple)
        {
            ctx.Logger.Warning("AutoSignal does not support triple signal head configuration. Changing to double.");
            HeadConfiguration = SignalHeadConfiguration.Double;
        }
        
        CTCAutoSignal? signal = Object.Instantiate(HeadConfiguration == SignalHeadConfiguration.Double ? _doublePrefab : _singlePrefab , parent.transform, false);
        if (!signal)
        {
            throw new SCPatchingException("Unable to instantiate new signal");
        }
        base.ApplyTo(signal, ctx);
        ctx.AutoSignals[Id] = signal;
    }
    
    public void ApplyTo(CTCAutoSignal signal, CTCPatchingContext ctx)
    {
        signal.id = Id;
        signal.blocks = ctx.GetBlocks(Blocks);
        signal.interlockingRouteMapping = InterlockingRouteMapping;
    }
}