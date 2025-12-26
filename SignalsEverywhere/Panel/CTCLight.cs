using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Model;
using Serilog;
using Track.Signals;
using UI.Builder;
using UnityEngine;
using UnityEngine.UI;

namespace SignalsEverywhere.Panel;

public class CTCLight : MonoBehaviour
{
    public Color OnColor = Color.white;
    public Color OffColor = Color.black;

    private Image _image;
    public string Block = "";

    public IDisposable _listener;

    private TooltipInfo _cachedTooltipInfo;
    private float _cachedTooltipExpires;

    private bool _lit;
    
    public TooltipInfo TooltipInfo
    {
        get
        {
            if (_cachedTooltipExpires < (double) Time.unscaledTime)
            {
                _cachedTooltipInfo = BuildTooltip();
                _cachedTooltipExpires = Time.unscaledTime + 1f;
            }
            return _cachedTooltipInfo;
        }
    }
    
    private TooltipInfo BuildTooltip()
    {
        if (Block == null)
        {
            return new TooltipInfo("Loading", null);
        }
        CTCBlock block = CTCPanelController.Shared.BlockForId(Block);
        if (_lit)
        {
            HashSet<Car> source = block.CarsInBlock();
            List<Car> list = source.Where(car => car.IsLocomotive).OrderBy(car => car.DisplayName).ToList();
            string str = source.Count.Pluralize("car");
            string text = list.Count <= 0 ? str : $"{string.Join(", ", list.Select(l => l.DisplayName))} ({str})";
            string title = "Occupied Block";
            return new TooltipInfo(title, text);
        }
        return new TooltipInfo(null, null);
    }
    
    public void OnEnable()
    {
        _image = GetComponent<Image>();
        SetState(_lit);
        
        var rect = GetComponent<RectTransform>();
        rect.Tooltip(() => TooltipInfo);

        if (_listener != null)
        {
            _listener.Dispose();
        }
        if (Block == null) return;
        _listener = CTCPanelController.Shared.GetComponentInParent<SignalStorage>(true).ObserveBlockOccupancy(Block, SetState);
    }

    public void Initialize(string block)
    {
        Block = block;
        var storage = CTCPanelController.Shared.GetComponentInParent<SignalStorage>(true);
        _listener = storage.ObserveBlockOccupancy(Block, SetState);
        SetState(storage.GetBlockOccupied(Block));
    }

    public void SetState(bool state)
    {
        _lit = state;
        if (_image == null) return;
        _image.color = state ? OnColor : OffColor;
    }

    private void OnDisable()
    {
        _listener.Dispose();
        var rect = GetComponent<RectTransform>();
        rect.Tooltip(null);
    }

    public void OnDestroy()
    {
        _listener = null;
        _image = null;
    }
}