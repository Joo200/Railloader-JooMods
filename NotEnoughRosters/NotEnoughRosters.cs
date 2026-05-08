using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using Game.State;
using HarmonyLib;
using Newtonsoft.Json;
using NotEnoughRosters.Patches;
using Railloader;
using Serilog;
using UI.Builder;
using UI.EngineRoster;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;

namespace NotEnoughRosters;

public class NotEnoughRosters : SingletonPluginBase<NotEnoughRosters>, IModTabHandler
{
    private readonly ILogger logger = Log.ForContext<NotEnoughRosters>();

    private readonly IModDefinition _modDefinition;

    private Dictionary<string, LocomotiveFilter> _rosterFilters = new();
    private UIState<string?> _selectedFilterState = new(null);

    public NotEnoughRosters(IModdingContext moddingContext, IModDefinition self)
    {
        _modDefinition = self;

        var file = Path.Combine(self.Directory, "trains.json");
        _rosterFilters = GetRoasters(file);
        Messenger.Default.Register<MapDidLoadEvent>(this,
            ml => { NotEnoughRosterPanel.CreateInstance(_rosterFilters); });
    }


    public void ModTabDidOpen(UIPanelBuilder builder)
    {
        builder.HStack(h =>
        {
            h.AddButton("Toggle Original Roster", () =>
            {
                EngineRosterPanel_Toggle_Patch.DisablePatch = true;
                EngineRosterPanel.Toggle();
                EngineRosterPanel_Toggle_Patch.DisablePatch = false;
            });
            h.Spacer();
            h.AddButton("Add Filter", () =>
            {
                var newName = "New Filter " + (_rosterFilters.Count + 1);
                _rosterFilters[newName] = new LocomotiveFilter();
                _selectedFilterState.Value = newName;
                SaveRoasters();
                builder.Rebuild();
            });
        });

        var listItems = _rosterFilters.Select(kvp => new UIPanelBuilder.ListItem<LocomotiveFilter>(kvp.Key, kvp.Value, "Filters", kvp.Key)).ToList();

        var outerHStack = builder.HStack(h =>
        {
            h.AddListDetail(listItems, _selectedFilterState!, (pb, filter) =>
            {
                var key = _selectedFilterState.Value;
                if (key == null || !_rosterFilters.TryGetValue(key, out var currentFilter) || currentFilter != filter)
                {
                    pb.AddLabel("Select a filter to edit");
                    return;
                }
                pb.VStack(detailBuilder =>
                {
                    PrintSettings(detailBuilder, key, filter, () => builder.Rebuild());
                    detailBuilder.AddExpandingVerticalSpacer();
                });
            });
        });

        var layout = outerHStack.GetComponent<LayoutElement>();
        if (layout != null)
        {
            layout.preferredHeight = 600;
            layout.flexibleHeight = 0;
        }
    }

    private void PrintSettings(UIPanelBuilder builder, string key, LocomotiveFilter filter, Action rebuildAll)
    {
        builder.Spacer(8f);
        builder.HStack(h =>
        {
            h.AddLabel("Name:");
            h.AddInputField(key, newKey =>
            {
                if (string.IsNullOrEmpty(newKey) || newKey == key) return;
                _rosterFilters.Remove(key);
                _rosterFilters[newKey] = filter;
                _selectedFilterState.Value = newKey;
                SaveRoasters();
                rebuildAll.Invoke();
            }).Width(200f);
            h.Spacer();
            h.AddButtonCompact("Remove Filter", () =>
            {
                _rosterFilters.Remove(key);
                _selectedFilterState.Value = _rosterFilters.Keys.FirstOrDefault();
                SaveRoasters();
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

    public override void OnEnable()
    {
        var harmony = new Harmony(_modDefinition.Id);
        harmony.PatchCategory("NotEnoughRosters");
    }

    public override void OnDisable()
    {
        var harmony = new Harmony(_modDefinition.Id);
        harmony.UnpatchCategory("NotEnoughRosters");
    }

    private void SaveRoasters()
    {
        var file = Path.Combine(_modDefinition.Directory, "trains.json");
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