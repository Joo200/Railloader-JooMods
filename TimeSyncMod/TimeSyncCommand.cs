using Game;
using Game.Messages;
using Game.State;
using Network;
using UI.Console;

namespace TimeSyncMod
{
    [ConsoleCommand("/timesync", "Sync the time to all clients")]
    public class TimeSyncCommand : IConsoleCommand
    {
        public string Execute(string[] components)
        {
            if (!Multiplayer.IsHost)
            {
                return "Only host can sync time";
            }

            GameDateTime timeCursor = TimeWeather.Now;
            StateManager.ApplyLocal(new SetTimeOfDay((float)timeCursor.TotalSeconds));
            return "Synced time to all clients";
        }
    }
}
