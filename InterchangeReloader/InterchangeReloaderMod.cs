using System.Linq;
using Railloader;
using Serilog;

namespace InterchangeReloader;

public class InterchangeReloaderMod : PluginBase
{
    ILogger logger = Log.ForContext<InterchangeReloaderMod>();

    private readonly IModDefinition _modDefinition;
    private readonly IModdingContext _moddingContext;

    public static bool SKOMIntegrationEnabled { get; private set; } = false;
        
    public InterchangeReloaderMod(IModdingContext moddingContext, IModDefinition self)
    {
        logger.Information("Starting InterchangeReloader");
        _modDefinition = self;
        _moddingContext = moddingContext;
    }

    public override void OnEnable()
    {
        if (_moddingContext.Mods.Any(s => s.Id == "Zamu.SomeKindOfMadness"))
        {
            logger.Information("Detected SKOM, registering integration");
            SKOMIntegration.Register();
            SKOMIntegrationEnabled = true;
        }
    }
}