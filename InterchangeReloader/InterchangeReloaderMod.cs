using System;
using System.Reflection;
using System.Linq;
using Railloader;
using Serilog;

namespace InterchangeReloader;

public class InterchangeReloaderMod : PluginBase
{
    ILogger logger = Log.ForContext<InterchangeReloaderMod>();

    private readonly IModDefinition _modDefinition;
    private readonly IModdingContext _moddingContext;
        
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
            RegisterSkom();
            Ops.InterchangeReloader.SkomActive = true;
        }
    }

    private void RegisterSkom()
    {
        try
        {
            var skomAssembly = Assembly.Load("InterchangeReloaderSkom");
            var integrationType = skomAssembly.GetType("InterchangeReloader.SKOMIntegration");
            integrationType.GetMethod("Register", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            logger.Information("Successfully registered SKOM integration");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to register SKOM integration");
        }
    }
}