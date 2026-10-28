using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Events;
using Game.State;
using HarmonyLib;
using Newtonsoft.Json;
using NotEnoughRosters.Patches;
#if RR2026
using Railroader.Modding;
#else
using GalaSoft.MvvmLight.Messaging;
using Railloader;
#endif
using Serilog;
using UI.Builder;
using UI.EngineRoster;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;

namespace NotEnoughRosters;

#if RR2026
// Railroader 2026.1 loads the mod here (Info.json "assemblies"). There's no mod settings tab: the filter editor opens
// from the roster's "Edit Filters" button.
public sealed class ModEntryPoint : IModEntryPoint
{
    public void OnModLoad(IModContext context)
    {
        NotEnoughRosters.Shared = new NotEnoughRosters(context.ModDirectory);
        NotEnoughRosters.Shared.OnEnable();
    }

    public void OnModUnload()
    {
        NotEnoughRosters.Shared?.OnDisable();
    }
}

public class NotEnoughRosters
{
    private const string HarmonyId = "joo/not-enough-rosters";

    public static NotEnoughRosters Shared { get; internal set; } = null!;

    private readonly string _directory;

    private readonly IDisposable _mapDidLoad;
#else
public class NotEnoughRosters : SingletonPluginBase<NotEnoughRosters>, IModTabHandler
{
    private readonly IModDefinition _modDefinition;

    private string _directory => _modDefinition.Directory;

    private string HarmonyId => _modDefinition.Id;
#endif

    private readonly ILogger logger = Log.ForContext<NotEnoughRosters>();

    private Dictionary<string, LocomotiveFilter> _rosterFilters = new();
    private UIState<string?> _selectedFilterState = new(null);

#if RR2026
    public NotEnoughRosters(string directory)
    {
        _directory = directory;

        _rosterFilters = GetRoasters(Path.Combine(_directory, "trains.json"));
        _mapDidLoad = EventBus.Subscribe<MapDidLoadEvent>(ml => CreateWindows());
    }
#else
    public NotEnoughRosters(IModdingContext moddingContext, IModDefinition self)
    {
        _modDefinition = self;

        _rosterFilters = GetRoasters(Path.Combine(_directory, "trains.json"));
        Messenger.Default.Register<MapDidLoadEvent>(this, ml => CreateWindows());
    }
#endif

    private void CreateWindows()
    {
        NotEnoughRosterPanel.CreateInstance(_rosterFilters);
        NotEnoughRostersSettingsWindow.CreateInstance();
    }

    // The filter editor lives in its own window: a list/detail view collapses when a host
    // (e.g. RailForge) wraps the mod tab in a scroll view, so the tab only links to it.
    public void ModTabDidOpen(UIPanelBuilder builder)
    {
        builder.HStack(h =>
        {
            h.AddButton("Edit Filters", () => NotEnoughRostersSettingsWindow.Shared?.Show());
            h.AddButton("Toggle Original Roster", ToggleOriginalRoster);
            h.Spacer();
        });
        if (NotEnoughRostersSettingsWindow.Shared == null)
            builder.AddLabel("Load a game to edit the roster filters.");
    }

    internal static void ToggleOriginalRoster()
    {
        EngineRosterPanel_Toggle_Patch.DisablePatch = true;
        EngineRosterPanel.Toggle();
        EngineRosterPanel_Toggle_Patch.DisablePatch = false;
    }

    internal void BuildFilterEditor(UIPanelBuilder builder, Action rebuildAll)
    {
        builder.HStack(h =>
        {
            h.AddButton("Toggle Original Roster", ToggleOriginalRoster);
            h.Spacer();
            h.AddButton("Add Filter", () =>
            {
                var newName = "New Filter " + (_rosterFilters.Count + 1);
                _rosterFilters[newName] = new LocomotiveFilter();
                _selectedFilterState.Value = newName;
                SaveRoasters();
                rebuildAll.Invoke();
            });
        });

        var listItems = _rosterFilters.Select(kvp => new UIPanelBuilder.ListItem<LocomotiveFilter>(kvp.Key, kvp.Value, "Filters", kvp.Key)).ToList();

        builder.AddListDetail(listItems, _selectedFilterState!, (pb, filter) =>
        {
            var key = _selectedFilterState.Value;
            if (key == null || !_rosterFilters.TryGetValue(key, out var currentFilter) || currentFilter != filter)
            {
                pb.AddExpandingVerticalSpacer();
                pb.AddLabel("Select a filter to edit");
                pb.AddExpandingVerticalSpacer();
                return;
            }
            pb.VScrollView(detailBuilder => PrintSettings(detailBuilder, key, filter, rebuildAll));
        }, 200f);
    }

