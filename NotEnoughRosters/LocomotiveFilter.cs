using System.Collections.Generic;
using Game.Messages;
using Game.State;
using Model;
using Model.Ops.Timetable;
using Newtonsoft.Json;
using Serilog;
using ILogger = Serilog.ILogger;

namespace NotEnoughRosters;

public class LocomotiveFilter
{
    private readonly ILogger logger = Log.ForContext<NotEnoughRosterPanel>();

    [JsonProperty("names")] public List<string> Names { get; set; } = new();

    [JsonProperty("crews")] public List<string> Crews { get; set; } = new();

    [JsonProperty("passenger")] public bool IsPassenger { get; set; }

    [JsonProperty("skipMu")] public bool SkipMu { get; set; } = true;

    [JsonProperty("showMu")] public bool ShowMu { get; set; }

    public bool Matches(BaseLocomotive locomotive)
    {
        if (locomotive.KeyValueObject[PropertyChange.KeyForControl(PropertyChange.Control.Mu)].BoolValue)
        {
            if (ShowMu) return true;

            if (SkipMu) return false;
        }

        if (Names.Contains(locomotive.DisplayName)) return true;

        TrainCrew trainCrew;
        if (string.IsNullOrEmpty(locomotive.trainCrewId)) return false;
        if (StateManager.Shared.PlayersManager.TrainCrewForId(locomotive.trainCrewId, out trainCrew))
        {
            if (Crews.Contains(trainCrew.Name)) return true;
            if (IsPassenger && TimetableController.Shared.TryGetTrainForTrainCrew(trainCrew, out _)) return true;
        }

        return false;
    }
}