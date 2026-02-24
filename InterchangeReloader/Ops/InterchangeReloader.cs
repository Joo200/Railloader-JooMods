using System.Collections.Generic;
using System.Linq;
using Game;
using Model;
using Model.Ops;
using Model.Ops.Definition;
using Serilog;
using StrangeCustoms.Tracks;
using StrangeCustoms.Tracks.Industries;
using UI.Builder;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Random = UnityEngine.Random;

namespace InterchangeReloader.Ops;

public class InterchangeReloader : IndustryComponent, ICustomIndustryComponent
{
    private static ILogger logger = Log.ForContext<InterchangeReloader>();

    public static readonly List<InterchangeReloader> Reloaders = new();
    
    public float MinCarTime { get; private set; } = 2.0f;
    public float MaxCarTime { get; private set; } = 6.0F;

    public int MaxCars { get; private set; } = 12;

    public List<Load> ExampleLoads { get; private set; } = new();

    public static bool SkomActive = false;

    public bool AcceptsCarsWithLoad(IOpsCar car)
    {
        return !Disabled && isActiveAndEnabled && !Industry.ProgressionDisabled && carTypeFilter.Matches(car.CarType);
    }

    public bool Disabled
    {
        get => Industry.Storage.InterchangeDisabled;
        set => Industry.Storage.SetInterchangeDisabled(value);
    }

    public void OnEnable()
    {
        Reloaders.Add(this);
    }
    
    public void OnDisable()
    {
        Reloaders.Remove(this);
    }

    public void SerializeComponent(SerializedComponent serializedComponent)
    {
        serializedComponent.ExtraData?["minCarTime"] = MinCarTime;
        serializedComponent.ExtraData?["maxCarTime"] = MaxCarTime;
        serializedComponent.ExtraData?["maxCars"] = MaxCars;
    }

    public void DeserializeComponent(SerializedComponent serializedComponent, PatchingContext ctx)
    {
        if (serializedComponent.ExtraData == null)
        {
            return;
        }
        
        serializedComponent.ExtraData.TryGetValue("minCarTime", out var minDays);
        if (minDays != null) MinCarTime = (float)minDays;
        serializedComponent.ExtraData.TryGetValue("maxCarTime", out var maxDays);
        if (maxDays != null) MaxCarTime = (float)maxDays;
        serializedComponent.ExtraData.TryGetValue("maxCars", out var maxCars);
        if (maxCars != null) MaxCars = (int)maxCars;
        serializedComponent.ExtraData.TryGetValue("loads", out var loads);
        if (loads != null) ExampleLoads = loads.ToList().Select(l => ctx.GetLoad((string)l)).ToList();
    }

    protected override void OnCompleteWaybill(IIndustryContext ctx, IOpsCar car, Waybill waybill)
    {
        base.OnCompleteWaybill(ctx, car, waybill);
        float multiplier = Industry.GetContractMultiplier() != 0 ? 1 / Industry.GetContractMultiplier() : 1.0f;
        var yeetTime = ctx.Now.AddingHours(Random.Range(MinCarTime * multiplier, MaxCarTime * multiplier));
        ctx.SetDateTime("yeet-" + car.Id, yeetTime);
    }

    private struct Order
    {
        public Load? Load;
        public IndustryComponent Destination;
        public bool NoPayment;
        public string? Tag;
    }

    private static bool IsValidTarget(IndustryComponent component, out Load? load, out bool orderEmpties, out bool orderLoads, out float maxStorage)
    {
        load = null;
        orderEmpties = false;
        orderLoads = false;
        maxStorage = 0f;
        switch (component)
        {
            case IndustryLoader industryLoader:
                load = industryLoader.load;
                orderEmpties = industryLoader.orderEmpties;
                maxStorage = industryLoader.maxStorage;
                return true;
            case IndustryUnloader industryUnloader:
                load = industryUnloader.load;
                orderLoads = industryUnloader.orderLoads;
                maxStorage = industryUnloader.maxStorage;
                return true;
            case TeleportLoadingIndustry teleportLoadingIndustry:
                load = teleportLoadingIndustry.load;
                orderEmpties = teleportLoadingIndustry.orderEmpties;
                maxStorage = teleportLoadingIndustry.maxStorage;
                return true;
            default:
                return false;
        }
    }

