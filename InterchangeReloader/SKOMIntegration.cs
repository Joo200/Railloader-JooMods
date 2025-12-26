using GalaSoft.MvvmLight.Messaging;
using Serilog;
using Model.Ops;
using SomeKindOfMadness;

namespace InterchangeReloader;

public static class SKOMIntegration
{
    private static readonly ILogger Logger = Log.ForContext(typeof(SKOMIntegration));

    public static void Register()
    {
        Messenger.Default.Register<EmptyCarWillBeRouted>(typeof(SKOMIntegration), RerouteCar);
        Messenger.Default.Register<LoadedCarWillBeRouted>(typeof(SKOMIntegration), RerouteCar);
        Logger.Information("Registered SKOM integration");
    }

    private static void RerouteCar(OverrideCarDestination @event)
    {
        if (@event.TargetCount > 0)
            return;

        foreach (var reloader in Ops.InterchangeReloader.Reloaders)
        {
            if (reloader.Disabled || !reloader.isActiveAndEnabled || reloader.Industry.ProgressionDisabled)
                continue;

            if (@event.Car.Waybill.HasValue && @event.Car.Waybill.Value.Destination.Identifier == reloader.Identifier)
                continue;
            
            if (@event.CarPosition.Equals(reloader))
                continue;
            
            if (reloader.Industry.Contract == null)
                continue;

            if (!reloader.carTypeFilter.Matches(@event.Car.CarType))
                continue;
            
            var cars = OpsController.Shared.CountOrdersForIndustry(reloader.Industry);
            if (cars > reloader.MaxCars * 1.3f)
                continue;

            Logger.Information($"Relocated car to interchange reloader {reloader.DisplayName}");            
            @event.AddTarget(new TargetIndustry(reloader, 1));
        }
    }
}
