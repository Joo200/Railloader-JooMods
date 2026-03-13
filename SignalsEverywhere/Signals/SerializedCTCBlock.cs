using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StrangeCustoms.Tracks;
using Track;
using Track.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Signals;

public class SerializedCTCBlock
{
    private static readonly AccessTools.FieldRef<CTCBlock, TrackSpan[]> _spans = AccessTools.FieldRefAccess<CTCBlock, TrackSpan[]>(nameof(_spans));
    
    public string Id { get; set; } = "";
    public List<SerializedSpan> Spans { get; set; } = new();
    public bool ThrownSwitchesSetOccupied { get; set; } = true;
    public bool CreateInverse { get; set; } = false;

    public SerializedCTCBlock() { }
    
    public SerializedCTCBlock(CTCBlock block)
    {
        Id = block.id;
        Spans = block.GetComponentsInChildren<TrackSpan>().Select(s => new SerializedSpan(s)).ToList();
        ThrownSwitchesSetOccupied = block.thrownSwitchesSetOccupied;
    }

    public void CreateFor(GameObject parent, CTCPatchingContext ctx)
    {
        CTCBlock ctcBlock = parent.GetComponent<CTCBlock>() ?? parent.AddComponent<CTCBlock>();
        ctcBlock.id = Id;
        ctcBlock.thrownSwitchesSetOccupied = ThrownSwitchesSetOccupied;
        
        ApplySpans(ctcBlock, ctx);

        ctx.Blocks[Id] = ctcBlock;
    }

    public void ApplySpans(CTCBlock block, CTCPatchingContext ctx)
    {
        block.GetComponents<TrackSpan>().ToList().ForEach(Object.DestroyImmediate);
        int index = 0;
        foreach (var serializedSpan in Spans)
        {
            index++;
            var trackSpan = block.gameObject.AddComponent<TrackSpan>();
            var type = typeof(SerializedSpan);
            trackSpan.id = $"{block.name}-span-{index}";
            trackSpan.name = $"{block.name}-span-{index}";
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
        _spans(block) = null;
        
        if (CreateInverse)
            CreateInverseFor(block, ctx);
    }

    private void CreateInverseFor(CTCBlock block, CTCPatchingContext ctx)
    {
        if (ctx.Blocks.ContainsKey($"{block.id}-inv"))
            return;
        
        CTCBlock inv = Object.Instantiate(block, block.transform.parent.transform, false);
        inv.id = $"{block.id}-inv";
        foreach (var s in inv.GetComponentsInChildren<TrackSpan>())
            s.id += "-inv";
        ctx.Blocks[inv.id] = inv;
        var inversed = block.transform.parent.gameObject.AddComponent<CTCInversedBlock>();
        inversed.A = block;
        inversed.B = inv;
    }
}
