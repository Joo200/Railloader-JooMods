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
using UnityEngine.UI;

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
    private HashSet<string> _collapsedSections = new();
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
    }

    public void TryRebuild()
    {
        // Shared outlives the panel when another game is loaded: a destroyed panel passes the callers' `?.` (a C# null
        // check, not Unity's), so check here before touching its window.
        if (this == null) return;
        var window = Window;
        if (window != null && window.IsShown) Rebuild();
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
        Window.ShowWindow();
        Rebuild();
    }

    public override void Populate(UIPanelBuilder builder)
    {
        if (_layout == null)
        {
            logger.Error("CTCPanelLayout not set");
            return;
        }

        ;
        logger.Information("Rebuilding CTC Panel");

        if (_observer == null)
            _observer = _storage?.GetComponent<KeyValueObject>().Observe("mode", _ => TryRebuild(), false);

        bool activated = MapFeatureManager.Shared.AvailableFeatures.Any(s => s.identifier == "signals" && s.Unlocked);
        if (!activated || CTCPanelController.Shared == null)
        {
            builder.HStack(b => b.AddTitle("CTC Panel requires the Signals Map Feature to be unlocked.",
                "Please buy the CTC progression first."));
            return;
        }

        builder.HStack(b =>
        {
            NoWrap(b.AddLabel("Scale:"));
            b.AddButtonCompact("-", () => Scale = Mathf.Max(0.5f, Scale - 0.1f));
            NoWrap(b.AddLabel($"{Scale:F1}"));
            b.AddButtonCompact("+", () => Scale = Mathf.Min(2.0f, Scale + 0.1f));
            b.Spacer();

            NoWrap(b.AddLabel("Activate CTC:"));
            b.AddToggle(() => _storage.SystemMode == SystemMode.CTC,
                v => _storage.SystemMode = v ? SystemMode.CTC : SystemMode.ABS);

            b.Spacer();
            NoWrap(b.AddLabel("Highlight Schematic:"));
            b.AddToggle(() => PanelPrefs.Highlight, v => PanelPrefs.Highlight = v);
            b.Spacer();

            NoWrap(b.AddLabel("Volume:"));
            b.AddButtonCompact("-", () => PanelPrefs.SoundLevel = Mathf.Max(0.1f, PanelPrefs.SoundLevel - 0.1f));
            NoWrap(b.AddLabel(() => $"{PanelPrefs.SoundLevel:F1}", UIPanelBuilder.Frequency.Fast));
            b.AddButtonCompact("+", () => PanelPrefs.SoundLevel = Mathf.Min(1.0f, PanelPrefs.SoundLevel + 0.1f));
            NoWrap(b.AddLabel("Ring Bell:"));
            b.AddToggle(() => PanelPrefs.BellSound, v => PanelPrefs.BellSound = v);
            NoWrap(b.AddLabel("Click Knobs:"));
            b.AddToggle(() => PanelPrefs.ConfigSound, v => PanelPrefs.ConfigSound = v);
        });
        builder.VScrollView(BuildAllSections);
    }

    // Railroader 2026.1's rows can squeeze a label narrower than its text ("Volum / e:"): header labels keep one line.
    private static void NoWrap(RectTransform label)
    {
#if RR2026
        label.TextWrap(TMPro.TextOverflowModes.Overflow, TMPro.TextWrappingModes.NoWrap);
#endif
    }

    private void BuildAllSections(UIPanelBuilder builder) {
        foreach (var keyValuePair in _layout.panel)
        {
            var sectionKey = keyValuePair.Key;
            if (!_schematics.TryGetValue(sectionKey, out var schematic))
            {
                var go = new GameObject($"Schematic_{sectionKey}");
                schematic = go.AddComponent<Schematic>();
                schematic.Initialize(sectionKey, keyValuePair.Value);
                _schematics[sectionKey] = schematic;
            }
            else
            {
                schematic.Elements = keyValuePair.Value;
            }

            bool collapsed = _collapsedSections.Contains(sectionKey);
            builder.HStack(h =>
            {
                h.AddLabel(sectionKey);
                h.AddButtonCompact(collapsed ? "+" : "-", () =>
                {
                    if (collapsed) _collapsedSections.Remove(sectionKey);
                    else _collapsedSections.Add(sectionKey);
                    Rebuild();
                });
            });

            if (!collapsed)
            {
                builder.VStack(b => { BuildPanelSection(b, schematic); });
            }
        }
        logger.Information("Built CTC Panel");
    }

    public void BuildPanelSection(UIPanelBuilder builder, Schematic schematic)
    {
        float scaledGridCellSize = ViewCreator.GridCellSize * Scale;
        float totalCellsY = schematic.MaxY - schematic.MinY + 1;
        float schematicHeight = totalCellsY * scaledGridCellSize;
        float controlRowHeight = ViewCreator.FixedControlRowHeight;
        float totalHeight = schematicHeight + (CTCPanelController.Shared.SystemMode == SystemMode.CTC ? controlRowHeight : 0) + 64f;
        
        var h = builder.HScrollView(s =>
        {
            s.VStack(b => schematic.Build(b, Scale));
        });
        
        var layout = h.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.flexibleHeight = 0;
            layout.preferredHeight = totalHeight;
            layout.minHeight = totalHeight;
        }
    }

}
