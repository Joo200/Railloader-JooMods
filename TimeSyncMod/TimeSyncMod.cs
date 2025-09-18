using Game;
using Game.Messages;
using Game.State;
using Network;
using Railloader;
using Serilog;
using System.Threading;

namespace TimeSyncMod
{
    public class TimeSyncMod : PluginBase
    {
        Serilog.ILogger logger = Log.ForContext<TimeSyncMod>();

        private readonly Timer syncTimer;

        public TimeSyncMod(IModdingContext moddingContext, IModDefinition self)
        {
            logger.Information("Registering time sync command!");

            moddingContext.RegisterConsoleCommand(new TimeSyncCommand());

            syncTimer = new Timer(SyncTimes, new AutoResetEvent(false), 1000 * 60 * 10, 1000 * 60 * 10);
        }

        public void SyncTimes(object stateInfo)
        {
            if (!Multiplayer.IsHost)
            {
                return;
            }

            GameDateTime timeCursor = TimeWeather.Now;
            StateManager.ApplyLocal(new SetTimeOfDay((float)timeCursor.TotalSeconds));
            logger.Information("Synced time to all clients");
        }
    }
}
