using System.Collections.Generic;
using System.IO;
using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using HarmonyLib;
using Newtonsoft.Json;
using NotEnoughRosters.Patches;
using Railloader;
using Serilog;
using UI.Builder;
using UI.EngineRoster;
using ILogger = Serilog.ILogger;

namespace NotEnoughRosters;

public class NotEnoughRosters : SingletonPluginBase<NotEnoughRosters>, IModTabHandler
{
    private readonly ILogger logger = Log.ForContext<NotEnoughRosters>();

    private readonly IModDefinition _modDefinition;

    public NotEnoughRosters(IModdingContext moddingContext, IModDefinition self)
    {
        logger.Information("Starting NotEnoughRosters");
        _modDefinition = self;

        var file = Path.Combine(self.Directory, "trains.json");
        Messenger.Default.Register<MapDidLoadEvent>(this,
            ml => { NotEnoughRosterPanel.CreateInstance(GetRoasters(file)); });


        logger.Information("Finished NotEnoughRosters");
    }


    public void ModTabDidOpen(UIPanelBuilder builder)
    {
        builder.AddButton("Toggle Original Roster", () =>
        {
            EngineRosterPanel_Toggle_Patch.DisablePatch = true;
            EngineRosterPanel.Toggle();
            EngineRosterPanel_Toggle_Patch.DisablePatch = false;
        });
    }

    public void ModTabDidClose()
    {
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

    private Dictionary<string, LocomotiveFilter> GetRoasters(string file)
    {
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