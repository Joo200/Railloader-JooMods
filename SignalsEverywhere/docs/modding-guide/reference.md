# Quick reference

## `signals` file

```text
{ "<feature>": { "<module>": {
    "blocks":           { "<id>": Block | null },
    "autoSignals":      { "<id>": AutoSignal | null },
    "predicateSignals": { "<id>": PredicateSignal | null },
    "interlocking":     Interlocking,
    "intermediate":     Intermediate,
    "crossover":        Crossover
} | null } }
```

### Location

| Field | Type | |
|---|---|---|
| `segmentId` | string | Track segment id. |
| `distance` | number | Meters from `end`. |
| `end` | `Start` \| `End` | End measured from. A signal faces this end. |

### Block

| Field | Type | Default | |
|---|---|---|---|
| `spans` | list of `{ lower: Location, upper: Location }` | | Track covered. |
| `thrownSwitchesSetOccupied` | bool | `true` | A hand-thrown switch inside shows the block occupied. |
| `createInverse` | bool | `false` | Also create `<id>-inv` with the opposite traffic direction. |

### Every signal

| Field | Type | Default | |
|---|---|---|---|
| `direction` | `Left` \| `Right` | | Direction of travel governed. |
| `headConfiguration` | `Single` \| `Double` \| `Triple` | | Auto signals: no `Triple`. |
| `location` | Location | | Measure from the end trains approach from. |
| `leftSide` | bool | `false` | Mast on the left. |
| `offset` | integer | `3` | Meters from track center (×1.2 on the left). |
| `modelType` | string | `Vanilla` | Model to copy. |

### AutoSignal (plus every-signal fields)

| Field | Type | |
|---|---|---|
| `blocks` | list of block ids | Protected blocks. Occupied or traffic against → Stop. |
| `interlockingRouteMapping` | list of route indices | One per head, in a control point. `[]` in an intermediate. |

### PredicateSignal (plus every-signal fields)

| Field | Type | |
|---|---|---|
| `heads` | list of `{ nextCtcSignal, predicates }` | One per head, top first. |
| `nextCtcSignal` | signal id \| null | Decides green or yellow. |
| `predicates` | list of Predicate | All must be true. |

| Predicate `type` | Fields |
|---|---|
| `Switch` | `switchNode`, `switchSetting` (`Normal` \| `Reversed`) |
| `Block` | `blocks` |
| `InterlockingTrafficDirection` | `interlocking`, `direction` (`None` \| `Left` \| `Right`) |
| `InterlockingTrafficDirectionIsNot` | `interlocking`, `direction`, optional `switchNode` + `switchSetting` |
| `AlwaysFalse` | none |

In a crossover module, a predicate can add `crossoverGroup`.

### Interlocking

| Field | Type | |
|---|---|---|
| `id` | string | Unique railroad-wide. |
| `displayName` | string | Shown to the player. |
| `switchSets` | list of lists of node ids | One knob per set. |
| `outlets` | list of `{ direction, blocks, nextSignal }` | Ways out; direction is of travel leaving. |
| `routes` | list of `{ switchFilters, outletLeft, outletRight }` | `switchFilters`: `Normal` \| `Reversed` \| `None`, one per switch set. |

### Intermediate

| Field | Type | |
|---|---|---|
| `id` | string | Use the module key. |
| `blocks` | list of block ids | Left to right. |
| `signals` | list of signal ids | Left to right, both directions. |
| `signalLeft`, `signalRight` | signal id | Control point signal past each end. |

### Crossover

Interlocking fields, plus `routes[].usedBlocks` (list of block ids) and `signalGroups`: list of `{ groupId, signals, allowedDirection, allowedRoutes }`.

### Aspects

| Top | Second | Third | Aspect |
|---|---|---|---|
| green | | | Clear |
| yellow | | | Approach |
| red | green | | Diverging Clear |
| red | yellow | | Diverging Approach |
| red | red | not red | Restricting |
| red | red | red | Stop |

A head that may proceed: green if its next signal isn't at Stop, yellow if it is or there is none.

## `ctcPanel` file

```text
{ "panel": { "<section>": [ Element, ... ] } }
```

| Element field | |
|---|---|
| `Type` | `Label`, `Light`, `Track`, `TrackRise`, `TrackFall`, `SwitchLeftTop`, `SwitchRightTop`, `SwitchLeftBottom`, `SwitchRightBottom` |
| `X` | Column order (squashed to 1, 2, 3, …). |
| `Y` | Row. |
| `Block` | Light: block shown. Label: text. |
| `Id` | Label text; switch highlight id. |
| `Color` | `white` \| `red` |
| `ShowTrack` | Light: draw track behind. Default `true`. |
| `SwitchLabel`, `LabelOffsetX`, `LabelOffsetY` | Blue label on a switch piece. |
| `Interlock` | `{ Interlock, SwitchLabels, SignalLabel, KnobOrder, VanillaSwitchKnobIds, VanillaDirKnobId }` |
| `Crossover` | `{ Crossover, SwitchKnobOrder, SignalKnobOrder, SwitchLabels, SignalLabels }` |

## Patch instructions

| On | Instruction | |
|---|---|---|
| object | `$replace: value` | Replace it. |
| object | `$moveTo: "['a']['b']"` | Move into an existing object. |
| property | `$remove: true` | Remove (fails on object values in 1.4). |
| array item | `$add: value` | Append one. |
| array item | `$append: [values]` | Append several. |
| array item | `$find: [conditions]` | Merge into first match. |
| array item | `$index: n` | Merge into item n. |
| array item | `$selectAll: true` | With `$find`: every match. |
| array item | `$replace: value` | With `$find`/`$index`: replace the match. |
| array item | `$remove: true` | With `$find`/`$index`: remove the match. |
| array item | `$clone: true` | With `$find`/`$index`: copy, merge, append. |
| array item | `$optional: true` | With `$find`/`$index`: no match is fine. |

Condition: `{ "path": "...", "comparison": "Equals" | "NotEquals" | "StartsWith" | "EndsWith" | "Contains", "value": ... }`. Text compares ignore case. Every item must have the path.

## Rules of thumb

- Touch a module's interlocking/intermediate/crossover whenever you patch the module.
- After `$find`/`$index`/`$add` in an existing array, add a harmless `$replace` on the same component.
- When you change a signal, touch every outlet and predicate head that names it.
- Don't `null` predicate signals; move or rewrite them.
- Signals naming another control point go in a module after it.
- Keep `$find` on fields every item has.
- Check the game, not just `signal-patched.json`. Under RailForge, read `## Signal authoring`.
