using System;
using System.IO;
using System.Linq;
using Audio;
using Game.State;
using Helpers;
using Model.Ops;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Serilog;
using SignalsEverywhere.Patching;
using SignalsEverywhere.Signals;
using Track;
using Track.Signals;
using UI.Console;
using Object = UnityEngine.Object;

namespace SignalsEverywhere;

[ConsoleCommand("/signaldebug", "Sync the time to all clients")]
public class DebugCommand(SignalCreator signalCreator) : IConsoleCommand
{
    public string Execute(string[] components)
    {
        if (!Multiplayer.IsHost)
        {
            return "Only host can do this";
        }

        if (components.Length < 3)
        {
            return "Usage: /signaldebug <dump> <signals|panel>";
        }

        if (components[1] == "dump" && components[2] == "signals")
        {
            if (signalCreator.OriginalData == null)
                return "No signals loaded. Check the logs for errors.";
            var path = Path.Combine(SignalsEverywhere.Shared.ModDirectory, "signal-old.json");
            using (StreamWriter streamWriter = new StreamWriter(path))
                using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
                    signalCreator.Serializer.Serialize(jsonWriter, signalCreator.OriginalData);

            if (signalCreator.PatchedData == null)
                return "Dumped original data. Patched data contains errors.";
            
            path = Path.Combine(SignalsEverywhere.Shared.ModDirectory, "signal-patched.json");
            using (StreamWriter streamWriter = new StreamWriter(path))
                using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
                    signalCreator.Serializer.Serialize(jsonWriter, signalCreator.PatchedData);
            
            return "Successfully dumped signals to mod directory";
        }

        if (components[1] == "dump" && components[2] == "panel")
        {
            var dumped = SignalsEverywhere.Shared.PanelLayout;
            if (dumped == null)
                return "CTC Panel Layout is invalid. Unable to dump.";
            
            var deserialized = JObject.FromObject(dumped, signalCreator.Serializer);
            var path = Path.Combine(SignalsEverywhere.Shared.ModDirectory, "panel-dump.json");
            using (StreamWriter streamWriter = new StreamWriter(path))
                using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
                    signalCreator.Serializer.Serialize(jsonWriter, signalCreator.PatchedData);
            return "Successfully dumped panel to mod directory";
        }

        if (components[1] == "distances")
        {
            var stops = Object.FindObjectsOfType<PassengerStop>();
            foreach (var a in stops)
            {
                PrefabTypeAnalyzer.AnalyzeFromMonoBehaviour(a);
                
                foreach (var b in stops)
                {
                    if (a == b) continue;
                    try
                    {
                        if (a.ProgressionDisabled || b.ProgressionDisabled)
                            Log.Warning($"{a.timetableCode} -> {b.timetableCode}: Disabled");
                        else if (!a.TrackSpans.Any() || !b.TrackSpans.Any()) 
                            Log.Warning($"{a.timetableCode} -> {b.timetableCode}: No track spans");
                        else if (PassengerStop.TryCalculateMilesBetweenPassengerStops(a.identifier, b.identifier, out var info))
                            Log.Information($"{a.timetableCode} -> {b.timetableCode}: {info.Success}, {info.DistanceInMiles}, {info.TraverseTimeSeconds} ");
                        else
                            Log.Warning($"{a.timetableCode} -> {b.timetableCode}: No route found");
                    } catch (Exception e)
                    {
                        Log.Error(e, $"{a.timetableCode} -> {b.timetableCode}: Exception");
                    }
                }
            }
        }
        return "see log";
    }
}