# 4. Signals

## Properties every signal has

```json
"cn-ws": {
  "direction": "Left",
  "headConfiguration": "Single",
  "location": { "segmentId": "Sls0", "distance": 3.595, "end": "End" },
  "leftSide": true,
  "offset": 3,
  "modelType": "Vanilla"
}
```

| Property | Meaning |
|---|---|
| `direction` | `Left` or `Right`: the direction of travel it governs (see [How CTC works](02-how-ctc-works.md#signals)). |
| `headConfiguration` | `Single`, `Double` or `Triple`. Auto signals can't be `Triple`; SignalsEverywhere changes it to `Double` with a warning. |
| `location` | Where it stands. Measure from the end trains approach from; that sets which way it faces. See [Locations](03-locations-and-blocks.md#locations). |
| `leftSide` | `true` puts the mast on the left of the track, as seen by an approaching train. Default `false` (right). |
| `offset` | Meters from the track center, a whole number. Default 3. On the left side it's multiplied by 1.2. |
| `modelType` | Which signal model to copy. `Vanilla` (the default) uses the game's own. Another value is matched against the names of signals already in the world. |

The key is the signal's id. An `id` property inside is ignored and overwritten by the key.

Signal direction, facing and side are separate, so check all three for every signal: `direction` decides which trains it governs, `location.end` decides which way it faces, and `leftSide` decides which side of the track it stands on.

## How an aspect is built from heads

Each head shows green, yellow or red. The game combines them like this:

| Top head | Second head | Third head | Aspect |
|---|---|---|---|
| green | any | any | Clear |
| yellow | any | any | Approach |
| red | green | any | Diverging Clear |
| red | yellow | any | Diverging Approach |
| red | red | green or yellow | Restricting |
| red | red | red | Stop |

A head that may proceed shows green if the next signal on its route is showing anything but Stop, and yellow if the next signal shows Stop or there is no next signal. So **a missing next signal means Approach**, never Clear: leave `nextSignal` out only where the line really ends at a stop, like the end of signaled territory.

For an auto signal, a next signal showing a Diverging or Restricting aspect also gives yellow.

## Auto signals

```json
"autoSignals": {
  "cn-we": {
    "direction": "Right",
    "headConfiguration": "Double",
    "location": { "segmentId": "Szr5", "distance": 20.031, "end": "Start" },
    "blocks": [ "cn-w" ],
    "interlockingRouteMapping": [ 0, 1 ]
  }
}
```

- **`blocks`**: the blocks this signal protects, normally the block it faces into. If any of them is occupied, or (in CTC) has its traffic direction set against this signal, the signal shows Stop.
- **`interlockingRouteMapping`**: only for signals in a module with an interlocking. One route index per head: the top head follows route 0, the second head route 1. A head shows red unless its route is lined, then looks at that route's outlet in the signal's direction: the outlet's blocks must be clear, and its next signal decides green or yellow.

What the signal does depends on where it is:

| Module has | Signal behaves as |
|---|---|
| An interlocking | A control point signal. Leave out `interlockingRouteMapping` and it never clears (the game logs an error). |
| An intermediate | An automatic signal. It looks ahead to the next signal in the intermediate in its direction, and shows Stop when the control point at the far end has a route set against it. Use `"interlockingRouteMapping": []`. |
| Neither | Shows Approach whenever its blocks are clear. |

Extra mapping entries: SignalsEverywhere lets a `Double` auto signal list more than two routes. When the first two give Stop, it checks the extra ones and shows Diverging Approach if one of them is lined and clear. That's how a single diverging head can cover several diverging routes.

## Predicate signals

A predicate signal gives each head a list of conditions. A head may proceed when all of its conditions are true; then its `nextCtcSignal` decides green (next signal not at Stop) or yellow.

The ids in this example are made up to show the shape.

```json
"predicateSignals": {
  "el-we": {
    "direction": "Right",
    "headConfiguration": "Double",
    "location": { "segmentId": "S5xk", "distance": 12.0, "end": "Start" },
    "heads": [
      {
        "nextCtcSignal": "el-em",
        "predicates": [
          { "type": "Switch", "switchNode": "Nabc", "switchSetting": "Normal" },
          { "type": "Block", "blocks": [ "el-w", "el-mm" ] },
          { "type": "InterlockingTrafficDirection", "interlocking": "el-w", "direction": "Right" }
        ]
      },
      {
        "nextCtcSignal": "el-es",
        "predicates": [
          { "type": "Switch", "switchNode": "Nabc", "switchSetting": "Reversed" },
          { "type": "Block", "blocks": [ "el-w", "el-ms" ] },
          { "type": "InterlockingTrafficDirection", "interlocking": "el-w", "direction": "Right" }
        ]
      }
    ]
  }
}
```

Give it one entry in `heads` per head, top first.

### Predicate types

These are the game's names. Some older docs use shorter ones (`Interlocking`, `Direction`); those don't work.

| `type` | True when | Fields |
|---|---|---|
| `Switch` | The switch is in that position. | `switchNode`, `switchSetting` (`Normal` or `Reversed`) |
| `Block` | Every listed block is clear. | `blocks` |
| `InterlockingTrafficDirection` | In CTC: the control point's direction is the one given. Always true in ABS. | `interlocking`, `direction` (`Left`, `Right` or `None`) |
| `InterlockingTrafficDirectionIsNot` | In CTC: the control point's direction is not the one given, or (if you give `switchNode` and `switchSetting`) that switch isn't in that position. Always true in ABS. | `interlocking`, `direction`, optional `switchNode` + `switchSetting` |
| `AlwaysFalse` | Never. Holds a head at red. | none |

Note that `InterlockingTrafficDirection` uses `None`, `Right` and `Left`, the traffic directions, not the signal directions.

### When to use which kind

- **Auto signals** inside an interlocking are the normal choice for control point signals. The route table does the work, and traffic direction is handled for you.
- **Predicate signals** are for cases the route table can't express: a signal that depends on a switch outside the control point, or on another control point's direction. The game uses them at a few places (Bryson's west end).

A predicate signal that names a control point in another module has a load-order trap under RailForge. See [RailForge](08-railforge.md#forward-references).

## Removing a signal

Set it to `null`:

```json
"autoSignals": { "old-signal": null }
```

- **Auto signals**: safe.
- **Predicate signals**: SignalsEverywhere 1.4 crashes on a `null` predicate signal and skips the rest of that module (fixed by Tetz's SignalsEverywhere Fixes, pending upstream as PR #5). RailForge refuses the whole module instead. Rather than removing a predicate signal, rewrite it (or move it with `$moveTo`, but never a control point's signal; see [RailForge](08-railforge.md#forward-references)). See [Patching](07-patching.md#known-problems).

## Signals are rebuilt, not edited

When a patch touches a signal at all, SignalsEverywhere deletes the game object and builds a new one. Anything that pointed at the old one keeps pointing at a deleted object unless it is rebuilt too. That includes interlocking outlets (`nextSignal`) and predicate heads (`nextCtcSignal`) in other modules. Such a stale pointer counts as "no next signal", so the signal behind it is stuck at Approach.

So whenever you change a signal, also touch everything that names it. [Patching](07-patching.md#what-gets-rebuilt) has the full rule.
