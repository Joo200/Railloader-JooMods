using System;
using System.Collections.Generic;
using System.Linq;
using Audio;
using Game.State;
using Helpers;
using KeyValue.Runtime;
using Serilog;
using SignalsEverywhere.Patching;
using SignalsEverywhere.Signals;
using Track;
using Track.Signals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Panel;

public class CTCKeyValueObserver : MonoBehaviour
{
    Serilog.ILogger logger = Log.ForContext<CTCKeyValueObserver>();
    
    private readonly HashSet<string> _resetButtonIds = new();
    
    private KeyValueObject? _kvo;
    
    private HashSet<IDisposable> _observers = new();

    private CTCPanelLayout? _layout => SignalsEverywhere.Shared.PanelLayout;
    
    public void OnEnable()
    {
        CTCPanelController controller = GetComponent<CTCPanelController>();
        var storage = controller.GetComponentInParent<SignalStorage>();
        _kvo = storage.GetComponent<KeyValueObject>();
        
        foreach (var id in _layout.panel.Values.SelectMany(e => e).Select(e => e.Interlock?.Interlock))
        {
            if (id == null) continue;
            _observers.Add(_kvo.Observe(CTCPanel.Button(id), e =>
            {
                if (!e.BoolValue) return;
                if (StateManager.IsHost) HandleInterlockingExecute(id);
                SoundHelper.PlayKlick();
            }, false));
        }

        foreach (var id in _layout.panel.Values.SelectMany(e => e).Select(e => e.Crossover?.Crossover))
        {
            if (id == null) continue;
            _observers.Add(_kvo.Observe(CTCPanel.Button(id), e =>
            {
                if (!e.BoolValue) return;
                if (StateManager.IsHost) HandleCrossoverExecute(id);
                SoundHelper.PlayKlick();
            }, false));
        }

        foreach (var block in controller.AllInterlockings.Values.SelectMany(il => il.Blocks))
        {
            _observers.Add(storage.ObserveBlockOccupancy(block.id, HandleInterlockBell, false));
        }
        
        foreach (var block in FindObjectsOfType<CTCCrossover>().SelectMany(co => co.routes).SelectMany(r => r.usedBlocks).Distinct())
        {
            _observers.Add(storage.ObserveBlockOccupancy(block.id, HandleInterlockBell, false));
        }
    }

    public void Deactivate()
    {
        foreach (var disposable in _observers)
        {
            disposable.Dispose();
        }
        _observers.Clear();
    }

    private void HandleInterlockBell(bool occupied)
    {
        if (PanelPrefs.BellSound && occupied)
            ScheduledAudioPlayer.PlaySoundLocal("ctc-bell", PanelPrefs.SoundLevel * 0.3f);
    }
    

    private void HandleCrossoverExecute(string id)
    {
        logger.Information($"Execute button clicked for crossover {id}");
        _resetButtonIds.Add(id);
        Invoke(nameof(ResetButtonIds), 0.5f);
        var co = Object.FindObjectsOfType<CTCCrossover>().FirstOrDefault(c => c.id == id);
        if (co == null || !co.isActiveAndEnabled)
        {
            logger.Information($"Crossover {id} not found");
            return;
        }
        
        var column = _layout.panel.Values.SelectMany(e => e).FirstOrDefault(e => e.Crossover?.Crossover == id);
        if (column == null)
        {
            logger.Warning($"Crossover {id} not found in crossover columns");
            return;
        }
        if (_kvo == null)
        {
            logger.Warning($"Execute button clicked for crossover {id} but KVO is not available");
            return;
        }

        List<(TrackNode, SwitchSetting)> nodes = new();
        for (int i = 0; i < co.switchSets.Count; i++)
        {
            var switchSet = co.switchSets[i];
            var knobId = column.Crossover?.SwitchKnobId(i);
            if (knobId == null)
            {
                logger.Warning($"Crossover {id} has a null switch knob id for switch set {i}");
                continue;
            }

            var switchSetting = CTCPanelKnob.SwitchSettingFromValue(_kvo[CTCKeys.Knob(knobId)]);
            foreach (var trackNode in switchSet.switchNodes)
            {
                if (trackNode == null)
                {
                    logger.Warning($"Crossover {id} has a null track node in switch set {i}");
                    continue;
                }
                nodes.Add((trackNode, switchSetting));
            }
        }

        Dictionary<string, SignalDirection> groupDirections = new();
        foreach (var group in co.signalGroups)
        {
            var signalKnob = column.Crossover?.SignalKnobId(group.groupId);
            if (signalKnob == null)
            {
                logger.Warning($"Crossover {id} has a null signal knob id for group {group.groupId}");
                continue;
            }
            groupDirections[group.groupId] = CTCPanelKnob.DirectionFromValue(_kvo[CTCKeys.Knob(signalKnob)]);
        }

        logger.Information($"Crossover {id} has {nodes.Count} nodes and {groupDirections.Count} signal groups");
        if (!co.Code(nodes, groupDirections, out var reason))
        {
            logger.Warning($"Execute button clicked for crossover {id} but code failed: {reason}");
        }
    }
    
    private void HandleInterlockingExecute(string id)
    {
        _resetButtonIds.Add(id);
        Invoke(nameof(ResetButtonIds), 0.5f);
        if (!CTCPanelController.Shared.AllInterlockings.TryGetValue(id, out var il))
        {
            logger.Warning($"Interlocking {id} not found");
            return;
        }

        var column = _layout.panel.Values.SelectMany(e => e).FirstOrDefault(e => e.Interlock?.Interlock == id);
        if (column == null)
        {
            logger.Warning($"Interlocking {id} not found in interlocking columns");
            return;
        }
        
        List<(TrackNode, SwitchSetting)> nodes = new();
        for (int i = 0; i < il.switchSets.Count(); i++)
        {
            var switchSet = il.switchSets.ElementAt(i);
            var knob = column.Interlock?.SwitchKnobId(i);
            if (knob == null)
            {
                logger.Warning($"Interlocking {id} has a null switch node");
                continue;
            }

            var switchSetting = CTCPanelKnob.SwitchSettingFromValue(_kvo[CTCKeys.Knob(knob)]);
            foreach (var trackNode in switchSet.switchNodes)
            {
                nodes.Add((trackNode, switchSetting));
            }
        }

        if (!il.Code(CTCPanelKnob.DirectionFromValue(_kvo[CTCKeys.Knob(column.Interlock.DirKnobId())]), nodes, out var codeFailureReason))
        {
            logger.Warning($"Execute button clicked for interlocking {id} but code failed: {codeFailureReason}");
        }
    }

    private void ResetButtonIds()
    {
        foreach (string resetButtonId in _resetButtonIds)
            _kvo[CTCPanel.Button(resetButtonId)] = Value.Bool(false);
        _resetButtonIds.Clear();
    }
}