using System;
using System.Collections.Generic;
using System.Linq;
using Game.Messages;
using Game.State;
using Serilog;
using Track;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

/// <summary>
/// A CTC crossover:
/// - Determines the currently available physical route based on switch lining (like CTCInterlocking.IsLined()).
/// - Allows multiple signal-groups to be active concurrently as long as their route "UsedBlocks" don't overlap.
/// - Each signal group has a single allowed SignalDirection (Left OR Right). The knob may only be Up(None) or that allowed direction.
/// </summary>
public class CTCCrossover : MonoBehaviour
{
    public string id;
    public string displayName;

    public List<SwitchSet> switchSets = new();
    public List<Outlet> outlets = new();
    public List<Route> routes = new();
    public List<SignalGroup> signalGroups = new();

    private IReadOnlyList<CTCBlock> _blocks;
    private SignalStorage _storage;

    // Runtime: observe occupancy on any blocks used by active groups; if occupied -> drop affected groups.
    private readonly HashSet<IDisposable> _routeBlockObservers = new();
    private IDisposable? _modeObserver = null;
    private IDisposable? _unlockedObserver = null;

    private Serilog.ILogger Logger => Log.ForContext<CTCCrossover>().ForContext("CrossoverId", id ?? "<null>");

    public IReadOnlyList<CTCBlock> Blocks
    {
        get
        {
            if (_blocks == null)
                _blocks = GetComponentsInChildren<CTCBlock>(true);
            return _blocks;
        }
    }

    private void OnEnable()
    {
        _storage = GetComponentInParent<SignalStorage>();
        
        if (!StateManager.IsHost)
            return;
        
        foreach (var group in signalGroups)
        {
            var currentDirection = _storage.GetCrossoverGroupDirection(group.groupId);
            if (currentDirection == CTCTrafficFilter.None)
                continue;

            // Find which route is currently lined for this active group
            var linedRoutes = LinedRoutesForDirection(currentDirection);
            if (linedRoutes.Count > 0)
            {
                var routeIndex = linedRoutes[0];
                var route = routes[routeIndex];
                if (route.usedBlocks != null)
                {
                    foreach (var block in route.usedBlocks)
                    {
                        if (block != null)
                            block.TrafficFilter = currentDirection;
                    }
                }
            }
            else
            {
                // Group is active in storage but no route is lined.
                // This shouldn't happen under normal CTC operation but could happen on load if switches were moved.
                _storage.SetCrossoverGroupDirection(group.groupId, CTCTrafficFilter.None);
            }
        }
        
        RefreshRouteBlockObservers();
        _modeObserver = _storage.ObserveSystemMode(e =>
        {
            switch (e)
            {
                case SystemMode.ABS:
                    foreach (var group in signalGroups)
                    {
                        _storage.SetCrossoverGroupDirection(group.groupId, CTCTrafficFilter.Any);    
                    }

                    break;
                case SystemMode.CTC:
                    break;
            }
        });

        _unlockedObserver = _storage.ObserveUnlockedSwitchIds(nodsIdsArray =>
        {
            foreach (var switchNode in switchSets.SelectMany(c => c.switchNodes))
            {
                bool flag = nodsIdsArray.Contains(switchNode.id);
                if (flag != switchNode.IsCTCSwitchUnlocked)
                {
                    switchNode.IsCTCSwitchUnlocked = flag;
                    Action onDidChangeThrown = switchNode.OnDidChangeThrown;
                    if (onDidChangeThrown != null)
                        onDidChangeThrown();
                }
            }
        });
    }

    private void OnDisable()
    {
        StopObservingRouteBlocks();
        _modeObserver?.Dispose();
        _unlockedObserver?.Dispose();
    }

    // -----------------------------
    // Public API
    // -----------------------------

    
    private static CTCTrafficFilter AsFilter(SignalDirection direction)
    {
        switch (direction)
        {
            case SignalDirection.None:
                return CTCTrafficFilter.None;
            case SignalDirection.Right:
                return CTCTrafficFilter.Right;
            case SignalDirection.Left:
                return CTCTrafficFilter.Left;
            default:
                throw new ArgumentOutOfRangeException(nameof (direction), (object) direction, (string) null);
        }
    }

