using System.Collections.Generic;
using System.Linq;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class SerializedCTCCrossover
{
    public string Id { get; set; }
    public string DisplayName { get; set; }

    public List<List<string>> SwitchSets { get; set; } = new();
    public List<Outlet> Outlets { get; set; } = new();
    public List<Route> Routes { get; set; } = new();
    public List<SignalGroup> SignalGroups { get; set; } = new();

    public struct Outlet
    {
        public CTCDirection Direction { get; set; }
        public List<string> Blocks { get; set; }
        public string? NextSignal { get; set; }

        public CTCCrossover.Outlet Apply(CTCPatchingContext ctx)
        {
            CTCCrossover.Outlet outlet = new();
            outlet.blocks = ctx.GetBlocks(Blocks);
            outlet.direction = Direction;
            outlet.nextSignal = ctx.GetSignal(NextSignal);
            return outlet;
        }
    }

    public struct Route
    {
        public List<SwitchFilter> SwitchFilters { get; set; }
        public int OutletLeft { get; set; }
        public int OutletRight { get; set; }

        public List<string> UsedBlocks { get; set; }

        public CTCCrossover.Route Apply(CTCPatchingContext ctx)
        {
            CTCCrossover.Route route = new();
            route.switchFilters = SwitchFilters;
            route.outletLeft = OutletLeft;
            route.outletRight = OutletRight;
            route.usedBlocks = ctx.GetBlocks(UsedBlocks);
            return route;
        }
    }

    public struct SignalGroup
    {
        public string GroupId { get; set; }
        public List<string> Signals { get; set; }
        public SignalDirection AllowedDirection { get; set; }
        public List<int> AllowedRoutes { get; set; }

        public CTCCrossover.SignalGroup Apply(CTCPatchingContext ctx)
        {
            CTCCrossover.SignalGroup group = new();
            group.groupId = GroupId;
            group.signals = (Signals ?? new List<string>())
                .Select(ctx.GetSignal)
                .Where(s => s != null)
                .ToList()!;
            group.allowedDirection = AllowedDirection;
            group.allowedRoutes = AllowedRoutes;
            return group;
        }
    }

    public SerializedCTCCrossover() { }

    public SerializedCTCCrossover(CTCCrossover crossover)
    {
        Id = crossover.id;
        DisplayName = crossover.displayName;

        SwitchSets = (crossover.switchSets)
            .Select(s => (s.switchNodes).Select(n => n.id).ToList())
            .ToList();

        Outlets = (crossover.outlets)
            .Select(o => new Outlet
            {
                Direction = o.direction,
                Blocks = (o.blocks).Select(b => b.id).ToList(),
                NextSignal = o.nextSignal?.id
            })
            .ToList();

        Routes = (crossover.routes)
            .Select(r => new Route
            {
                SwitchFilters = r.switchFilters,
                OutletLeft = r.outletLeft,
                OutletRight = r.outletRight,
                UsedBlocks = (r.usedBlocks).Select(b => b.id).ToList(),
            })
            .ToList();

        SignalGroups = (crossover.signalGroups)
            .Select(g => new SignalGroup
            {
                GroupId = g.groupId,
                Signals = (g.signals).Select(s => s.id).ToList(),
                AllowedDirection = g.allowedDirection
            })
            .ToList();
    }

    public void CreateFor(GameObject parent, CTCPatchingContext ctx)
    {
        if (Id == null)
        {
            ctx.Logger.Error("CTCCrossover has no ID");
            return;
        }

        var crossover = parent.GetComponent<CTCCrossover>() ?? parent.AddComponent<CTCCrossover>();
        crossover.id = Id;
        crossover.displayName = DisplayName;
        
        ctx.Crossovers[Id] = crossover;
    }

    public void ApplyTo(CTCCrossover crossover, CTCPatchingContext ctx)
    {
        crossover.switchSets = new();
        foreach (var set in SwitchSets)
        {
            CTCCrossover.SwitchSet ss = new();
            ss.switchNodes = set.Select(s => ctx.NodesById[s]).ToList();
            crossover.switchSets.Add(ss);
        }

        crossover.outlets = new();
        foreach (var o in Outlets)
            crossover.outlets.Add(o.Apply(ctx));

        crossover.routes = new();
        foreach (var r in Routes)
            crossover.routes.Add(r.Apply(ctx));

        crossover.signalGroups = new();
        foreach (var g in SignalGroups)
            crossover.signalGroups.Add(g.Apply(ctx));
    }
}
