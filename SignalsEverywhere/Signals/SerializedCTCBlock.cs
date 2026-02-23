using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StrangeCustoms.Tracks;
using Track;
using Track.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Signals;

public class SerializedCTCBlock
{
    public string Id { get; set; } = "";
    public List<SerializedSpan> Spans { get; set; } = new();
    public bool ThrownSwitchesSetOccupied { get; set; } = true;

    public SerializedCTCBlock() { }
    
    public SerializedCTCBlock(CTCBlock block)
    {
        Id = block.id;
        Spans = block.GetComponentsInChildren<TrackSpan>().Select(s => new SerializedSpan(s)).ToList();
        ThrownSwitchesSetOccupied = block.thrownSwitchesSetOccupied;
    }

    public void CreateFor(GameObject parent, CTCPatchingContext ctx)
    {
        var ctcBlock = parent.AddComponent<CTCBlock>();
        ctcBlock.id = Id;
        ctcBlock.thrownSwitchesSetOccupied = ThrownSwitchesSetOccupied;
        
        int index = 0;
        foreach (var serializedSpan in Spans)
        {
            index++;
            var trackSpan = parent.AddComponent<TrackSpan>();
            var type = typeof(SerializedSpan);
            trackSpan.id = $"{parent.name}-span-{index}";
            trackSpan.name = $"{parent.name}-span-{index}";
            try
            {
                type.GetMethod("ApplyTo", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(serializedSpan, [trackSpan.name, ctx, trackSpan]);
            }
            catch (Exception e)
            {
                ctx.Logger.Error(e, $"Failed to apply span {index}");
                Object.DestroyImmediate(trackSpan);
            }
            
        }
        
        ctx.Blocks[Id] = ctcBlock;
    }
}
