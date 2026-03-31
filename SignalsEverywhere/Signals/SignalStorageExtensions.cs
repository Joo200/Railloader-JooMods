using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KeyValue.Runtime;
using Track.Signals;

namespace SignalsEverywhere.Signals;

public static class SignalStorageExtensions
{
    private static KeyValueObject Kvo(this SignalStorage storage) => storage.GetComponent<KeyValueObject>();
    
    private static AccessTools.FieldRef<CTCAutoSignal, HashSet<IDisposable>> FieldRef = AccessTools.FieldRefAccess<CTCAutoSignal, HashSet<IDisposable>>("Observers");
    private static AccessTools.FieldRef<CTCPredicateSignal, HashSet<IDisposable>> PredicateFieldRef = AccessTools.FieldRefAccess<CTCPredicateSignal, HashSet<IDisposable>>("Observers");
    
    public static void UpdateSignalOnChange<T>(
        this CTCAutoSignal signal,
        Func<string, Action<T>, IDisposable> observeAction,
        string itemId)
    {
        var method = typeof(CTCAutoSignal).GetMethod("SetNeedsUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldRef(signal).Add(observeAction(itemId, _ => method.Invoke(signal, null)));
    }

    public static void UpdatePredicateSignalOnChange<T>(
        this CTCPredicateSignal signal,
        Func<string, Action<T>, IDisposable> observeAction,
        string itemId)
    {
        var method = typeof(CTCPredicateSignal).GetMethod("SetNeedsUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
        if (method == null)
            method = typeof(CTCSignal).GetMethod("SetNeedsUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        
        PredicateFieldRef(signal).Add(observeAction(itemId, _ => method.Invoke(signal, null)));
    }
    
    public static string CrossoverDirKey(string groupId) => $"crossover:{groupId}:direction";
    
    public static IDisposable ObserveCrossoverGroupDirection(this SignalStorage storage, string groupId,
        Action<CTCTrafficFilter> onDirectionChanged)
    {
        return ObserveCrossoverGroupDirection(storage, groupId, onDirectionChanged, true);
    }
    
    public static IDisposable ObserveCrossoverGroupDirection(this SignalStorage storage, string groupId, Action<CTCTrafficFilter> onDirectionChanged, bool callInitial)
    {
        return storage.Kvo().Observe(CrossoverDirKey(groupId),
            (Action<Value>)(value => onDirectionChanged((CTCTrafficFilter)value.IntValue)), callInitial);
    }

    public static CTCTrafficFilter GetCrossoverGroupDirection(this SignalStorage storage, string groupId)
    {
        return (CTCTrafficFilter)storage.Kvo().Get(CrossoverDirKey(groupId)).IntValue;
    }

    public static void SetCrossoverGroupDirection(this SignalStorage storage, string groupId,
        CTCTrafficFilter direction)
    {
        Value obj = Value.IntOrNull((int)direction);
        storage.Kvo().Set(CrossoverDirKey(groupId), obj);
    }
}