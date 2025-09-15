using Game;
using Game.Messages;
using Game.State;
using UI.Console;

namespace TimeSyncMod
{
    [ConsoleCommand("/timesync", "Sync the time to all clients")]
    public class TimeSyncCommand : IConsoleCommand
    {
        public string Execute(string[] components)
        {
            GameDateTime timeCursor = TimeWeather.Now;
            StateManager.ApplyLocal(new SetTimeOfDay((float)timeCursor.TotalSeconds));
            return "synced time";
        }
    }
}
