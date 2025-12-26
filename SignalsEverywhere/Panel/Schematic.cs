using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KeyValue.Runtime;
using Serilog;
using Track.Signals;
using Track.Signals.Panel;
using UI.Builder;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Panel;

public class Schematic : MonoBehaviour
{
    Serilog.ILogger logger = Log.ForContext<Schematic>();
    
    public string Name { get; private set; }
    public List<CTCPanelLayout.SchematicElement> Elements { get; set; }
    public Dictionary<string, PanelMarker> Markers { get; } = new();
    
    public float MaxX { get; private set; }
    public int MinY { get; private set; }
    public int MaxY { get; private set; }

    public Action<string, bool>? OnHighlightRequest;

    private RectTransform? _view;
    private RectTransform? _controlRow;
    
    private HashSet<IDisposable> _observers = new();
    private KeyValueObject? _markerKvo;

    public void Initialize(string name, List<CTCPanelLayout.SchematicElement> elements)
    {
        Name = name;
        Elements = elements;
        
        if (elements.Count > 0)
        {
            MaxX = elements.Max(e => e.X) + 2;
            MinY = elements.Min(e => e.Y) - 2;
            MaxY = elements.Max(e => e.Y) + 2;
        }
        else
        {
            MaxX = 10;
            MinY = -2;
            MaxY = 3;
        }
    }

    private void OnDisable()
    {
        foreach (var disposable in _observers)
        {
            disposable.Dispose();
        }
        _observers.Clear();
        
        foreach (var marker in Markers.Values)
        {
            if (marker != null)
                Destroy(marker.gameObject);
        }
        Markers.Clear();
    }

    public void Build(UIPanelBuilder builder, float scale = 1.0f)
    {
        OnHighlightRequest = null;
        _view = ViewCreator.CreateSchematic(this, builder, scale);
        Markers.Clear();
        InitializeMarkersForSchematic();
        
        if (CTCPanelController.Shared.SystemMode == SystemMode.CTC)
        {
            logger.Information("Adding interlocking control columns");
            _controlRow = ViewCreator.CreateControlRow(builder, this);
            
            foreach (var element in Elements)
            {
                if (element.Interlock != null)
                {
                    InterlockingControlColumn.CreateControl(_controlRow, element, this, scale);    
                }
                if (element.Crossover != null)
                {
                    CrossoverControlColumn.CreateControl(_controlRow, element, this, scale);
                }
            }
        }
        else
        {
            logger.Information("CTC mode not active, skipping interlocking control columns");
        }
    }

    private void InitializeMarkersForSchematic()
    {
        var markerManager = Object.FindAnyObjectByType<CTCPanelMarkerManager>();
        if (markerManager == null) return;
        _markerKvo = markerManager.GetComponentInParent<KeyValueObject>();
        string prefix = "marker-";
        if (Name != "Mainline")
            prefix = $"markerbranch-{Name.Replace(" ", "_").Replace("-", "_")}-";
        
        logger.Information($"Found marker manager size with {_markerKvo.Keys.Count()} and filtered {_markerKvo.Keys.Count(k => k.StartsWith(prefix))} keys starting with {prefix}");
        foreach (var key in _markerKvo.Keys.Where(k => k.StartsWith(prefix)))
        {
            _observers.Add(_markerKvo.Observe(key, v => {
                if (v.IsNull)
                    DeleteMarker(key);
                else
                    UpdateMarkerFromKVO(key);
            }, true));
        }
        
        _observers.Add(_markerKvo.ObserveKeyChanges((key, change) =>
        {
            if (!key.StartsWith(prefix)) return;
            _observers.Add(_markerKvo.Observe(key, v => {
                if (v.IsNull)
                    DeleteMarker(key);
                else
                    UpdateMarkerFromKVO(key);
            }, true));
        }));
    }

    private void DeleteMarker(string key)
    {
        if (Markers.TryGetValue(key, out var marker))
        {
            Object.DestroyImmediate(marker.transform.gameObject);
            Markers.Remove(key);
        }
    }

    private void UpdateMarkerFromKVO(string key)
    {
        if (_view == null)
        {
            logger.Warning("View not yet created");
        }
        
        var val = _markerKvo[key];
        if (val.IsNull) return;
        var dict = val.DictionaryValue;
        if (!Markers.TryGetValue(key, out var marker))
        {
            logger.Information($"Adding marker {key}");
            marker = PanelMarker.CreateMarker(_view, key, dict["text"].StringValue, MinY, MaxY, Name);
            Markers[key] = marker;
        }

        marker.UpdatePosition(dict, _view.sizeDelta.y);
    }
}
