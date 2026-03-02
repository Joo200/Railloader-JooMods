using System;
using System.Collections.Generic;
using Serilog;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class CTCInversedBlock : MonoBehaviour
{
    private Serilog.ILogger Logger = Log.ForContext<CTCInversedBlock>();
    
    public CTCBlock A;
    public CTCBlock B;

    private HashSet<IDisposable> _observers = new();
    private SignalStorage? _storage;

    private bool _currentlyChanging = false;
    
    private void OnEnable()
    {
        _storage = GetComponentInParent<SignalStorage>();
        if (_storage == null)
        {
            Logger.Warning("Couldn't find SignalStorage");
            return;
        }

        _observers.Add(_storage.ObserveBlockTrafficFilter(A.id, b =>
        {
            if (_currentlyChanging) return;
            _currentlyChanging = true;
            B.TrafficFilter = Inverse(b);
            _currentlyChanging = false;
        }));
        _observers.Add(_storage.ObserveBlockTrafficFilter(B.id, b =>
        {
            if (_currentlyChanging) return;
            _currentlyChanging = true;
            A.TrafficFilter = Inverse(b);
            _currentlyChanging = false;
        }));
    }

    private void OnDisable()
    {
        foreach (var disposable in _observers)
        {
            disposable.Dispose();
        }
        _observers.Clear();
    }

    private CTCTrafficFilter Inverse(CTCTrafficFilter filter)
    {
        switch (filter) 
        {
            case CTCTrafficFilter.Left:
                return CTCTrafficFilter.Right;
            case CTCTrafficFilter.Right:
                return CTCTrafficFilter.Left;
            default:
                return filter;
        }
    }
}