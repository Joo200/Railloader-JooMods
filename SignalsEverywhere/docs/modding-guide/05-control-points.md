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

### A signal on its own

The auto engineer treats Stop as absolute only at a signal that belongs to an interlocking; any other signal is stop and proceed (see [The auto engineer](02-how-ctc-works.md#the-auto-engineer)). A predicate signal standing on its own, such as a block signal at a signal box, gets the AE's absolute stop when you put it in a module with a **switchless interlocking**: no switches, two empty outlets and one route. The interlocking needs no blocks of its own.

```json
"COWEE-BOX": {
  "interlocking": {
    "id": "cowee-box",
    "displayName": "Cowee Signal Box",
    "switchSets": [],
    "outlets": [
      { "direction": "Left",  "blocks": [], "nextSignal": null },
      { "direction": "Right", "blocks": [], "nextSignal": null }
    ],
    "routes": [ { "switchFilters": [], "outletLeft": 0, "outletRight": 1 } ]
  },
  "predicateSignals": {
    "cowee-box-down": { "...": "a predicate signal with a Block predicate on cowee-down" }
  }
},
"COWEE-BLOCKS": {
  "blocks": { "cowee-down": { "...": "the block the signal protects" } }
}
```

The signal's aspect still comes from its own predicates, so it clears by itself when its block is free, in CTC and in ABS; nobody has to code it. The AE names it by the interlocking's `displayName` ("Stop, Cowee Signal Box"). Keep the block it protects in another module (here `COWEE-BLOCKS`): in the interlocking's module it would be an OS block. Don't use `InterlockingTrafficDirection` predicates on such a signal: no route is ever coded, so in CTC they never come true.

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

**The stock intermediates on a feature object.** A few of the game's intermediates (between Whittier and Bryson) sit on the feature object instead of in a module, so they can't be patched. When you add a control point next to one, it keeps pointing at the old signal. Tetz's SignalsEverywhere Fixes relinks it (pending upstream as PR #8), and so does the 0.6 build. Without either, avoid putting a new control point at the end of one of those intermediates.

## Crossovers

A crossover is a control point that can have more than one route set at once, like a double crossover where each track can carry its own move. It has `switchSets`, `outlets` and `routes` like an interlocking, plus:

- **`routes[].usedBlocks`**: the blocks each route occupies. Two moves can be set at the same time only when their `usedBlocks` don't overlap.
- **`signalGroups`**: groups of signals, each with a `groupId`, its `signals`, the one `allowedDirection` it can be set to, and the `allowedRoutes` it can use. Each group gets its own direction knob on the panel.

Signals in a crossover are auto signals with `interlockingRouteMapping` (one route per head, like an interlocking). Predicate signals in a crossover can add `"crossoverGroup": "<groupId>"` to a predicate to tie it to a group's direction.

Crossovers are SignalsEverywhere's own addition, not part of the base game.

**Make every `groupId` unique across the whole map, not just within the crossover.** SignalsEverywhere saves a group's direction under `crossover:<groupId>:direction`, without the crossover's id. Two crossovers that both have a group `aeb` share one setting: clearing one clears the other's group too. Prefix the group with the crossover's id (`xo-aeb`).

**One group per track and direction.** A group is the signals on one track facing one way. Its `allowedRoutes` are the routes a train on that track, going that way, can take: for a double crossover between tracks A and B, the eastbound group on A gets "A straight" and "A over to B". Each group then needs its own `allowedDirection`.

**Coding applies every knob.** The code button reads all of the crossover's switch knobs and group knobs and codes them together. SignalsEverywhere first checks every group that's set; if any of them can't clear (its track is occupied, its route isn't lined, or it clashes with another group), nothing is coded, not even the groups that could. So before you code a new move, put any group whose train has already gone back to the middle.

**Scissors crossovers.** Where the two crossover legs cross each other in a diamond, a train on one leg fouls the other. Put both legs' blocks in each crossing route's `usedBlocks`, and the two diagonal moves can't be set together.

### A double crossover

Tracks A and B, a crossover X1 from A (west) to B (east) and X2 from B (west) to A (east). Each crossover's switch set is the switch at both ends, thrown together. Outlets 0 and 1 are A and B on the west side, 2 and 3 on the east.

```json
"crossover": {
  "id": "xo",
  "displayName": "Example Crossover",
  "switchSets": [
    [ "<X1 switch on A>", "<X1 switch on B>" ],
    [ "<X2 switch on B>", "<X2 switch on A>" ]
  ],
  "outlets": [
    { "direction": "Left",  "blocks": [ "xo-aw" ], "nextSignal": "<next signal west on A>" },
    { "direction": "Left",  "blocks": [ "xo-bw" ], "nextSignal": "<next signal west on B>" },
    { "direction": "Right", "blocks": [ "xo-ae" ], "nextSignal": "<next signal east on A>" },
    { "direction": "Right", "blocks": [ "xo-be" ], "nextSignal": "<next signal east on B>" }
  ],
  "routes": [
    { "switchFilters": [ "Normal", "Normal" ],   "outletLeft": 0, "outletRight": 2, "usedBlocks": [ "xo-a" ] },
    { "switchFilters": [ "Normal", "Normal" ],   "outletLeft": 1, "outletRight": 3, "usedBlocks": [ "xo-b" ] },
    { "switchFilters": [ "Reversed", "Normal" ], "outletLeft": 0, "outletRight": 3, "usedBlocks": [ "xo-a", "xo-x1", "xo-b" ] },
    { "switchFilters": [ "Normal", "Reversed" ], "outletLeft": 1, "outletRight": 2, "usedBlocks": [ "xo-b", "xo-x2", "xo-a" ] }
  ],
  "signalGroups": [
    { "groupId": "xo-aeb", "signals": [ "xo-aeb" ], "allowedDirection": "Right", "allowedRoutes": [ 0, 2 ] },
    { "groupId": "xo-beb", "signals": [ "xo-beb" ], "allowedDirection": "Right", "allowedRoutes": [ 1, 3 ] },
    { "groupId": "xo-awb", "signals": [ "xo-awb" ], "allowedDirection": "Left",  "allowedRoutes": [ 0, 3 ] },
    { "groupId": "xo-bwb", "signals": [ "xo-bwb" ], "allowedDirection": "Left",  "allowedRoutes": [ 1, 2 ] }
  ]
}
```

The plant blocks `xo-a`, `xo-b`, `xo-x1` and `xo-x2` go in the crossover's module; the outlet blocks don't (see [The interlocking's own blocks](#the-interlockings-own-blocks)). The signals `xo-aeb` (eastbound on A at the west end) and so on are auto signals in the same module, each head mapped to the routes its group can take. With both crossovers normal, eastbound on A and westbound on B can be cleared at the same time; "A over to B" uses both tracks' blocks, so it clears only on its own.