    private Order? FindOrderForCar(IOpsCar car)
    {
        var candidates = new List<Order>();
        var now = TimeWeather.Now;

        foreach (var industry in OpsController.Shared.AllIndustries)
        {
            if (industry.ProgressionDisabled || !industry.HasActiveContract(now))
                continue;

            foreach (var (component, context) in industry.EnumerateComponentContexts(0.0f))
            {
                if (component.ProgressionDisabled || !component.carTypeFilter.Matches(car.CarType))
                    continue;

                if (!IsValidTarget(component, out var load, out var orderEmpties, out var orderLoads, out var maxStorage))
                    continue;

                if (orderEmpties || orderLoads)
                {
                    float inStorage = context.QuantityInStorage(load);
                    float onOrder = context.QuantityOnOrder(load) + context.AvailableCapacityInCars(component.carTypeFilter, load);

                    if (inStorage + onOrder < maxStorage)
                    {
                        candidates.Add(new Order
                        {
                            Load = orderLoads ? load : null,
                            Destination = component,
                            NoPayment = false,
                            Tag = null
                        });
                    }
                }
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public override void Service(IIndustryContext ctx)
    {
        int interchangeCars = 0;
        int reorderedCars = 0;
        foreach (IOpsCar car in CarsAtPosition())
        {
            var yeetTime = ctx.GetDateTime("yeet-" + car.Id, car.Waybill.Value.Created.AddingHours(MinCarTime));
            if (yeetTime > ctx.Now || !car.Waybill.HasValue)
                continue;
            
            ctx.CounterClear("yeet-" + car.Id);
            
            var order = FindOrderForCar(car);
            if (order.HasValue)
            {
                GenerateWaybillForCar(car, ctx, order.Value);
                reorderedCars++;
            } else {
                int rd = Random.Range(0, ExampleLoads.Count + 1);
                if (rd == ExampleLoads.Count)
                    ctx.OrderAwayEmpty(car);
                else
                {
                    ReflectionUtils.RestockCar(car, ExampleLoads[rd]);
                    ctx.OrderAwayLoaded(car);
                }
                    
                ctx.OrderAwayEmpty(car);
                interchangeCars++;
            }
        }
    }

    public override void OrderCars(IIndustryContext ctx)
    {
        int cars = ctx.NumberOfCarsOnOrderForTag(null);
        int maxExpected = Mathf.RoundToInt(MaxCars * (SkomActive ? 0.5f : 1.0f));
        int order = maxExpected - cars;
        logger.Information($"Ordered {order} cars for {DisplayName}.");
        if (order < 0) return;
        for (int i = 0; i < order; i++)
        {
            int rd = Random.Range(0, ExampleLoads.Count + 1);
            if (rd == ExampleLoads.Count)
                ctx.OrderEmpty(carTypeFilter, null);
            else
                ctx.OrderLoad(carTypeFilter, ExampleLoads[rd], null, false, out var _);
        }
    }

    private IEnumerable<IOpsCar> CarsAtPosition()
    {
        foreach (Car car in OpsController.Shared.CarsAtPosition(this))
        {
            float num = 0.05f;
            if (Mathf.Abs(car.velocity) <= num)
            {
                var adapter = new OpsCarAdapter(car, OpsController.Shared);

                if (!adapter.Waybill.HasValue || adapter.IsOwnedByPlayer)
                {
                    continue;
                }

                if (!adapter.Waybill.Value.Destination.Equals(this))
                {
                    continue;
                }

                yield return adapter;
            }
        }
    }

    private void GenerateWaybillForCar(IOpsCar car, IIndustryContext ctx, Order order)
    {
        var controller = OpsController.Shared;
        var tons = order.Load != null ? ReflectionUtils.RestockCar(car, order.Load) : car.WeightInTons;
        var payment = order.NoPayment ? 0 : controller.PaymentForMove(this, order.Destination, (int)tons);
        int graceDays = controller.CalculateGraceDays(this, order.Destination);
        Waybill waybill = new Waybill(ctx.Now, this, order.Destination, payment, false, order.Tag, graceDays);
        TrainController.Shared.CarForId(car.Id).SetWaybill(waybill);
        logger.Information($"Changing the waybill of {car.DisplayName} to target {order.Destination.DisplayName}.");
    }

    public override void BuildPanel(UIPanelBuilder builder)
    {
    }
}