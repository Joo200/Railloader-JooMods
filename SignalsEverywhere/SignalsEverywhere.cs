using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Railloader;
using Serilog;
using GalaSoft.MvvmLight.Messaging;
using Game;
using Game.Events;
using HarmonyLib;
using Network.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalsEverywhere.Panel;
using SignalsEverywhere.Patching;
using SignalsEverywhere.Signals;
using StrangeCustoms;
using Track;
using Track.Signals;
using UI;
using UI.Builder;
using UI.Common;
using UnityEngine;
using Object = UnityEngine.Object;
using Timer = System.Threading.Timer;

namespace SignalsEverywhere;

public class SignalsEverywhere : SingletonPluginBase<SignalsEverywhere>, IModTabHandler
{
    Serilog.ILogger logger = Log.ForContext<SignalsEverywhere>();

    private readonly Timer syncTimer;
    
    private CTCKeyValueObserver? _ctcObserver;
    
    private readonly IModdingContext _moddingContext;
    private readonly IModDefinition _modDefinition;
    
    private readonly SignalCreator _signalCreator;

    public string ModDirectory => _modDefinition.Directory;

    public CTCPanelLayout? PanelLayout { get => field ?? LoadPanelLayout(); private set; }
    
    public SignalsEverywhere(IModdingContext moddingContext, IModDefinition self)
    {
        Messenger.Default.Send(this);
        
        logger.Information("Started");
        _moddingContext = moddingContext;
        _modDefinition = self;
        _signalCreator = new SignalCreator(this);
        
        moddingContext.RegisterConsoleCommand(new DebugCommand(_signalCreator));
        
        Messenger.Default.Register<GraphDidChangeEvent>(this, _ => RegisterSignals());
        Messenger.Default.Register<CTCFeatureChange>(this, _ => CTCPanel.Shared?.TryRebuild());
        Messenger.Default.Register<ProgressionStateDidChange>(this, _ => CTCPanel.Shared?.TryRebuild());
    }

    private CTCPanelLayout? LoadPanelLayout()
    {
        PatchHelper patchHelper = GetMixintoJson("ctcPanel", null);
        try
        {
            var result = patchHelper.Value.ToObject<CTCPanelLayout>();
            result.Squash();
            PanelLayout = result;
            return result;
        }
        catch (Newtonsoft.Json.JsonSerializationException e)
        {
            logger.Error(e, "Error loading CTC Panel layout, dump json file");
            var path = Path.Combine(ModDirectory, "ctcpanel-failed.json");
            using (StreamWriter streamWriter = new StreamWriter(path))
                using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
                    _signalCreator.Serializer.Serialize(jsonWriter, patchHelper.Value);
            return null;
        }
    }

    private void RebuildCtcPanel()
    {
        if (CTCPanel.Shared == null)
            InitializeCtcPanel();
        else
            CTCPanel.Shared?.TryRebuild();
    }
    
    private bool _loaded = false;
    public void RegisterSignals()
    {
        CTCPanelController instance = Object.FindAnyObjectByType<CTCPanelController>(FindObjectsInactive.Include);
        if (instance == null)
        {
            logger.Warning("Couldn't find CTC Panel");
            return;
        }
        logger.Information("Patching signals");
        _signalCreator.CreateSignals(GetMixintoJson("signals", null), instance);
        _loaded = true;
    }

    public void BuildSignals(CTCPanelController instance)
    {
        if (!_loaded)
        {
            _signalCreator.CreateSignals(GetMixintoJson("signals", null), instance);
            _loaded = true;
        }
        
        try
        {
            _signalCreator.BuildSignals(instance);
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Error building signals: {ex.Message}");
        }

        try
        {
            RebuildCtcPanel();
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Error rebuilding CTC panel: {ex.Message}");
        }
        
        logger.Information("Adding CTC KeyValueObserver to observe custom buttons");
        if (instance.GetComponent<CTCKeyValueObserver>() == null)
            instance.gameObject.AddComponent<CTCKeyValueObserver>();
    }
    
    private void InitializeCtcPanel()
    {
        try
        {
            CTCPanel.CreateInstance();
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Error loading CTC config: {ex.Message}");
        }
    }

    public override void OnEnable()
    {
        var harmony = new Harmony(_modDefinition.Id);
        harmony.PatchCategory("SignalsEverywhere");
    }