    public void DeactivateAll()
    {
        foreach (var group in signalGroups)
        {
            _storage.SetCrossoverGroupDirection(group.groupId, CTCTrafficFilter.None);
        }

        foreach (var block in Blocks)
        {
            if (block != null)
                block.TrafficFilter = CTCTrafficFilter.None;
        }

        foreach (var route in routes)
        {
            if (route.usedBlocks != null)
            {
                foreach (var block in route.usedBlocks)
                {
                    if (block != null)
                        block.TrafficFilter = CTCTrafficFilter.None;
                }
            }
        }

        foreach (var outlet in outlets)
        {
            SetOutletBlocks(outlet, outlet.direction, CTCTrafficFilter.None, true);
        }
    }

    public bool IsTrafficAgainst(CTCBlock block, CTCTrafficFilter trafficFilter)
    {
        foreach (var group in signalGroups)
        {
            var activeDir = _storage.GetCrossoverGroupDirection(group.groupId);
            if (activeDir == CTCTrafficFilter.None || activeDir == CTCTrafficFilter.Any)
                continue;

            var linedRoutes = LinedRoutesForDirection(activeDir);
            if (linedRoutes.Count == 0)
                continue;

            int routeIndex = linedRoutes[0];
            var route = routes[routeIndex];
            var outlet = OutletForDirection(route, activeDir);

            if (outlet.Blocks.Contains(block))
            {
                return activeDir != trafficFilter;
            }
        }
        
        return false;
    }

    private bool SetOutletBlocks(Outlet outlet, CTCDirection direction, CTCTrafficFilter trafficFilter, bool isResetting, bool dryRun = false)
    {
        foreach (var block in outlet.Blocks)
        {
            CTCIntermediate im = block.Intermediate;
            if (im == null) continue;
            var sig = im.NextExternalSignalForDirection(direction);
            if (sig.Interlocking != null &&
                sig.Interlocking.IsTrafficAgainst(im.BlockAtEnd(direction), trafficFilter, null)) return false;
            if (sig.GetComponentInParent<CTCCrossover>()?.IsTrafficAgainst(im.BlockAtEnd(direction), trafficFilter) ?? false) return false;
        }

        if (!isResetting)
        {
            if (outlet.Blocks.Any(b => b.IsOccupied)) return false;
        }
        
        foreach (var bl in outlet.Blocks)
        {
            if (bl.isActiveAndEnabled && !bl.TrySetDirection(direction, trafficFilter, dryRun)) return false;
        }

        return true;
    }

