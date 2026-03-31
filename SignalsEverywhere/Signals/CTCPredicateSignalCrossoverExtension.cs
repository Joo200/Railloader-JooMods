using System;
using System.Collections.Generic;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class CTCPredicateSignalCrossoverExtension : MonoBehaviour
{
    [Serializable]
    public class CrossoverPredicate
    {
        public CTCCrossover crossover;
        public string crossoverGroupId;
        public CTCTrafficFilter direction;
        public bool isNot;
    }

    [Serializable]
    public class HeadCrossoverPredicates
    {
        public List<CrossoverPredicate> predicates = new();
    }

    public List<HeadCrossoverPredicates> heads = new();

    public bool IsSatisfied(int headIndex)
    {
        if (headIndex < 0 || headIndex >= heads.Count)
            return true;

        foreach (var predicate in heads[headIndex].predicates)
        {
            if (!IsSatisfied(predicate))
                return false;
        }
        return true;
    }

    private bool IsSatisfied(CrossoverPredicate predicate)
    {
        if (predicate.crossover == null)
            return true;

        var storage = predicate.crossover.GetComponentInParent<SignalStorage>();
        if (storage == null)
            return true;

        var currentDirection = storage.GetCrossoverGroupDirection(predicate.crossoverGroupId);
        bool matches = currentDirection == predicate.direction;

        return predicate.isNot ? !matches : matches;
    }
}
