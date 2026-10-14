# 10. Worked examples

These come from real mods (Tetz's Alarka Branch Signals and Branch Junctions). Locations are trimmed to keep the focus on the structure; the full files are in those mods.

## Example 1: a signal-only control point

Alarka Jct is yard limits, and the Alarka Branch leaves from its wye. A signal-only control point (`ajy`) marks where signaled territory starts: trains leaving the yard get a signal onto the branch, and trains coming off the branch get a signal into the yard.

```json
{
  "BK-AJ-HW": {
    "AJ-BRANCH": {
      "$replace": {
        "interlocking": {
          "id": "ajy",
          "displayName": "Alarka Jct Yard Limit",
          "switchSets": [],
          "outlets": [
            { "direction": "Left",  "blocks": [],         "nextSignal": null },
            { "direction": "Right", "blocks": [ "ab-cn" ], "nextSignal": "cn-we" }
          ],
          "routes": [
            { "switchFilters": [], "outletLeft": 0, "outletRight": 1 }
          ]
        },
        "blocks": {
          "ajy": { "spans": [ { "lower": { ... }, "upper": { ... } } ] }
        },
        "autoSignals": {
          "ajy-ee": { "direction": "Left",  "headConfiguration": "Single", "blocks": [ "ajy" ], "interlockingRouteMapping": [ 0 ], "location": { ... } },
          "ajy-wm": { "direction": "Right", "headConfiguration": "Single", "blocks": [ "ajy" ], "interlockingRouteMapping": [ 0 ], "location": { ... } }
        },
        "predicateSignals": {}
      }
    }
  }
}
```

Points to notice:

- **No switches, one route.** `switchSets` and `switchFilters` are empty, so route 0 is always lined.
- **The yard side has no blocks and no next signal.** Westbound (`Left`) trains go into yard limits, where there's nothing to detect. `ajy-ee` can only show Approach, which is right: the train is entering yard limits.
- **The OS block `ajy`** is a short block between the two signals. Both signals protect it, so a train standing there holds both at Stop.
- **The whole module is `$replace`d.** This mod replaced a stock module with a different design, so it rewrites it from scratch instead of patching piece by piece. `$replace` touches everything, which also avoids the touch problems from [Patching](07-patching.md).
- **`ab-cn` is shared.** It's the single track between `ajy` and Cochran West. It's listed in `ajy`'s right outlet and in `cn-w`'s left outlet, and lives in its own module (`AB-CN`) with no intermediate, because there are no signals between the two control points.

## Example 2: a passing siding

Cochran has a passing siding with a control point at each end and nothing else. Four modules:

| Module | Holds |
|---|---|
| `CN-W` | Interlocking `cn-w` (switch `Nuam`), OS block `cn-w`, signals `cn-we`, `cn-wm`, `cn-ws`. |
| `CN-M` | Blocks `cn-mm` (main) and `cn-ms` (siding). No control point. |
| `CN-E` | Interlocking `cn-e` (switch `Natz`), OS block `cn-e`, signals `cn-ee`, `cn-em`, `cn-es`. |
| `CN-AL` | The intermediate to Alarka. |

The two interlockings mirror each other:

```json
"cn-w": {
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
"cn-e": {
  "switchSets": [ [ "Natz" ] ],
  "outlets": [
    { "direction": "Right", "blocks": [ "cn-al-1" ], "nextSignal": "cn-al-eb" },
    { "direction": "Left",  "blocks": [ "cn-mm" ],   "nextSignal": "cn-wm" },
    { "direction": "Left",  "blocks": [ "cn-ms" ],   "nextSignal": "cn-ws" }
  ],
  "routes": [
    { "switchFilters": [ "Normal" ],   "outletLeft": 1, "outletRight": 0 },
    { "switchFilters": [ "Reversed" ], "outletLeft": 2, "outletRight": 0 }
  ]
}
```

And the signals at `cn-w`:

| Signal | Direction | Heads | Mapping | Stands |
|---|---|---|---|---|
| `cn-we` | Right | Double | `[0, 1]` | On the single track, facing the points. Top head for the main, second for the siding. |
| `cn-wm` | Left | Single | `[0]` | On the main, facing the frog. |
| `cn-ws` | Left | Single | `[1]` | On the siding, facing the frog, on the left (`"leftSide": true`) because the siding is on that side. |

Points to notice:

- **Each outlet's next signal is the signal at the far end of that track.** A train leaving `cn-w` eastbound on the siding meets `cn-es` next, so outlet 2's next signal is `cn-es`, and `cn-we`'s second head looks at it.
- **The middle blocks belong to both control points.** `cn-mm` and `cn-ms` are outlets of both, which is how a route set at one end blocks an opposing route from the other.
- **Route numbers are per interlocking.** Route 1 at `cn-w` is the siding; at `cn-e` it's also the siding, but only because the routes were listed the same way.

### Its panel section

```json
{ "panel": { "Alarka Branch": [
  { "$add": { "Type": "Light", "Block": "cn-w", "X": 4, "Y": 2, "Color": "red", "ShowTrack": false,
              "Interlock": { "Interlock": "cn-w", "SwitchLabels": [ "CW" ], "SignalLabel": "CW" } } },
  { "$add": { "Type": "SwitchRightBottom", "X": 4, "Y": 2, "Id": "CW" } },
  { "$add": { "Type": "Label", "Id": "Cochran", "X": 5, "Y": 0 } },
  { "$add": { "Type": "Light", "Block": "cn-mm", "X": 5, "Y": 2 } },
  { "$add": { "Type": "Light", "Block": "cn-ms", "X": 5, "Y": 3 } },
  { "$add": { "Type": "Light", "Block": "cn-e", "X": 6, "Y": 2, "Color": "red", "ShowTrack": false,
              "Interlock": { "Interlock": "cn-e", "SwitchLabels": [ "CE" ], "SignalLabel": "CE" } } },
  { "$add": { "Type": "SwitchLeftBottom", "X": 6, "Y": 2, "Id": "CE" } }
] } }
```

- The siding is drawn below the main (row 3), so the switches branch to the lower right at the west end and come back from the lower left at the east end.
- The two levers sit two columns apart (X 4 and 6), the minimum for one-knob levers.

## Example 3: adding a junction to an existing control point

The Robinson Gap mine lead joins the main line just west of Alarka Jct West (`aj-w`). A real railroad would make the new switch part of the existing control point, worked from the same lever. This needs Tetz's SignalsEverywhere Fixes, because `aj-w` shares a module with `aj-e` in the base game; the Fixes mod moves it into its own module, `AJ-W`.

### The interlocking: add a switch set, double the routes

```json
"AJ-W": {
  "interlocking": {
    "switchSets": { "$replace": [ [ "Nowf" ], [ "Nit5" ] ] },
    "outlets": { "$replace": [
      { "direction": "Left",  "blocks": [ "rg-bk" ],    "nextSignal": "aj-bk-w" },
      { "direction": "Left",  "blocks": [ "rg-mine" ],  "nextSignal": null },
      { "direction": "Right", "blocks": [ "aj-mm" ],    "nextSignal": "aj-em" },
      { "direction": "Right", "blocks": [ "aj-bk-co" ], "nextSignal": null }
    ] },
    "routes": { "$replace": [
      { "switchFilters": [ "Normal",   "Normal" ],   "outletLeft": 0, "outletRight": 2 },
      { "switchFilters": [ "Normal",   "Reversed" ], "outletLeft": 1, "outletRight": 2 },
      { "switchFilters": [ "Reversed", "Normal" ],   "outletLeft": 0, "outletRight": 3 },
      { "switchFilters": [ "Reversed", "Reversed" ], "outletLeft": 1, "outletRight": 3 }
    ] }
  }
}
```

- The stock switch `Nowf` stays set 0, so the stock signals' route numbers keep their meaning for the first switch. The new switch `Nit5` is set 1.
- Two switches, two positions each: four routes. Each route picks a left outlet by the new switch and a right outlet by the old one.
- Everything is `$replace`, never `$find`, so the change counts as a touch (see [Known problems](07-patching.md#known-problems)).

### The signals: new blocks, new mappings

```json
"AJ-W": {
  "blocks": {
    "rg": { "spans": [ ... two spans, one per leg of Nit5 ... ] }
  },
  "autoSignals": {
  "aj-wm": { "headConfiguration": "Double",
             "blocks": { "$replace": [ "aj-w", "rg" ] },
             "interlockingRouteMapping": { "$replace": [ 0, 1 ] } },
  "aj-ws": { "headConfiguration": "Double",
             "blocks": { "$replace": [ "aj-w", "rg" ] },
             "interlockingRouteMapping": { "$replace": [ 2, 3 ] } },
  "RG_entry": { "direction": "Right", "headConfiguration": "Double",
                "blocks": [ "rg", "aj-w" ], "interlockingRouteMapping": [ 1, 3 ],
                "location": { "segmentId": "Sjdl", "distance": 20.49, "end": "Start" } }
  }
},
"AJ-BK": {
  "blocks": {
    "rg-bk":   { "spans": [ ... the main west of the junction ... ] },
    "rg-mine": { "spans": [ ... the mine lead ... ] }
  }
}
```

- The junction's track is a second OS block, `rg`. Every signal at the control point now protects both OS blocks, so a train on either switch holds all of them.
- The new outlet blocks `rg-bk` and `rg-mine` are defined in the plain module `AJ-BK`, not in `AJ-W`. In `AJ-W` they would be OS blocks: a train approaching on the main or coming down the mine lead would cancel the route, and the dispatcher couldn't code one while it waited (see [The interlocking's own blocks](05-control-points.md#the-interlockings-own-blocks)). An earlier version of this example made that mistake.
- The westbound signals `aj-wm` and `aj-ws` become double: top head for the main to Bryson, second head for the mine.
- `RG_entry` is the new signal on the mine lead, for trains coming out onto the main.
- The westbound home signal `aj-we` stands west of the new switch, so it's moved there and its predicate heads are rewritten to include `Nit5`.
- The old block between Alarka Jct and Bryson is shortened and the new block `rg-bk` takes over its east end. The intermediate on that stretch (`AJ-BK`) is patched to list `rg-bk` and to name the moved `aj-we` as its right-hand end signal, and the intermediate signal facing east (`aj-bk-e`) now protects `rg-bk`.

### The panel: two knobs on one lever

```json
{ "panel": { "Mainline": [
  { "$find": [ { "path": "Type", "value": "Light" },
               { "path": "X", "value": 39 },
               { "path": "Y", "value": 2 } ],
    "$replace": { "Type": "Light", "Block": "aj-w", "X": 39, "Y": 2,
                  "Color": "red", "ShowTrack": false } },
  { "$add": { "Type": "Light", "Block": "rg", "X": 37.6, "Y": 2, "Color": "red", "ShowTrack": false,
              "Interlock": { "Interlock": "aj-w",
                             "VanillaSwitchKnobIds": [ "35", "aj-w-1" ], "VanillaDirKnobId": "36",
                             "SwitchLabels": [ "RG", "35" ], "SignalLabel": "36",
                             "KnobOrder": [ 1, 0 ] } } },
  { "$add": { "Type": "SwitchLeftBottom", "X": 37.6, "Y": 2, "Id": "RG" } }
] } }
```

- The stock `aj-w` light is replaced by a copy without its `Interlock`, which takes the old lever away from it.
- The lever moves onto the new junction's light. A two-knob lever needs more room, and this puts it between existing columns using fractional `X`.
- `VanillaSwitchKnobIds` keeps the stock knob's id (`35`) so saved switch state still lines up, and gives the new knob its own id.
- `KnobOrder` draws the new switch's knob first, so the knobs read left to right like the track.
- `$find` matches on `Type`, `X` and `Y`, which every element has.