    public bool Code(List<(TrackNode node, SwitchSetting setting)> nodes, Dictionary<string, SignalDirection> groupDirections, out CodeFailureReason reason)
    {
        var switchOverrides = new Dictionary<TrackNode, SwitchSetting>();
        if (nodes != null)
        {
            foreach (var (node, setting) in nodes)
            {
                if (node == null) continue;
                switchOverrides[node] = setting;
            }
        }

        // 1. Dry run: can we activate all requested groups?
        var plan = new List<(SignalGroup group, SignalDirection direction, int routeIndex)>();
        var activeBlocks = new HashSet<CTCBlock>();
        var activeRoutes = new HashSet<int>();

        foreach (var group in signalGroups)
        {
            var direction = SignalDirection.None;
            if (groupDirections != null && groupDirections.TryGetValue(group.groupId, out var dir))
                direction = dir;

            if (direction == SignalDirection.None)
                continue;

            if (direction != group.allowedDirection)
            {
                reason = CodeFailureReason.DirectionNotAllowedForGroup;
                return false;
            }

            if (!TrySelectRouteForGroup(group, direction, switchOverrides, activeBlocks, activeRoutes, out var selectedRouteIndex, out reason))
                return false;
            
            foreach (var (otherGroup, otherDir, otherIndex) in plan)
            {
                if (otherIndex == selectedRouteIndex && otherDir != direction)
                {
                    reason = CodeFailureReason.ConflictWithActiveGroup;
                    Logger.Warning($"Conflict detected: Group {group.groupId} wants to use route {selectedRouteIndex} in direction {direction} but group {otherGroup.groupId} is already using it in direction {otherDir}");
                    return false;
                }
            }
            
            // Mark these blocks as active for subsequent groups in the same Code() call
            var route = routes[selectedRouteIndex];
            var (blocks, _) = GetBlocksForGroupActivation(route, AsFilter(direction));
            
            // Validate blocks (occupancy and conflict) - already done in TrySelectRouteForGroup, 
            // but we need to ensure consistency with CTCInterlocking.SetTrafficFilterFromHelper (dryRun=true)
            var filter = AsFilter(direction);
            var outlet = OutletForDirection(route, filter);
            if (!SetOutletBlocks(outlet, outlet.direction, CTCTrafficFilter.None, true)) 
            {
                Logger.Warning("Crossover {id}: Block {blockId} TrySetDirection reset failed.", id, outlet.Blocks.First().id);
                reason = CodeFailureReason.BlockConfigError;
                return false;
            }
            if (!SetOutletBlocks(outlet, outlet.direction, filter, false, true))
            {
                Logger.Warning("Crossover {id}: Block {blockId} TrySetDirection failed in dry run, {setting}.", id, outlet.Blocks.First().id, filter);
                reason = CodeFailureReason.ConflictWithActiveGroup;
                return false;
            }

            foreach (var b in outlet.Blocks)
                activeBlocks.Add(b);

            foreach (var b in blocks)
            {
                if (b == null || !b.isActiveAndEnabled) continue;
                b.TrafficFilter = filter;
                activeBlocks.Add(b);
            }

            activeRoutes.Add(selectedRouteIndex);

            Logger.Information("Crossover {id}: Selected route {routeIndex} for group {groupId} with direction {direction}.", id, selectedRouteIndex, group.groupId, direction);
            plan.Add((group, direction, selectedRouteIndex));
        }

        // 2. Clear everything first
        DeactivateAll();

        // 3. Apply switches
        if (nodes != null)
        {
            foreach (var (node, setting) in nodes)
            {
                if (node == null)
                {
                    Logger.Warning("Crossover {id}: null switch node provided to Code()", id);
                    continue;
                }

                if (!TrySetSwitch(node, setting))
                {
                    reason = CodeFailureReason.SwitchBlocked;
                    return false;
                }
            }
        }

        // 4. Apply groups
        foreach (var (group, direction, routeIndex) in plan)
        {
            var filter = AsFilter(direction);
            _storage.SetCrossoverGroupDirection(group.groupId, filter);
            
            var route = routes[routeIndex];
            var (blocksToSet, _) = GetBlocksForGroupActivation(route, filter);
            var outlet = OutletForDirection(route, filter);
            
            SetOutletBlocks(outlet, outlet.direction, filter, false);

            foreach (var block in blocksToSet)
                block.TrafficFilter = filter;
            
            Logger.Information("Crossover {id}: Activated group {groupId} at route {routeIndex} with filter {filter}.", id, group.groupId, routeIndex, filter);
        }

        reason = CodeFailureReason.None;
        return true;
    }

    // -----------------------------
    // Route selection by switch lining
    // -----------------------------

    private List<int> LinedRoutesForDirection(CTCTrafficFilter direction, IReadOnlyDictionary<TrackNode, SwitchSetting>? switchOverrides = null)
    {
        if (direction != CTCTrafficFilter.Left && direction != CTCTrafficFilter.Right)
            return new List<int>();

        var lined = new List<int>();
        for (int i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            if (!IsLined(route, switchOverrides))
                continue;

            var outletIndex = direction == CTCTrafficFilter.Left ? route.outletLeft : route.outletRight;
            if (outletIndex < 0 || outletIndex >= outlets.Count)
                continue;

            var requiredOutletDir = direction == CTCTrafficFilter.Left ? CTCDirection.Left : CTCDirection.Right;
            if (outlets[outletIndex].direction != requiredOutletDir)
                continue;

            lined.Add(i);
        }

        return lined;
    }