    private void PrintSettings(UIPanelBuilder builder, string key, LocomotiveFilter filter, Action rebuildAll)
    {
        builder.Spacer(8f);
        builder.HStack(h =>
        {
            h.AddLabel("Name:");
            h.AddInputField(key, newKey =>
            {
                if (string.IsNullOrEmpty(newKey) || newKey == key || _rosterFilters.ContainsKey(newKey)) return;
                SetFilterOrder(_rosterFilters.Select(kvp =>
                    kvp.Key == key ? new KeyValuePair<string, LocomotiveFilter>(newKey, filter) : kvp));
                _selectedFilterState.Value = newKey;
                rebuildAll.Invoke();
            }).Width(200f);
            h.Spacer();
            h.AddButtonCompact("Move Up", () =>
            {
                MoveFilter(key, -1);
                rebuildAll.Invoke();
            }).Tooltip("Move Up", "Show this section earlier in the roster window");
            h.AddButtonCompact("Move Down", () =>
            {
                MoveFilter(key, 1);
                rebuildAll.Invoke();
            }).Tooltip("Move Down", "Show this section later in the roster window");
            h.Spacer(8f);
            h.AddButtonCompact("Remove Filter", () =>
            {
                SetFilterOrder(_rosterFilters.Where(kvp => kvp.Key != key));
                _selectedFilterState.Value = _rosterFilters.Keys.FirstOrDefault();
                rebuildAll.Invoke();
            });
        }).Height(30f);
        builder.Spacer(8f);

        builder.HStack(hs =>
        {
            hs.AddLabel("Passenger").Tooltip("Always show passenger locomotives", "Enable this option to always show passenger trains in this section.");
            hs.AddToggle(() => filter?.IsPassenger ?? false, v =>
            {
                filter.IsPassenger = v;
                SaveRoasters();
            });
            hs.Spacer(20f);
            hs.AddLabel("Skip MU").Tooltip("Skip MU locomotives", "Enable this option to skip MU trains in this section.");
            hs.AddToggle(() => filter?.SkipMu ?? false, v =>
            {
                filter.SkipMu = v;
                SaveRoasters();
            });
            hs.Spacer(20f);
            hs.AddLabel("Show MU").Tooltip("Show MU locomotives", "Enable this option to show MU trains in this section.");
            hs.AddToggle(() => filter?.ShowMu ?? false, v =>
            {
                filter.ShowMu = v;
                SaveRoasters();
            });
            hs.Spacer();
        });

        builder.Spacer(12f);
        builder.AddSection(null, ns =>
        {
            ns.AddLabelMarkup("<style=H2>Visible Locomotives:</style>").Height(30f);
            ns.Spacer(4f);
            foreach (var name in filter.Names.ToList())
            {
                ns.HStack(h =>
                {
                    h.AddButtonCompact("-", () =>
                    {
                        filter.Names.Remove(name);
                        SaveRoasters();
                        builder.Rebuild();
                    }).Tooltip("Delete entry", "Remove this locomotive from the list");
                    h.Spacer(8f);
                    h.AddLabel(name);
                }).Height(24f);
            }

            var names = TrainController.Shared.Cars.Where(c => c.IsLocomotive).Select(c => c.DisplayName).Where(n => !filter.Names.Contains(n)).ToList();
            names.Sort();
            var dropdownOptions = new List<string> { "Add existing Locomotive" };
            dropdownOptions.AddRange(names);

            ns.Spacer(4f);
            ns.AddDropdown(dropdownOptions, 0,
                s =>
                {
                    if (s == 0) return;
                    filter.Names.Add(names[s - 1]);
                    SaveRoasters();
                    builder.Rebuild();
                });
            ns.Spacer(4f);
            ns.AddInputField("", newName =>
            {
                if (string.IsNullOrEmpty(newName)) return;
                filter.Names.Add(newName);
                SaveRoasters();
                builder.Rebuild();
            }, "+ Add Custom Name").Height(30f);
        });

        builder.Spacer(12f);
        builder.AddSection(null, cs =>
        {
            cs.AddLabelMarkup("<style=H2>Visible Crews:</style>").Height(30f);
            cs.Spacer(4f);
            foreach (var crew in filter.Crews.ToList())
            {
                cs.HStack(h =>
                {
                    h.AddButtonCompact("-", () =>
                    {
                        filter.Crews.Remove(crew);
                        SaveRoasters();
                        builder.Rebuild();
                    }).Tooltip("Delete entry", "Remove this crew from the list");
                    h.Spacer(8f);
                    h.AddLabel(crew);
                }).Height(24f);
            }

            var names = StateManager.Shared.PlayersManager.TrainCrews.Select(c => c.Name).Where(c => !filter.Crews.Contains(c)).ToList();
            names.Sort();
            var dropdownOptions = new List<string> { "Add existing Crew" };
            dropdownOptions.AddRange(names);

            cs.Spacer(4f);
            cs.AddDropdown(dropdownOptions, 0,
                s =>
                {
                    if (s == 0) return;
                    filter.Crews.Add(names[s - 1]);
                    SaveRoasters();
                    builder.Rebuild();
                });
            cs.Spacer(4f);
            cs.AddInputField("", newCrew =>
            {
                if (string.IsNullOrEmpty(newCrew)) return;
                filter.Crews.Add(newCrew);
                SaveRoasters();
                builder.Rebuild();
            }, "+ Add Custom Crew").Height(30f);
        });
    }

