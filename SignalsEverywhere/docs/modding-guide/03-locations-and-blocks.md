# 3. Locations and blocks

## Locations

A location is a point on one track segment:

```json
{ "segmentId": "S4u5", "distance": 72.242, "end": "End" }
```

- **`segmentId`**: the segment.
- **`end`**: `Start` or `End`, the end of the segment you measure from.
- **`distance`**: meters from that end.

The same point can be written from either end: on a 100 m segment, `20 from Start` is `80 from End`.

**For signals, the end you measure from also sets which way the signal faces.** The signal faces the end you measured from, so it's read by trains coming from that end. Measure a signal's location from the end trains approach it from.

A location must lie inside its segment. A distance longer than the segment is an error (RailForge refuses the whole module), and so is a segment id that doesn't exist. If another mod changes the track, segment lengths and ids can change under you. See [RailForge](08-railforge.md).

## Blocks

```json
"blocks": {
  "cn-w": {
    "spans": [
      {
        "lower": { "segmentId": "Szr5", "distance": 21.031, "end": "Start" },
        "upper": { "segmentId": "S3xa", "distance": 4.707,  "end": "End" }
      },
      {
        "lower": { "segmentId": "Szr5", "distance": 21.031, "end": "Start" },
        "upper": { "segmentId": "Sls0", "distance": 4.595,  "end": "End" }
      }
    ],
    "thrownSwitchesSetOccupied": true
  }
}
```

### Spans

Each **span** covers the track between two locations, `lower` and `upper`, following the track between them. A span can cross segments and switches. A block over a turnout needs one span per leg: the example above is the Cochran West switch, one span from the main line through the points to each leg.

If a block never shows occupied, a span's ends usually don't connect along the track. Check both ends, and try measuring one of them from the other end of its segment.

### Placing block boundaries

Signals stand at block boundaries. A signal protects the block it faces into, so put the signal just before that block's boundary, on the approach side. We use 1 m:

```text
       signal cn-we (Right) at Szr5 20.031 from Start
              v
--------------|--+=============== block cn-w ===========>
                 ^
       block boundary at Szr5 21.031 from Start
```

Blocks next to each other should meet at the same point, or overlap slightly. A gap leaves a car undetected.

### Thrown and unlocked switches

`thrownSwitchesSetOccupied` (default `true`): when a switch inside the block is thrown by hand, the block shows occupied until the switch is set back. In CTC, a switch unlocked for hand operation also shows its block occupied.

- Keep it `true` for blocks on the main line or sidings, so a hand-thrown switch stops trains.
- When you patch a module's interlocking or crossover, SignalsEverywhere sets it to `false` on every block listed in that module, since those switches are thrown by the control point and are part of the route.

### Inverse blocks

`createInverse: true` makes a second block named `<id>-inv` covering the same track, whose traffic direction is always the opposite of the first. It's for tracks that two control points see from opposite sides. Most mods don't need it.

### Removing a block

Set it to `null` in a patch:

```json
"blocks": { "old-block": null }
```

Anything that still names it (a signal, an outlet, an intermediate) needs changing too, or the game logs a warning and the reference is dropped.

## Seeing blocks in game

SignalsEverywhere's mod tab has a **Blocks** dropdown. Pick a block and its spans are highlighted on the track. Use it to check every new block before you test with trains.
