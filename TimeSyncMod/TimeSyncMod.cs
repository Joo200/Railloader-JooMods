using Network.Client;
using Railloader;
using Serilog;
using UI.Builder;

namespace TimeSyncMod
{
    public class TimeSyncMod : PluginBase
    {
        ILogger logger = Log.ForContext<TimeSyncMod>();

        public TimeSyncMod(IModdingContext moddingContext, IModDefinition self)
        {
            logger.Information("Registering time sync command!");

            moddingContext.RegisterConsoleCommand(new TimeSyncCommand());
        }
    }
}
