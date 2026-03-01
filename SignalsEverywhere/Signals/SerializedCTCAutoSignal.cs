using System.Collections.Generic;
using System.Linq;
using StrangeCustoms.Tracks;
using Track;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class SerializedCTCAutoSignal : SerializedCTCSignal
{
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

        GameObject prefab = SignalPrefabStore.Shared.GetSignalType(ModelType, HeadConfiguration);
        CTCAutoSignal signal = Object.Instantiate(prefab, parent.transform, false).AddComponent<CTCAutoSignal>();
        
        if (signal == null)
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