    private bool IsLined(Route route, IReadOnlyDictionary<TrackNode, SwitchSetting>? switchOverrides = null)
    {
        for (int index = 0; index < switchSets.Count; ++index)
        {
            var switchSet = switchSets[index];

            // If route doesn't specify a filter for this set -> treat as wildcard
            if (route.switchFilters == null || index >= route.switchFilters.Count)
                continue;

            var filter = route.switchFilters[index];
            if (filter == SwitchFilter.None)
                continue;

            SwitchSetting requiredSetting = filter switch
            {
                SwitchFilter.Normal => SwitchSetting.Normal,
                SwitchFilter.Reversed => SwitchSetting.Reversed,
                _ => throw new ArgumentOutOfRangeException()
            };

            foreach (var switchNode in switchSet.switchNodes)
            {
                if (switchNode == null)
                    continue;

                var currentSetting = GetSwitchSetting(switchNode, switchOverrides);
                if (switchNode.IsCTCSwitchUnlocked && _storage.SystemMode == SystemMode.CTC)
                    return false;
                if (currentSetting != requiredSetting)
                    return false;
            }
        }

        return true;
    }

    private SwitchSetting GetSwitchSetting(TrackNode node, IReadOnlyDictionary<TrackNode, SwitchSetting>? switchOverrides)
    {
        if (switchOverrides != null && switchOverrides.TryGetValue(node, out var setting))
            return setting;

        return node.isThrown ? SwitchSetting.Reversed : SwitchSetting.Normal;
    }

    // -----------------------------
    // Conflict & occupancy logic
    // -----------------------------

    private bool TrySelectRouteForGroup(SignalGroup group, SignalDirection direction, IReadOnlyDictionary<TrackNode, SwitchSetting>? switchOverrides, HashSet<CTCBlock> activeBlocksInPlan, HashSet<int> activeRoutesInPlan, out int selectedRouteIndex, out CodeFailureReason reason)
    {
        selectedRouteIndex = -1;

        var filter = AsFilter(direction);
        var linedRouteIndices = LinedRoutesForDirection(filter, switchOverrides);
        if (linedRouteIndices.Count == 0)
        {
            reason = CodeFailureReason.NoRoute;
            return false;
        }

        bool sawInvalidOutlet = false;
        bool sawNoBlocks = false;
        bool sawOccupied = false;
        bool sawConflict = false;

        foreach (var routeIndex in linedRouteIndices)
        {
            if (!group.allowedRoutes.Contains(routeIndex))
            {
                continue;
            }
            
            if (activeRoutesInPlan.Contains(routeIndex))
            {
                Logger.Warning("Crossover {id}: Route {routeIndex} is already active in plan.", id, routeIndex);
                sawConflict = true;
                continue;
            }

            var route = routes[routeIndex];
            var (blocks, outletOk) = GetBlocksForGroupActivation(route, filter);
            if (!outletOk)
            {
                sawInvalidOutlet = true;
                continue;
            }

            if (blocks.Count == 0)
            {
                sawNoBlocks = true;
                continue;
            }

            bool occupied = false;
            foreach (var b in blocks)
            {
                if (b != null && b.IsOccupied)
                {
                    Logger.Information("Crossover {id}: Block {blockId} is occupied.", id, b.id);
                    occupied = true;
                    break;
                }
            }
            if (occupied)
            {
                sawOccupied = true;
                continue;
            }

            bool conflict = false;
            var outlet = OutletForDirection(route, filter);
            foreach (var b in blocks.OfType<CTCBlock>().Where(b => activeBlocksInPlan.Contains(b)))
            {
                Logger.Warning("Crossover {id}: Block {blockId} is active in plan but not in route.", id, b.id);
                conflict = true;
                break;
            }
            if (conflict)
            {
                sawConflict = true;
                continue;
            }

            Logger.Information("Crossover {id}: Route {routeIndex} is valid for group {groupId} in direction {direction}.", id, routeIndex, group.groupId, direction);
            selectedRouteIndex = routeIndex;
            reason = CodeFailureReason.None;
            return true;
        }

        if (sawConflict)
            reason = CodeFailureReason.ConflictWithActiveGroup;
        else if (sawOccupied)
            reason = CodeFailureReason.BlockOccupied;
        else if (sawNoBlocks)
            reason = CodeFailureReason.NoBlocksDefined;
        else if (sawInvalidOutlet)
            reason = CodeFailureReason.InvalidDirectionOrOutlet;
        else
            reason = CodeFailureReason.NoRoute;

        return false;
    }