    public override void OnDisable()
    {
        var harmony = new Harmony(_modDefinition.Id);
        harmony.UnpatchAll("SignalsEverywhere");
    }

    public IEnumerable<ModMixinto> GetMixintos(string identifier)
    {
        var basedir = _moddingContext.ModsBaseDirectory;
        foreach (var mixinto in _moddingContext.GetMixintos(identifier))
        {
            var path = Path.GetFullPath(mixinto.Mixinto);
            if (!path.StartsWith(basedir))
            {
                logger.Warning($"Mixinto {mixinto.Mixinto} is not in the mods directory.");
            }
            yield return mixinto;
        }
    }

    public PatchHelper GetMixintoJson(string identifier, JObject? baseJson)
    {
        if (baseJson == null) baseJson = new JObject();
        PatchHelper patchHelper = new(baseJson);
        var basedir = _moddingContext.ModsBaseDirectory;
        foreach (var mixinto in _moddingContext.GetMixintos(identifier))
        {
            var path = Path.GetFullPath(mixinto.Mixinto);
            if (!path.StartsWith(basedir))
            {
                logger.Warning($"Mixinto {mixinto.Mixinto} is not in the mods directory.");
            }
            var json = JObject.Parse(File.ReadAllText(mixinto.Mixinto));
            json.Remove("$schema");
            patchHelper.ApplyPatch(mixinto.Mixinto, json);
        }

        return patchHelper;
    }

    
    private readonly List<string> _segmentTokens = new();
    
    public void ModTabDidOpen(UIPanelBuilder builder)
    {
        if (CTCPanelController.Shared == null)
        {
            builder.AddSection("CTC is not enabled", s =>
            {
                builder.AddLabel("The CTC Feature is not enabled. There is nothing to see here.");
                builder.AddLabel("Buy the progressions for signals (Company mode) or enable the CTC MapFeature (Sandbox mode) first.");
            });
            return;
        }
        
        builder.AddSection("CTCPanel", sec =>
        {
            sec.AddButton("Rebuild CTCPanel", () =>
            {
                LoadPanelLayout();
                InitializeCtcPanel();
                Console.Log("CTCPanel reloaded");
            });

            sec.AddButton("Dump CTCPanel", () =>
            {
                var result = DebugCommand.DumpPanel(_signalCreator);
                Alert alert = new Alert(AlertStyle.Toast, AlertLevel.Info, result, TimeWeather.Now.TotalSeconds);
                WindowManager.Shared.Present(alert);
            });
        });
        builder.AddSection("Signals", sec =>
        {
            sec.AddButton("Reload Signal Definitions", () =>
            {
                try
                {
                    RegisterSignals();
                    BuildSignals(CTCPanelController.Shared);
                    Console.Log("Signals reloaded");
                }
                catch (Exception e)
                {
                    logger.Error(e, "Error reloading signals");
                    Alert alert = new Alert(AlertStyle.Toast, AlertLevel.Error, "Failed to reload signals. Check log for details", TimeWeather.Now.TotalSeconds);
                    WindowManager.Shared.Present(alert);
                }
            });

            sec.AddButton("Dump Signals", () =>
            {
                var result = _signalCreator.DumpData();
                Alert alert = new Alert(AlertStyle.Toast, AlertLevel.Info, result, TimeWeather.Now.TotalSeconds);
                WindowManager.Shared.Present(alert);
            });
        });
        
        IReadOnlyDictionary<string, CTCBlock> storedBlocks = CTCPanelController.Shared.AllBlocks;
        builder.AddSection("Blocks", sec =>
        {
            sec.AddDropdown(storedBlocks.Keys.ToList(), 0, selected =>
            {
                _segmentTokens.ForEach(SegmentIndicatorController.Shared.Remove);
                _segmentTokens.Clear();
                var block = storedBlocks.ElementAt(selected).Value;
                _segmentTokens.Add(SegmentIndicatorController.Shared.Add(block.GetComponentsInChildren<TrackSpan>().Select(s => s.id)));
            });
        });
    }

    public void ModTabDidClose()
    {
        _segmentTokens.ForEach(SegmentIndicatorController.Shared.Remove);
        _segmentTokens.Clear();
    }
}
