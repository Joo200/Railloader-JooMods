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
        if (Interlocking != null && ctx.ElementModified(jsonPath + ".interlocking"))
        {
            ctx.Logger.Debug($"Creating interlocking {Interlocking.Id}");
            Interlocking.CreateFor(gameObject, ctx);
        }
        else if (gameObject.GetComponent<CTCInterlocking>() != null)
        {
            ctx.Logger.Debug($"Deleting existing interlocking {gameObject.name}");
            Object.DestroyImmediate(gameObject.GetComponent<CTCInterlocking>());
        }

        if (Intermediate != null && ctx.ElementModified(jsonPath + ".intermediate"))
        {
            ctx.Logger.Debug($"Creating intermediate {Intermediate.Id}");
            Intermediate.CreateFor(gameObject, ctx);
        }
        else if (gameObject.GetComponent<CTCIntermediate>() != null)
        {
            ctx.Logger.Debug($"Deleting existing intermediate {gameObject.name}");
            Object.DestroyImmediate(gameObject.GetComponent<CTCIntermediate>());
        }

        if (Crossover != null && ctx.ElementModified(jsonPath + ".crossover"))
        {
            ctx.Logger.Debug($"Creating crossover {Crossover.Id}");
            Crossover.CreateFor(gameObject, ctx);
        }
        else if (gameObject.GetComponent<CTCCrossover>() != null)
        {
            ctx.Logger.Debug($"Deleting existing crossover {gameObject.name}");
            Object.DestroyImmediate(gameObject.GetComponent<CTCCrossover>());
        }

        foreach (var serSignal in AutoSignals)
        {
            if (!ctx.ElementModified(jsonPath + ".autoSignals." + serSignal.Key))
                continue;
            
            if (ctx.AutoSignals.TryGetValue(serSignal.Key, out var signal))
            {
                ctx.Logger.Debug($"Deleting existing auto signal {serSignal.Key}");
                Object.DestroyImmediate(signal.gameObject);
                ctx.AutoSignals.Remove(serSignal.Key);
            }

            if (serSignal.Value != null)
            {
                ctx.Logger.Debug($"Creating auto signal {serSignal.Key}");
                serSignal.Value.Id = serSignal.Key;
                serSignal.Value.CreateFor(gameObject, ctx);    
            }
        }

        foreach (var serSignal in PredicateSignals)
        {
            if (!ctx.ElementModified(jsonPath + ".predicateSignals." + serSignal.Key))
                continue;
            
            if (ctx.PredicateSignals.TryGetValue(serSignal.Key, out var signal))
            {
                ctx.Logger.Debug($"Deleting existing predicate signal {serSignal.Key}");
                Object.DestroyImmediate(signal.gameObject);
                ctx.PredicateSignals.Remove(serSignal.Key);
            }
            serSignal.Value.Id = serSignal.Key;
            serSignal.Value.CreateFor(gameObject, ctx);
        }
        
        foreach (var serBlock in Blocks)
        {
            if (!ctx.ElementModified(jsonPath + ".blocks." + serBlock.Key))
                continue;
            
            if (ctx.Blocks.TryGetValue(serBlock.Key, out var block))
            {
                ctx.Logger.Debug($"Deleting existing block {serBlock.Key}");
                Object.DestroyImmediate(block);
                ctx.Blocks.Remove(serBlock.Key);
            }

            if (serBlock.Value != null)
            {
                ctx.Logger.Debug($"Creating block {serBlock.Key}");
                var blockGameObject = new GameObject(serBlock.Key.ToUpper());
                blockGameObject.transform.parent = gameObject.transform;

                serBlock.Value.Id = serBlock.Key;
                serBlock.Value.CreateFor(blockGameObject, ctx);
            }
        }
    }

    public void Finalize(CTCPatchingContext ctx, string jsonPath)
    {
        foreach (var serSignal in AutoSignals)
        {
            if (!ctx.ElementModified(jsonPath + ".autoSignals." + serSignal.Key))
                continue;
            if (ctx.AutoSignals.TryGetValue(serSignal.Key, out var signal))
                serSignal.Value.ApplyTo(signal, ctx);
            else
                ctx.Logger.Warning($"Auto signal {serSignal.Key} not found in patching context.");
        }
        
        foreach (var serSignal in PredicateSignals)
        {
            if (!ctx.ElementModified(jsonPath + ".predicateSignals." + serSignal.Key))
                continue;
            if (ctx.PredicateSignals.TryGetValue(serSignal.Key, out var signal))
                serSignal.Value.ApplyTo(signal, ctx);
            else
                ctx.Logger.Warning($"Predicate signal {serSignal.Key} not found in patching context.");
        }
        
        if (Intermediate != null && ctx.ElementModified(jsonPath + ".intermediate"))
        {
            if (ctx.Intermediates.TryGetValue(Intermediate.Id, out var intermediate))
                Intermediate.ApplyTo(intermediate, ctx);
            else
                ctx.Logger.Warning($"Intermediate {Intermediate.Id} not found in patching context.");
        }

        if (Interlocking != null && ctx.ElementModified(jsonPath + ".interlocking"))
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
        
        if (Crossover != null && ctx.ElementModified(jsonPath + ".crossover"))
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
