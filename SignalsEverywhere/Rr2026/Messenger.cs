using System;
using System.Collections.Generic;
using Game.Events;

namespace GalaSoft.MvvmLight.Messaging;

// SignalsEverywhere on Railroader 2026.1: the game's EventBus in place of MVVM Light's Messenger (gone in Railroader 2026.1): subscriptions are kept per
// recipient so Unregister can drop them all.
public class Messenger
{
    public static Messenger Default { get; } = new Messenger();

    private readonly Dictionary<object, List<IDisposable>> _subscriptions = new(ReferenceEqualityComparer.Instance);

    public void Register<TMessage>(object recipient, Action<TMessage> action)
    {
        if (!_subscriptions.TryGetValue(recipient, out var list))
        {
            list = new List<IDisposable>();
            _subscriptions[recipient] = list;
        }
        list.Add(EventBus.Subscribe(action));
    }

    public void Unregister(object recipient)
    {
        if (!_subscriptions.TryGetValue(recipient, out var list)) return;
        _subscriptions.Remove(recipient);
        foreach (var subscription in list) subscription.Dispose();
    }

    public void Send<TMessage>(TMessage message) => EventBus.Publish(message);

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
