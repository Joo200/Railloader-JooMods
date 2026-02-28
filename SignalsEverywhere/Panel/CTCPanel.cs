using System;
using System.Collections.Generic;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Game.Progression;
using KeyValue.Runtime;
using Serilog;
using Track.Signals;
using UI.Builder;
using UI.Common;
using UnityEngine;

namespace SignalsEverywhere.Panel;

public class CTCPanel : WindowBase
{
    Serilog.ILogger logger = Log.ForContext<CTCPanel>();
    
    public override string WindowIdentifier => "CTCPanel";
    public override string Title => "CTC Panel";
    public override Vector2Int DefaultSize => new(800, 200);
    public override Window.Position DefaultPosition => Window.Position.UpperRight;
    public override Window.Sizing Sizing => Window.Sizing.Resizable(DefaultSize);

    public static string Button(string buttonId) => $"sebutton:{buttonId}:active";
    public static CTCPanel? Shared { get; private set; }

    private Dictionary<string, Schematic> _schematics = new();
    private static SignalStorage? _storage => CTCPanelController.Shared?.GetComponentInParent<SignalStorage>();
    
    private static CTCPanelLayout _layout => SignalsEverywhere.Shared.PanelLayout;
    public List<string> Branches => _layout.panel.Keys.ToList();
    
    private IDisposable? _observer;

    public float Scale
    {
        get => PlayerPrefs.GetFloat("SignalsEverywhere.CTCPanel.Scale", 1.0f);
        set
        {
            PlayerPrefs.SetFloat("SignalsEverywhere.CTCPanel.Scale", value);
            TryRebuild();
        }
    }
    
    public static void CreateInstance()
    {
        if (Shared != null)
        {
            Shared.TryRebuild();
            return;
        }
        WindowHelper.CreateWindow<CTCPanel>();
        Shared = WindowManager.Shared.GetWindow<CTCPanel>();
        Shared._observer = _storage?.ObserveSystemMode(v => Shared?.TryRebuild());
    }

    public void TryRebuild()
    {
        if (Window.IsShown) Rebuild();
    }

    private void OnDisable()
    {
        _observer?.Dispose();
        foreach (var schematic in _schematics.Values)
        {
            if (schematic != null)
                Destroy(schematic.gameObject);
        }
        _schematics.Clear();
    }

    public void Toggle()
    {
        if (Window.IsShown)
            Close();
        else
            Show();
    }

    public void Close()
    {
        Window.CloseWindow();

        Messenger.Default.Unregister(this);
    }

    public void Show()
    {
        var rect = GetComponent<RectTransform>();
        rect.position = new Vector2(Screen.width, Screen.height - 40);
        Window.ShowWindow();
        Rebuild();
    }
    
    public override void Populate(UIPanelBuilder builder)
    {
        if (_layout == null)
        {
            logger.Error("CTCPanelLayout not set");
            return;
        };
        logger.Information("Rebuilding CTC Panel");

        bool activated = MapFeatureManager.Shared.AvailableFeatures.Any(s => s.identifier == "signals" && s.Unlocked);
        if (!activated || CTCPanelController.Shared == null)
        {
            builder.HStack(b => b.AddTitle("CTC Panel requires the Signals Map Feature to be unlocked.", "Please buy the CTC progression first."));
            return;
        }

        builder.HStack(b =>
        {
            b.AddLabel("Scale:");
            b.AddButtonCompact("-", () => Scale = Mathf.Max(0.5f, Scale - 0.1f));
            b.AddLabel($"{Scale:F1}");
            b.AddButtonCompact("+", () => Scale = Mathf.Min(2.0f, Scale + 0.1f));
            b.Spacer();

            b.AddLabel("Activate CTC:");
            b.AddToggle(() => _storage.SystemMode == SystemMode.CTC,
                v => _storage.SystemMode = v ? SystemMode.CTC : SystemMode.ABS);
            
            b.Spacer();
            b.AddLabel("Highlight Schematic:");
            b.AddToggle(() => PanelPrefs.Highlight, v => PanelPrefs.Highlight = v);
            b.Spacer();

            b.AddLabel("Volume:");
            b.AddButtonCompact("-", () => PanelPrefs.SoundLevel = Mathf.Max(0.1f, PanelPrefs.SoundLevel - 0.1f));
            b.AddLabel(() => $"{PanelPrefs.SoundLevel:F1}", UIPanelBuilder.Frequency.Fast);
            b.AddButtonCompact("+", () => PanelPrefs.SoundLevel = Mathf.Min(1.0f, PanelPrefs.SoundLevel + 0.1f));
            b.AddLabel("Ring Bell:");
            b.AddToggle(() => PanelPrefs.BellSound, v => PanelPrefs.BellSound = v);
            b.AddLabel("Click Knobs:");
            b.AddToggle(() => PanelPrefs.ConfigSound, v => PanelPrefs.ConfigSound = v);
        });
        
        foreach (var keyValuePair in _layout.panel)
        {
            if (!_schematics.TryGetValue(keyValuePair.Key, out var schematic))
            {
                var go = new GameObject($"Schematic_{keyValuePair.Key}");
                schematic = go.AddComponent<Schematic>();
                schematic.Initialize(keyValuePair.Key, keyValuePair.Value);
                _schematics[keyValuePair.Key] = schematic;
            }
            else
            {
                schematic.Elements = keyValuePair.Value;
            }

            builder.AddSection(keyValuePair.Key, b => { BuildPanelSection(b, schematic); });
        }
        logger.Information("Built CTC Panel");
    }

    public void BuildPanelSection(UIPanelBuilder builder, Schematic schematic)
    {
        var c = builder.HScrollView(s => s.VStack(b => schematic.Build(b, Scale)));
    }

}