    private (HashSet<CTCBlock> blocks, bool ok) GetBlocksForGroupActivation(Route route, CTCTrafficFilter direction)
    {
        if (direction != CTCTrafficFilter.Left && direction != CTCTrafficFilter.Right)
            return (new HashSet<CTCBlock>(), false);

        // Primary: route-declared used blocks (your “crossover conflict domain”)
        var set = new HashSet<CTCBlock>(route.usedBlocks);
        return (set, true);
    }

    private Outlet OutletForDirection(Route route, CTCTrafficFilter direction)
    {
        return direction switch
        {
            CTCTrafficFilter.Left => outlets[route.outletLeft],
            CTCTrafficFilter.Right => outlets[route.outletRight],
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    // -----------------------------
    // Observers: if a used block becomes occupied -> drop the affected group(s)
    // -----------------------------

    private void RefreshRouteBlockObservers()
    {
        StopObservingRouteBlocks();

        if (_storage == null)
            return;

        var observed = new HashSet<CTCBlock>();

        foreach (var b in Blocks)
            observed.Add(b);

        foreach (var b in outlets.SelectMany(o => o.Blocks))
            observed.Add(b);

        foreach (var block in observed.Distinct())
            ObserveRouteBlock(block);
    }

    private void ObserveRouteBlock(CTCBlock block)
    {
        var disp = _storage.ObserveBlockOccupancy(block.id, occupied =>
        {
            if (!occupied)
                return;

            // Drop any group whose active-block-set includes this block
            foreach (var group in signalGroups)
            {
                var groupDirection = _storage.GetCrossoverGroupDirection(group.groupId);
                if (groupDirection == CTCTrafficFilter.None)
                    continue;

                var linedRoutes = LinedRoutesForDirection(groupDirection);
                if (linedRoutes.Count == 0)
                    continue;

                int routeIndex = linedRoutes.First(i => group.allowedRoutes.Contains(i));

                var (blocks, ok) = GetBlocksForGroupActivation(routes[routeIndex], groupDirection);
                if (!ok)
                    continue;

                Outlet outlet = OutletForDirection(routes[routeIndex], groupDirection);
                foreach (var b in outlet.Blocks)
                {
                    blocks.Add(b);
                }
                
                if (blocks.Contains(block))
                {
                    Logger.Information("Crossover {id}: Dropping group {groupId} because block {blockId} became occupied.",
                        id, group.groupId, block.id);

                    _storage.SetCrossoverGroupDirection(group.groupId, CTCTrafficFilter.None);
                    
                    foreach (var b in blocks)
                    {
                        b.TrafficFilter = CTCTrafficFilter.None;
                    }
                }
            }
        });

        _routeBlockObservers.Add(disp);
    }

    private void StopObservingRouteBlocks()
    {
        foreach (var d in _routeBlockObservers)
            d.Dispose();
        _routeBlockObservers.Clear();
    }
    
    public (IReadOnlyCollection<CTCBlock>, CTCSignal, bool) BlockAndNextSignal(
        string signalId,
        int routeIndex,
        CTCDirection direction)
    {
        if (routeIndex < 0 || routeIndex >= routes.Count)
            throw new ArgumentException($"Crossover {id}: Index out of range: {routeIndex}", nameof (routeIndex));
        Route route = routes[routeIndex];
        Outlet outlet = OutletForDirection(route, direction == CTCDirection.Left ? CTCTrafficFilter.Left : CTCTrafficFilter.Right);
        bool flag = IsLined(route);
        var signalGroup = signalGroups.FirstOrDefault(g => g.signals.Any(sig => sig != null && sig.id == signalId));
        if (!string.IsNullOrEmpty(signalGroup.groupId))
        {
            var activeDir = _storage.GetCrossoverGroupDirection(signalGroup.groupId);
            flag &= DirectionMatches(direction, activeDir);
        }
        return (route.usedBlocks.Union(outlet.Blocks).ToList(), outlet.nextSignal, flag);
    }

    private static bool DirectionMatches(CTCDirection signalDirection, CTCTrafficFilter trafficFilter)
    {
        switch (trafficFilter)
        {
            case CTCTrafficFilter.None:
                return false;
            case CTCTrafficFilter.Right:
                return signalDirection == CTCDirection.Right;
            case CTCTrafficFilter.Left:
                return signalDirection == CTCDirection.Left;
            case CTCTrafficFilter.Any:
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof (trafficFilter), (object) trafficFilter, (string) null);
        }
    }

    // -----------------------------
    // Data structs
    // -----------------------------

    [Serializable]
    public struct SwitchSet
    {
        public List<TrackNode> switchNodes;
    }

    [Serializable]
    public struct Outlet
    {
        [Tooltip("Direction of this outlet relative to the crossover. Direction of travel _from_ the crossover.")]
        public CTCDirection direction;

        [Tooltip("Blocks that this outlet connects directly to.")]
        public List<CTCBlock> blocks;

        [Tooltip("Signal beyond 'block'.")]
        public CTCSignal nextSignal;

        public IReadOnlyCollection<CTCBlock> Blocks
            => blocks != null && blocks.Count > 0 ? blocks : Array.Empty<CTCBlock>();
    }

    [Serializable]
    public struct Route
    {
        [Tooltip("Per switch-set filter (None/Normal/Reversed), same semantics as interlocking.")]
        public List<SwitchFilter> switchFilters;

        public int outletLeft;
        public int outletRight;

        [Tooltip("Blocks used by this crossover route. This is the conflict domain for parallel operation.")]
        public List<CTCBlock> usedBlocks;
    }

    [Serializable]
    public struct SignalGroup
    {
        public string groupId;

        [Tooltip("Signals belonging to the group (for association/UI).")]
        public List<CTCSignal> signals;

        [Tooltip("The only allowed non-None direction for this group (Left OR Right).")]
        public SignalDirection allowedDirection;

        public List<int> allowedRoutes;

    }

    public enum CodeFailureReason
    {
        None,
        InvalidGroup,
        GroupMisconfigured,
        DirectionNotAllowedForGroup,
        NoRoute,
        InvalidDirectionOrOutlet,
        NoBlocksDefined,
        BlockOccupied,
        ConflictWithActiveGroup,
        SwitchBlocked,
        BlockConfigError
    }

    private bool TrySetSwitch(TrackNode node, SwitchSetting setting)
    {
        if (!TrainController.Shared.CanSetSwitch(node, setting == SwitchSetting.Reversed, out var foundCar))
        {
            var trainName = foundCar != null ? foundCar.DisplayName : "<unknown>";
            Logger.Warning("Crossover {id}: Unable to set switch {switchId}. Blocked by train {train}",
                id, node.id, trainName);
            return false;
        }

        StateManager.ApplyLocal(new SetSwitch(node.id, setting == SwitchSetting.Reversed, StateManager.Now, "CTCCrossover"));
        return true;
    }
}
