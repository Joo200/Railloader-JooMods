# 5. Control points

## Interlockings

```json
"CN-W": {
  "interlocking": {
    "id": "cn-w",
    "displayName": "Cochran West",
    "switchSets": [ [ "Nuam" ] ],
    "outlets": [
      { "direction": "Left",  "blocks": [ "ab-cn" ], "nextSignal": "ajy-ee" },
      { "direction": "Right", "blocks": [ "cn-mm" ], "nextSignal": "cn-em" },
      { "direction": "Right", "blocks": [ "cn-ms" ], "nextSignal": "cn-es" }
    ],
    "routes": [
      { "switchFilters": [ "Normal" ],   "outletLeft": 0, "outletRight": 1 },
      { "switchFilters": [ "Reversed" ], "outletLeft": 0, "outletRight": 2 }
    ]
  },
  "blocks": { "cn-w": { ... } },
  "autoSignals": {
    "cn-we": { "direction": "Right", "headConfiguration": "Double", "blocks": [ "cn-w" ], "interlockingRouteMapping": [ 0, 1 ], ... },
    "cn-wm": { "direction": "Left",  "headConfiguration": "Single", "blocks": [ "cn-w" ], "interlockingRouteMapping": [ 0 ], ... },
    "cn-ws": { "direction": "Left",  "headConfiguration": "Single", "blocks": [ "cn-w" ], "interlockingRouteMapping": [ 1 ], ... }
  }
}
```

This is the west end of a passing siding: one switch, the main line to the east (`cn-mm`), the siding to the east (`cn-ms`), and the single track to the west (`ab-cn`).

### `id` and `displayName`

`id` is how signals, the panel and other mods refer to it. `displayName` is shown to the player. Interlocking ids must be unique railroad-wide.

### `switchSets`

A list of sets; each set is a list of switch node ids. One set gets one knob on the panel, and every switch in a set is thrown together (both ends of a crossover, for example). The order matters: `switchFilters` in each route follows it.

A control point with no switches (`"switchSets": []`) is a signal-only control point.

### `outlets`

The ways out. Each outlet has:

- **`direction`**: the direction of travel *leaving* the control point by this outlet.
- **`blocks`**: the block just past the control point on that side. These blocks get the traffic direction when a route is set, and must be clear for the signal to proceed. An empty list is allowed (dark territory beyond, for example).
- **`nextSignal`**: the next signal a train meets after leaving by this outlet. It decides green or yellow for the signal heads using this outlet. Use `null` only where there is no next signal; a head leading to `null` can never show better than Approach.

### `routes`

Each route says: when the switches are in these positions, a train going `Left` leaves by outlet `outletLeft`, and a train going `Right` leaves by outlet `outletRight`.

- **`switchFilters`**: one entry per switch set, in the same order: `Normal`, `Reversed`, or `None` (doesn't matter).
- The first route whose filters match the current switch positions is the lined one.
- Routes are numbered from 0 in the order listed. Signals refer to them by number in `interlockingRouteMapping`.

Every switch position you want to be usable needs a route. Positions with no matching route can't be coded from the panel ("no route").

### The interlocking's own blocks

Blocks in the same module as the interlocking are its **OS blocks** (the track over the switches). When a route is coded they all get the traffic direction, and the interlocking won't move a switch while any of them is occupied. Signals at the control point list them in `blocks`, so a train standing on the switches holds every signal at Stop.

**Keep outlet blocks out of the control point's module.** The game counts *every* block in the module as an OS block, whether or not it covers the switches. Once a route is coded, a train entering any of them cancels it, and while one is occupied the dispatcher can't code or cancel a route at that control point. So the blocks in the outlets (the track a train approaches on) belong in a module without an interlocking: the stretch's own module, or a neighbouring one, the way the base game keeps the blocks east of Bryson East in `GI-BR`. Under RailForge, prefer a module that comes before the control point's; forward references to blocks are accepted, but not to interlockings (see [Forward references](08-railforge.md#forward-references)).

### Signals at the control point

Each signal at the control point faces into the OS block and uses `interlockingRouteMapping` to say which route each head follows:

- Approaching the facing-point side (the side with the points), a `Double` signal with `[0, 1]`: top head for the normal route, second head for the diverging route.
- On each leg of the frog side, a `Single` signal mapped to the one route that leg belongs to: `[0]` for the main, `[1]` for the siding.

### Coding a route

When the dispatcher codes a route, the game checks the OS blocks are clear and the switches can move, throws them, then sets the direction. Setting the direction gives traffic direction to the OS blocks and to the outlet's blocks, and fails if the next control point already has traffic set against it on that stretch. Once a train occupies an OS block, the direction is cleared again, and the dispatcher has to code the next move.

## Intermediates

An intermediate covers the automatic signals between two control points.

```json
"CN-AL": {
  "intermediate": {
    "id": "CN-AL",
    "blocks": [ "cn-al-1", "cn-al-2" ],
    "signals": [ "cn-al-wb", "cn-al-eb" ],
    "signalLeft": "cn-ee",
    "signalRight": "al-we"
  },
  "blocks": { "cn-al-1": { ... }, "cn-al-2": { ... } },
  "autoSignals": {
    "cn-al-wb": { "direction": "Left",  "blocks": [ "cn-al-1" ], "interlockingRouteMapping": [], ... },
    "cn-al-eb": { "direction": "Right", "blocks": [ "cn-al-2" ], "interlockingRouteMapping": [], ... }
  }
}
```

- **`id`**: use the module key.
- **`blocks`**: every block between the two control points, in order from left to right.
- **`signals`**: every automatic signal between them, in order from left to right by where they stand, both directions mixed.
- **`signalLeft`**: the first control point signal a `Left`-bound train meets past the left end. **`signalRight`**: the same for the right end.

An intermediate signal shows Stop when its block is occupied, and when the control point ahead of it has a route set against it. Otherwise it looks at the next signal in its direction: the next one in `signals`, or `signalLeft`/`signalRight` at the end.

Between two control points with no intermediate signals, you still need the blocks, but you don't need an intermediate. Put the blocks in the control points' outlets.

The control points at each end must name the intermediate's end blocks in their outlets, and their outlets' `nextSignal` should be the first intermediate signal in that direction. The links go both ways: if you add or move a signal on the line, update the intermediate and the control points.

**The stock intermediates on a feature object.** A few of the game's intermediates (between Whittier and Bryson) sit on the feature object instead of in a module, so they can't be patched. When you add a control point next to one, it keeps pointing at the old signal. Tetz's SignalsEverywhere Fixes relinks it (pending upstream as PR #8). Without it, avoid putting a new control point at the end of one of those intermediates.

## Crossovers

A crossover is a control point that can have more than one route set at once, like a double crossover where each track can carry its own move. It has `switchSets`, `outlets` and `routes` like an interlocking, plus:

- **`routes[].usedBlocks`**: the blocks each route occupies. Two moves can be set at the same time only when their `usedBlocks` don't overlap.
- **`signalGroups`**: groups of signals, each with a `groupId`, its `signals`, the one `allowedDirection` it can be set to, and the `allowedRoutes` it can use. Each group gets its own direction knob on the panel.

Signals in a crossover are auto signals with `interlockingRouteMapping` (one route per head, like an interlocking). Predicate signals in a crossover can add `"crossoverGroup": "<groupId>"` to a predicate to tie it to a group's direction.

Crossovers are SignalsEverywhere's own addition, not part of the base game. If you can, find a mod that uses one and dump its signals to see a working example before you write your own.
