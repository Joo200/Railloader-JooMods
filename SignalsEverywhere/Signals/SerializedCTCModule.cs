using System.Collections.Generic;
using StrangeCustoms.Tracks;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class SerializedCTCModule
{
    public string Id { get; set; } = "";
    public Dictionary<string, SerializedCTCBlock> Blocks { get; set; } = new();
    public Dictionary<string, SerializedCTCAutoSignal> AutoSignals { get; set; } = new();
    public Dictionary<string, SerializedCTCPredicateSignal> PredicateSignals { get; set; } = new();
    public SerializedCTCInterlocking? Interlocking { get; set; }
    public SerializedCTCIntermediate? Intermediate { get; set; }
    public SerializedCTCCrossover? Crossover { get; set; }
    
    public SerializedCTCModule() {}
    
    public SerializedCTCModule(GameObject go)
    {
        foreach (var block in go.GetComponentsInChildren<CTCBlock>(true))
        {
            Blocks.Add(block.id, new SerializedCTCBlock(block));
        }

        foreach (var s in go.GetComponentsInChildren<CTCAutoSignal>(true))
        {
            AutoSignals.Add(s.id, new SerializedCTCAutoSignal(s));
        }

        foreach (var s in go.GetComponentsInChildren<CTCPredicateSignal>(true))
        {
            PredicateSignals.Add(s.id, new SerializedCTCPredicateSignal(s));
        }

        var il = go.GetComponentInChildren<CTCInterlocking>(true);
        if (il != null)
        {
            Interlocking = new SerializedCTCInterlocking(il);
        }
        var im = go.GetComponentInChildren<CTCIntermediate>(true);
        if (im != null)
        {
            Intermediate = new SerializedCTCIntermediate(im);
        }
    }

    public void Initialize(GameObject gameObject, CTCPatchingContext ctx, string jsonPath)
    {
        ctx.Logger.Information($"Creating module Initialize {Id}");
        if (Interlocking != null)
        {
            ctx.Logger.Information($"Creating interlocking {Interlocking.Id}");
            if (ctx.Interlockings.ContainsKey(Interlocking.Id))
            {
                throw new SCPatchingException($"Interlocking {Interlocking.Id} already exists",
                    jsonPath + "." + Interlocking.Id + ".id");
            }
            Interlocking.CreateFor(gameObject, ctx);
        }

        if (Intermediate != null)
        {
            ctx.Logger.Information($"Creating intermediate {Intermediate.Id}");
            if (ctx.Intermediates.ContainsKey(Intermediate.Id))
            {
                throw new SCPatchingException($"Intermediate {Intermediate.Id} already exists",
                    jsonPath + "." + Intermediate.Id + ".id");
            }
            Intermediate.CreateFor(gameObject, ctx);
        }

        if (Crossover != null)
        {
            ctx.Logger.Information($"Creating crossover {Crossover.Id}");
            if (ctx.Crossovers.ContainsKey(Crossover.Id))
            {
                throw new SCPatchingException($"Crossover {Crossover.Id} already exists", jsonPath + "." + Crossover.Id + ".id");
            }

            Crossover.CreateFor(gameObject, ctx);
        }

        foreach (var serSignal in AutoSignals)
        {
            ctx.Logger.Information($"Creating auto signal {serSignal.Key}");
            if (ctx.AutoSignals.ContainsKey(serSignal.Key))
            {
                throw new SCPatchingException($"AutoSignal {serSignal.Key} already exists",
                    jsonPath + ".autoSignals." + serSignal.Key + ".id");
            }
            serSignal.Value.Id = serSignal.Key;
            serSignal.Value.CreateFor(gameObject, ctx);
        }

        foreach (var serSignal in PredicateSignals)
        {
            if (ctx.GetSignal(serSignal.Key))
            {
                throw new SCPatchingException($"PredicateSignals {serSignal.Key} already exists",
                    jsonPath + ".predicateSignals." + serSignal.Key + ".id");
            }
            serSignal.Value.Id = serSignal.Key;
            serSignal.Value.CreateFor(gameObject, ctx);
        }
        
        foreach (var serBlock in Blocks)
        {
            ctx.Logger.Information($"Creating block {serBlock.Key}");
            if (ctx.Blocks.ContainsKey(serBlock.Key))
            {
                throw new SCPatchingException($"Block {serBlock.Key} already exists",
                    jsonPath + ".blocks." + serBlock.Key + ".id");
            }
            var blockGameObject = new GameObject(serBlock.Key.ToUpper());
            blockGameObject.transform.parent = gameObject.transform;

            serBlock.Value.Id = serBlock.Key;
            serBlock.Value.CreateFor(blockGameObject, ctx);
        }
    }

    public void Finalize(CTCPatchingContext ctx)
    {
        foreach (var serSignal in AutoSignals)
        {
            if (ctx.AutoSignals.TryGetValue(serSignal.Key, out var signal))
                serSignal.Value.ApplyTo(signal, ctx);
            else
                ctx.Logger.Warning($"Auto signal {serSignal.Key} not found in patching context.");
        }
        
        foreach (var serSignal in PredicateSignals)
        {
            if (ctx.PredicateSignals.TryGetValue(serSignal.Key, out var signal))
                serSignal.Value.ApplyTo(signal, ctx);
            else
                ctx.Logger.Warning($"Predicate signal {serSignal.Key} not found in patching context.");
        }
        
        if (Intermediate != null)
        {
            if (ctx.Intermediates.TryGetValue(Intermediate.Id, out var intermediate))
                Intermediate.ApplyTo(intermediate, ctx);
            else
                ctx.Logger.Warning($"Intermediate {Intermediate.Id} not found in patching context.");
        }

        if (Interlocking != null)
        {
            if (ctx.Interlockings.TryGetValue(Interlocking.Id, out var interlocking))
                Interlocking.ApplyTo(interlocking, ctx);
            else
                ctx.Logger.Warning($"Interlocking {Interlocking.Id} not found in patching context.");

            foreach (var sblock in Blocks)
            {
                if (ctx.Blocks.TryGetValue(sblock.Key, out var block))
                {
                    block.thrownSwitchesSetOccupied = false;
                }
            }
        }
        
        if (Crossover != null)
        {
            if (ctx.Crossovers.TryGetValue(Crossover.Id, out var crossover))
                Crossover.ApplyTo(crossover, ctx);
            else
                ctx.Logger.Warning($"Crossover {Crossover.Id} not found in patching context.");
            
            foreach (var sblock in Blocks)
            {
                if (ctx.Blocks.TryGetValue(sblock.Key, out var block))
                {
                    block.thrownSwitchesSetOccupied = false;
                }
            }
        }
    }
}
