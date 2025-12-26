using System.Collections.Generic;
using System.Linq;
using StrangeCustoms.Tracks;
using Track;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class SerializedCTCPredicateSignal : SerializedCTCSignal
{
    private static CTCPredicateSignal? _triplePrefab => Object.FindObjectsOfType<CTCPredicateSignal>(true)
        .FirstOrDefault(s => s.headConfiguration == SignalHeadConfiguration.Triple);

    private static CTCPredicateSignal? _doublePrefab => Object.FindObjectsOfType<CTCPredicateSignal>(true)
        .FirstOrDefault(s => s.headConfiguration == SignalHeadConfiguration.Double);
    
    public struct HeadPredicates
    {
        public List<Predicate> Predicates { get; set; }
        public string? NextCtcSignal { get; set; }
        
        public HeadPredicates() {}

        public HeadPredicates(CTCPredicateSignal.HeadPredicates predicates)
        {
            Predicates = predicates.predicates.Select(p => new Predicate(p)).ToList();
            NextCtcSignal = predicates.nextSignal?.id;
        }
        
        public CTCPredicateSignal.HeadPredicates Apply(CTCPatchingContext ctx)
        {
            CTCPredicateSignal.HeadPredicates headPredicates = new();
            foreach (var predicate in Predicates)
            {
                headPredicates.predicates.Add(predicate.Apply(ctx));
            }
            headPredicates.nextSignal = ctx.GetSignal(NextCtcSignal);
            return headPredicates;
        }
    }

    public struct Predicate
    {
        public CTCPredicateSignal.PredicateType Type { get; set; }
        public string SwitchNode { get; set; }
        public SwitchSetting SwitchSetting { get; set; }
        public List<string> Blocks { get; set; }
        public string Interlocking { get; set; }
        public SignalDirection Direction { get; set; }
        
        public Predicate() {}

        public Predicate(CTCPredicateSignal.Predicate predicate)
        {
            Type = predicate.type;
            SwitchNode = predicate.switchNode?.id;
            SwitchSetting = predicate.switchSetting;
            Blocks = predicate.blocks?.Select(b => b.id).ToList();
            Interlocking = predicate.interlocking?.id;
            Direction = predicate.direction;
        }

        public CTCPredicateSignal.Predicate Apply(CTCPatchingContext ctx)
        {
            CTCPredicateSignal.Predicate predicate = new CTCPredicateSignal.Predicate();
            predicate.type = Type;
            predicate.switchNode = Graph.Shared.GetNode(SwitchNode);
            predicate.switchSetting = SwitchSetting;
            predicate.blocks = ctx.GetBlocks(Blocks);
            predicate.interlocking = ctx.Interlockings[Interlocking];
            predicate.direction = Direction;
            return predicate;
        }
    }
    
    public List<HeadPredicates> Heads { get; set; }

    public SerializedCTCPredicateSignal() {}
    
    public SerializedCTCPredicateSignal(CTCPredicateSignal signal) : base(signal)
    {
        Heads = signal.heads.Select(h => new HeadPredicates(h)).ToList();
    }

    public void CreateFor(GameObject parent, CTCPatchingContext ctx)
    {
        CTCPredicateSignal? signal = Object.Instantiate(HeadConfiguration == SignalHeadConfiguration.Triple ? _triplePrefab : _doublePrefab, parent.transform, false);
        if (!signal)
        {
            throw new SCPatchingException("Unable to instantiate new signal");
        }
        base.ApplyTo(signal, ctx);
        ctx.PredicateSignals[signal.id] = signal;
    }

    public void ApplyTo(CTCPredicateSignal signal, CTCPatchingContext ctx)
    {
        signal.heads = new();
        foreach (var predicate in Heads)
        {
            signal.heads.Add(predicate.Apply(ctx));
        }
    }
}