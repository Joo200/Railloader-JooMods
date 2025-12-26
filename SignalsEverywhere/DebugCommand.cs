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
            return "Usage: /signaldebug <interlock|signal> <id>";
        }

        if (components[1] == "dump" && components[2] == "signals")
        {
            var dumped = signalCreator.Deserialize();

            if (dumped == null)
            {
                return "Failed to deserialize signals";
            }

            var serializer = new JsonSerializer();
            serializer.Converters.Add(new StringEnumConverter());
            var deserialized = JObject.FromObject(dumped, serializer);

            var path = Path.Combine(SignalsEverywhere.Shared.ModDirectory, "signal-dump.json");

            using (StreamWriter streamWriter = new StreamWriter(path))
                using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(jsonWriter, deserialized);
            }
            return "Successfully dumped signals to mod directory";
        }

        if (components[1] == "dump" && components[2] == "panel")
        {
            var dumped = SignalsEverywhere.Shared.PanelLayout;
            var serializer = new JsonSerializer();
            serializer.Converters.Add(new StringEnumConverter());
            var deserialized = JObject.FromObject(dumped, serializer);
            var path = Path.Combine(SignalsEverywhere.Shared.ModDirectory, "panel-dump.json");
            using (StreamWriter streamWriter = new StreamWriter(path))
                using (var jsonWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.Indented })
            {
                serializer.Serialize(jsonWriter, deserialized);
            }
            return "Successfully dumped panel to mod directory";
        }

        if (components[1] == "interlock")
        {
            var interlock = CTCPanelController.Shared.AllInterlockings[components[2]];
            if (interlock == null)
            {
                return "Interlocking not found";
            }
            for (int i = 0; i < interlock.routes.Count; i++)
            {
                Log.Information($"Route: {i}");
                var (leftBlocks, leftSignal, leftLined) = interlock.BlockAndNexSignal(i, CTCDirection.Left);
                Log.Information($"Left Blocks: {string.Join(", ", leftBlocks.Select(b => b.id))}, Signal: {leftSignal?.id ?? "None"}, Lined: {leftLined}");
                var (rightBlocks, rightSignal, rightLined) = interlock.BlockAndNexSignal(i, CTCDirection.Right);
                Log.Information($"Left Blocks: {string.Join(", ", rightBlocks.Select(b => b.id))}, Signal: {rightSignal?.id ?? "None"}, Lined: {rightLined}");
            }    
        }

        if (components[1] == "signal")
        {
            var signal = Graph.Shared.MarkerForId(components[2]);
            if (signal == null || signal.Signal == null)
            {
                return "Signal not found";
            }
            Log.Information($"Signal: {signal.Signal.id}");
            Log.Information($"Interlocking: {signal.Signal.Interlocking?.id ?? "None"}");

            if (signal.Signal is CTCAutoSignal autoSignal)
            {
                foreach (var routeMapping in autoSignal.interlockingRouteMapping)
                {
                    var result = signal.Signal.Interlocking.BlockAndNexSignal(routeMapping, autoSignal.direction);
                    Log.Information($"Route {routeMapping} for signal {signal.Signal.id} returned {string.Join(", ", result.Item1.Select(b => b.id))} " +
                                    $"Signal: {result.Item2?.id ?? "None"}, Lined: {result.Item3}");
                    
                }
            }
        }

        if (components[1] == "panel")
        {
            var knobs = Object.FindObjectsOfType<CTCPanelKnob>(true);
            foreach (var knob in knobs)
            {
                Log.Information($"Knob found: {knob.transform.parent.name} {knob.transform.name} {knob.knobId} {knob.purpose}");
            }
            
            var buttons = Object.FindObjectsOfType<CTCPanelButton>(true);
            foreach (var button in buttons)
            {
                Log.Information($"Button found: {button.transform.parent.name} {button.transform.name} {button.id}");
            }
        }

        if (components[1] == "milepost")
        {
            var posts = Object.FindObjectsOfType<MilePost>(true);
            foreach (var milePost in posts)
            {
                var trackMarker = milePost.GetComponent<TrackMarker>();
                Log.Information($"====== Found Milepost: Origin[{milePost.origin}], Mileage[{milePost.mileage}], Prefix[{milePost.prefix}]");
                Log.Information($"-> Trackmarker: {trackMarker?.type}, {trackMarker.Location}");
                PrefabTypeAnalyzer.AnalyzeFromMonoBehaviour(milePost);
            }
        }

        if (components[1] == "sound")
        {
            StateManager.Shared.AudioPlayer.audioLibrary.entries.ForEach(e =>
            {
                Log.Information($"Sound: {e.name} --> {e.volumeMultiplier}");
            });
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