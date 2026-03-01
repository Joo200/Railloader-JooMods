using System;
using System.Collections.Generic;
using System.Linq;
using Game.Messages;
using Game.State;
using KeyValue.Runtime;
using Serilog;
using SignalsEverywhere.Signals;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Track;
using Track.Signals;
using ValueType = KeyValue.Runtime.ValueType;

namespace SignalsEverywhere.Panel;

public class ThreeWayKnob : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public enum KnobPosition
    {
        Left = 0,
        Up = 1,
        Right = 2
    }

    public enum Purpose
    {
        Unknown,
        Signal,
        Switch,
        CrossoverSignal
    }
    
    private Purpose _purpose = Purpose.Unknown;

    public RectTransform KnobHandle;
    public Image LeftIndicator;
    public Image RightIndicator;
    public Image? UpperIndicator;

    public Color LeftColor = Color.green;
    public Color RightColor = Color.green;

    public SwitchSetting CurrentSwitchSetting
    {
        get => CTCPanelKnob.SwitchSettingFromValue(_keyValueObject[CTCKeys.Knob(KnobId)]);
    } 
    
    public SignalDirection CurrentDirection
    {
        get => CTCPanelKnob.DirectionFromValue(_keyValueObject[CTCKeys.Knob(KnobId)]);
    } 
    
    public string ParentId { get; set; }
    public string KnobId { get; set; }
    
    public string DisplayName { get; set; }
    
    public Schematic? Schematic { get; set; }
    public List<TrackNode>? Nodes;

    public List<KnobPosition> AllowedPositions = [KnobPosition.Left, KnobPosition.Up, KnobPosition.Right];
    
    private readonly HashSet<IDisposable> _observers = new();
    
    private KeyValueObject? _keyValueObject;
    
    private bool _playSound = false;

    public void RegisterCrossoverSignalListener(SignalStorage storage, string groupId, string knobId)
    {
        _purpose = Purpose.CrossoverSignal;
        _keyValueObject = storage.GetComponent<KeyValueObject>();
        ParentId = groupId;
        KnobId = knobId;

        var value = _keyValueObject[CTCKeys.Knob(KnobId)];
        if (value.Type == ValueType.Int)
        {
            UpdateVisuals(GetKnobPosition(CTCPanelKnob.DirectionFromValue(value)));
            UpdateSignalIndicator(CTCPanelKnob.DirectionFromValue(value));
        }
        else
        {
            _keyValueObject[CTCKeys.Knob(KnobId)] = Value.Int((int) SignalDirection.None);
            UpdateSignalIndicator(SignalDirection.None);
        }
        
        _observers.Add(_keyValueObject.Observe(CTCKeys.Knob(KnobId), e =>
        {
            UpdateVisuals(GetKnobPosition(CTCPanelKnob.DirectionFromValue(e)));
            if (_playSound) SoundHelper.PlayKlick();
            else _playSound = true;
        }, false));
        _observers.Add(storage.ObserveCrossoverGroupDirection(groupId, e =>
        {
            switch (e)
            {
                case CTCTrafficFilter.Any:
                case CTCTrafficFilter.None:
                    UpdateSignalIndicator(SignalDirection.None);
                    break;
                case CTCTrafficFilter.Left:
                    UpdateSignalIndicator(SignalDirection.Left);
                    break;
                case CTCTrafficFilter.Right:
                    UpdateSignalIndicator(SignalDirection.Right);
                    break;
            }   
        }));
    }
    
    public void RegisterSignalListener(SignalStorage storage, string interlockingId, string knobId)
    { 
        _purpose = Purpose.Signal;
        _keyValueObject = storage.GetComponent<KeyValueObject>();
        KnobId = knobId;
        ParentId = interlockingId;

        var value = _keyValueObject[CTCKeys.Knob(KnobId)];
        if (value.Type == ValueType.Int)
        {
            UpdateVisuals(GetKnobPosition(CTCPanelKnob.DirectionFromValue(value)));
            UpdateSignalIndicator(CTCPanelKnob.DirectionFromValue(value));
        }
        else
        {
            _keyValueObject[CTCKeys.Knob(KnobId)] = Value.Int((int) SignalDirection.None);
            UpdateSignalIndicator(SignalDirection.None);
        }
        
        _observers.Add(_keyValueObject.Observe(CTCKeys.Knob(KnobId), e =>
        {
            UpdateVisuals(GetKnobPosition(CTCPanelKnob.DirectionFromValue(e)));
            if (_playSound) SoundHelper.PlayKlick();
            else _playSound = true;
        }, false));
        _observers.Add(storage.ObserveInterlockingDirection(interlockingId, UpdateSignalIndicator));
    }

    private void UpdateSignalIndicator(SignalDirection direction)
    {
        LeftIndicator.color = Color.gray;
        RightIndicator.color = Color.gray;
        UpperIndicator?.color = Color.gray;
        if (direction == SignalDirection.Left)
        {
            LeftIndicator.color = LeftColor;
        } else if (direction == SignalDirection.Right)
        {
            RightIndicator.color = RightColor;
        } else if (direction == SignalDirection.None)
        {
            UpperIndicator?.color = Color.red;
        }
    }

    public void RegisterSwitchListener(SignalStorage storage, List<TrackNode> nodes, string knobId)
    {
        _purpose = Purpose.Switch;
        _keyValueObject = storage.GetComponent<KeyValueObject>();
        KnobId = knobId;
        Nodes = nodes;
        
        var value = _keyValueObject[CTCKeys.Knob(KnobId)];
        if (value.Type == ValueType.Int)
        {
            UpdateVisuals(GetKnobPosition(CTCPanelKnob.SwitchSettingFromValue(value)));
            UpdateDirectionSwitch(CTCPanelKnob.SwitchSettingFromValue(value));
        }
        else
        {
            SwitchSetting improvised = nodes.First().isThrown ? SwitchSetting.Reversed : SwitchSetting.Normal;
            _keyValueObject[CTCKeys.Knob(KnobId)] = Value.Int((int) (improvised));
            UpdateVisuals(GetKnobPosition(improvised));
            UpdateDirectionSwitch(improvised);
        }
        
        _observers.Add(_keyValueObject.Observe(CTCKeys.Knob(KnobId), e =>
        {
            UpdateVisuals(GetKnobPosition(CTCPanelKnob.SwitchSettingFromValue(e)));
            if (_playSound) SoundHelper.PlayKlick();
            else _playSound = true;
        }, false));
        foreach (var node in nodes)
        {
            _observers.Add(storage.ObserveSwitchPosition(node.id, UpdateDirectionSwitch));
        }
    }

    private void UpdateDirectionSwitch(SwitchSetting setting)
    {
        switch (setting)
        {
            case SwitchSetting.Normal:
                if (LeftIndicator != null) LeftIndicator.color = LeftColor;
                if (RightIndicator != null) RightIndicator.color = Color.gray;
                break;
            case SwitchSetting.Reversed:
                if (LeftIndicator != null) LeftIndicator.color = Color.gray;
                if (RightIndicator != null) RightIndicator.color = RightColor;
                break;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var pos = GetKnobPosition(CurrentDirection);

        var index = AllowedPositions.IndexOf(pos);
        if (index < 0)
            index = 0;

        int delta =
            eventData.button == PointerEventData.InputButton.Left ? -1 :
            eventData.button == PointerEventData.InputButton.Right ?  1 :
            0;

        if (delta == 0)
            return;

        var newIndex = index + delta;

        // ensure not at beginning/end (no wrap, no out-of-range)
        if (newIndex < 0 || newIndex >= AllowedPositions.Count)
            return;

        pos = AllowedPositions[newIndex];
        UpdateVisuals(pos);
        UpdateSignalKnob(pos);
    }

    private void AlertHover(bool hover)
    {
        if (!string.IsNullOrEmpty(DisplayName) && Schematic != null)
        {
            Log.Debug($"Invoking hover event for {DisplayName} to {hover} on schematic {Schematic.Name}");
            Schematic.OnHighlightRequest?.Invoke(DisplayName, hover);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        AlertHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AlertHover(false);
    }

    public void SetIndicator(KnobPosition position)
    {
        LeftIndicator.color = Color.gray;
        RightIndicator.color = Color.gray;
        if (position == KnobPosition.Left)
            LeftIndicator.color = LeftColor;
        if (position == KnobPosition.Right)
            RightIndicator.color = RightColor;
    }

    public void UpdateVisuals(KnobPosition position)
    {
        if (KnobHandle != null)
            KnobHandle.localEulerAngles = new Vector3(0, 0,
                (position == KnobPosition.Left) ? 45f : (position == KnobPosition.Right) ? -45f : 0f);
    }

    public SwitchSetting GetSwitchSetting(KnobPosition position)
    {
        return position == KnobPosition.Left ? SwitchSetting.Normal : SwitchSetting.Reversed;
    }

    public SignalDirection GetSignalDirection(KnobPosition position)
    {
        switch (position)
        {
            case KnobPosition.Left: return SignalDirection.Left;
            case KnobPosition.Right: return SignalDirection.Right;
            default:
            case KnobPosition.Up: return SignalDirection.None;
        }
    }

    public KnobPosition GetKnobPosition(SignalDirection direction)
    {
        switch (direction)
        {
            case SignalDirection.Left: return KnobPosition.Left;
            case SignalDirection.Right: return KnobPosition.Right;
            default: return KnobPosition.Up;
        }
    }

    public KnobPosition GetKnobPosition(SwitchSetting setting)
    {
        return setting == SwitchSetting.Normal ? KnobPosition.Left : KnobPosition.Right;
    }

    public void UpdateSignalKnob(KnobPosition position)
    {
        switch (_purpose)
        {
         case Purpose.Unknown:
             break;
         case Purpose.CrossoverSignal:
         case Purpose.Signal:
             MessageForValueChange((int) GetSignalDirection(position));
             _keyValueObject[KnobId] = Value.Int((int) GetSignalDirection(position));
             break;
         case Purpose.Switch:
             MessageForValueChange((int) GetSwitchSetting(position));
             _keyValueObject[KnobId] = Value.Int((int) GetSwitchSetting(position));
             break;
        }
    }

    private void OnDisable()
    {
        foreach (var ds in _observers)
        {
            ds.Dispose();
        }
        _observers.Clear();
    }

    private void MessageForValueChange(int num)
    {
        StateManager.ApplyLocal(new PropertyChange(_keyValueObject.RegisteredId, CTCKeys.Knob(KnobId),
            new IntPropertyValue(num)));
    }

    private void OnEnable()
    {
        var storage = CTCPanelController.Shared.GetComponentInParent<SignalStorage>();
        switch (_purpose)
        {
            case Purpose.CrossoverSignal:
                if (ParentId != null && KnobId != null)
                    RegisterCrossoverSignalListener(storage, ParentId, KnobId);
                break;
            case Purpose.Signal:
                if (ParentId != null && KnobId != null)
                    RegisterSignalListener(storage, ParentId, KnobId);
                break;
            case Purpose.Switch:
                if (Nodes != null && KnobId != null)
                    RegisterSwitchListener(storage, Nodes, KnobId);
                break;
        }
    }


    public static ThreeWayKnob AddThreePositionKnob(Transform parent, string labelL, string labelOff, string labelR, Schematic schematic, float scale = 1.0f, bool upperIndicator = false)
    {
        var go = new GameObject("TextKnob");
        go.transform.SetParent(parent, false);
        var rootRect = go.AddComponent<RectTransform>();
        
        // Scaled dimensions
        float width = 80f * scale;
        float height = 80f * scale;
        rootRect.sizeDelta = new Vector2(width, height);
    
        var layout = rootRect.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = height;
        layout.preferredWidth = width;
        layout.minWidth = width * 0.8f;

        // 2. Labels Area (Manual Positioning)
        GameObject labelArea = new GameObject("LabelArea");
        labelArea.transform.SetParent(rootRect.transform, false);
        var labelAreaRect = labelArea.AddComponent<RectTransform>();
        labelAreaRect.sizeDelta = new Vector2(width, 30 * scale);
        labelAreaRect.anchoredPosition = new Vector2(0, 25 * scale); // Position above the knob center

        // L Label: Aligned with the -45 deg position
        var l1 = ViewCreator.CreateLabel(labelArea.transform, labelL, scale);
        l1.rectTransform.anchoredPosition = new Vector2(-28 * scale, 0); 

        // Off Label: Aligned with the center (Up)
        var l2 = ViewCreator.CreateLabel(labelArea.transform, labelOff, scale);
        l2.rectTransform.anchoredPosition = new Vector2(0, 5 * scale); // Slightly higher for "Up"

        // R Label: Aligned with the 45 deg position
        var l3 = ViewCreator.CreateLabel(labelArea.transform, labelR, scale);
        l3.rectTransform.anchoredPosition = new Vector2(28 * scale, 0);

        // 3. Knob Area
        GameObject knobArea = new GameObject("KnobArea");
        knobArea.transform.SetParent(rootRect.gameObject.transform, false);
        var areaRect = knobArea.AddComponent<RectTransform>();
        areaRect.sizeDelta = new Vector2(80 * scale, 60 * scale);

        var bg = knobArea.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0);

        // 4. The Knob Visuals - Scaled
        var knobBase = ViewCreator.CreateImage(knobArea.transform, "Base", new Vector2(40 * scale, 40 * scale), Color.grey);
        var knobHandle = ViewCreator.CreateImage(knobBase.transform, "Handle", new Vector2(8 * scale, 24 * scale), Color.white);
        knobHandle.pivot = new Vector2(0.5f, 0.2f);
    
        // 6. Hook up logic
        ThreeWayKnob component = knobArea.AddComponent<ThreeWayKnob>();
        component.KnobHandle = knobHandle;
        component.DisplayName = labelOff;
        component.Schematic = schematic;

        // 5. Indicators (Lights) - Scaled position and size
        var lightL = ViewCreator.CreateImage(knobArea.transform, "LightL", new Vector2(8 * scale, 8 * scale), Color.gray);
        lightL.anchoredPosition = new Vector2(-25 * scale, 20 * scale);
        component.LeftIndicator = lightL.GetComponent<Image>();

        if (upperIndicator)
        {
            var lightU = ViewCreator.CreateImage(knobArea.transform, "LightN", new Vector2(8 * scale, 8 * scale), Color.gray);
            lightU.anchoredPosition = new Vector2(0, 25 * scale);
            component.UpperIndicator = lightU.GetComponent<Image>();
        }
    
        var lightR = ViewCreator.CreateImage(knobArea.transform, "LightR", new Vector2(8 * scale, 8 * scale), Color.gray);
        lightR.anchoredPosition = new Vector2(25 * scale, 20 * scale);
        component.RightIndicator = lightR.GetComponent<Image>();

        return component;
    }
}