using System.Collections.Generic;
using System.Linq;
using Track;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public class SerializedCTCInterlocking
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public List<List<string>> SwitchSets { get; set; }
    public List<Outlet> Outlets { get; set; }
    public List<Route> Routes { get; set; }
    
    public struct Outlet
    {
        public CTCDirection Direction { get; set; }
        public List<string> Blocks { get; set; }
        public string? NextSignal { get; set; }

        public CTCInterlocking.Outlet Apply(CTCPatchingContext ctx)
        {
            CTCInterlocking.Outlet outlet = new();
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
        
        public CTCInterlocking.Route Apply(CTCPatchingContext ctx)
        {
            CTCInterlocking.Route route = new();
            route.switchFilters = SwitchFilters;
            route.outletLeft = OutletLeft;
            route.outletRight = OutletRight;
            return route;
        }
    }
    
    public SerializedCTCInterlocking() {}
    
    public SerializedCTCInterlocking(CTCInterlocking interlocking)
    {
        Id = interlocking.id;
        DisplayName = interlocking.displayName;
        SwitchSets = interlocking.switchSets.Select(s => s.switchNodes.Select(n => n.id).ToList()).ToList();
        Outlets = interlocking.outlets.Select(o => new Outlet {Direction = o.direction, Blocks = o.blocks.Select(b => b.id).ToList(), NextSignal = o.nextSignal?.id}).ToList();
        Routes = interlocking.routes.Select(r => new Route {SwitchFilters = r.switchFilters, OutletLeft = r.outletLeft, OutletRight = r.outletRight}).ToList();
    }

    public void CreateFor(GameObject parent, CTCPatchingContext ctx)
    {
        if (Id == null)
        {
            ctx.Logger.Error("Interlocking has no ID");
            return;
        }
        CTCInterlocking interlocking = parent.AddComponent<CTCInterlocking>();
        interlocking.id = Id;
        interlocking.displayName = DisplayName;
        ctx.Interlockings.Add(Id, interlocking);
    }

    public void ApplyTo(CTCInterlocking interlocking, CTCPatchingContext ctx)
    {
        interlocking.switchSets = new();
        foreach (var SwitchSet in SwitchSets)
        {
            CTCInterlocking.SwitchSet set = new();
            set.switchNodes = SwitchSet.Select(s => ctx.NodesById[s]).ToList();
            interlocking.switchSets.Add(set);
        }

        interlocking.outlets = new();
        foreach (var outlet in Outlets)
        {
            interlocking.outlets.Add(outlet.Apply(ctx));
        }

        interlocking.routes = new();
        foreach (var route in Routes)
        {
            var rte = route.Apply(ctx);
            interlocking.routes.Add(rte);
        }
    }
}