    public void ModTabDidClose()
    {
        SaveRoasters();
    }

#if RR2026
    public void OnEnable()
#else
    public override void OnEnable()
#endif
    {
        var harmony = new Harmony(HarmonyId);
        harmony.PatchCategory("NotEnoughRosters");
    }

#if RR2026
    public void OnDisable()
#else
    public override void OnDisable()
#endif
    {
        var harmony = new Harmony(HarmonyId);
        harmony.UnpatchCategory("NotEnoughRosters");
    }

    // The roster panel shows sections in filter order, so rebuild the shared dictionary in place:
    // Dictionary.Remove + add would reuse the freed slot and shuffle the order.
    private void SetFilterOrder(IEnumerable<KeyValuePair<string, LocomotiveFilter>> entries)
    {
        var ordered = entries.ToList();
        _rosterFilters.Clear();
        foreach (var kvp in ordered)
            _rosterFilters[kvp.Key] = kvp.Value;
        SaveRoasters();
    }

    private void MoveFilter(string key, int offset)
    {
        var entries = _rosterFilters.ToList();
        var index = entries.FindIndex(kvp => kvp.Key == key);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= entries.Count) return;
        (entries[index], entries[target]) = (entries[target], entries[index]);
        SetFilterOrder(entries);
    }

    private void SaveRoasters()
    {
        var file = Path.Combine(_directory, "trains.json");
        var json = JsonConvert.SerializeObject(_rosterFilters, Formatting.Indented);
        File.WriteAllText(file, json);
    }

    private Dictionary<string, LocomotiveFilter> GetRoasters(string file)
    {
        if (!File.Exists(file)) return new Dictionary<string, LocomotiveFilter>();
        var json = File.ReadAllText(file);
        var result = JsonConvert.DeserializeObject<Dictionary<string, LocomotiveFilter>>(json);
        if (result == null)
        {
            logger.Warning("No preset defined.");
            return new Dictionary<string, LocomotiveFilter>();
        }

        return result;
    }
}