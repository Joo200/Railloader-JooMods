# 2. How CTC works

Railroader's signal system is built from a few kinds of objects. SignalsEverywhere doesn't invent new ones; it lets you create and edit the game's own. Knowing what each one does makes the JSON make sense.

## Features and modules

The signal data is grouped in two levels:

```json
{
  "BK-AJ-HW": {             <- feature
    "CN-W": { ... },        <- module
    "CN-AL": { ... }
  }
}
```

- A **feature** is a stretch of railroad that the game switches on as a unit, like the signals between Bryson, Alarka Jct and Whittier (`BK-AJ-HW`). Put your modules in the feature that already covers that stretch. A new feature name creates a new group.
- A **module** is one place: a control point, or the stretch between two of them. Its key (`CN-W`) becomes the name of the game object that holds everything inside it.

A module can contain:

| Key | What it holds |
|---|---|
| `blocks` | Track sections that detect trains. |
| `autoSignals` | Signals whose aspect comes from blocks and routes. |
| `predicateSignals` | Signals whose aspect comes from a list of conditions. |
| `interlocking` | A control point: switches, routes and exits. At most one per module. |
| `intermediate` | The automatic signals between two control points. At most one per module. |
| `crossover` | A control point that lets several routes be set at once. At most one per module. |

Ids of blocks and signals must be unique across the whole railroad, not just the module.

## Tying modules to a milestone

A feature is switched on by the game's own signal milestone for that stretch, and everything in it comes on together: modules you add to `BK-AJ-HW` appear as soon as the stock Bryson to Whittier signals do. To make your modules wait for a milestone of your own (a progression section in a `game-graph` file that unlocks a map feature), add a `map-feature-gates.json` to your mod folder:

```json
{
  "gates": [
    {
      "mapFeature": "signals-alarka",
      "fallbackMapFeature": "alarka",
      "feature": "BK-AJ-HW",
      "modules": [ "AJ-BRANCH", "AB-CN", "CN-W", "CN-M", "CN-E", "CN-AL", "AL" ]
    }
  ]
}
```

- **`mapFeature`**: the map feature your milestone unlocks. The listed modules are on only while it's unlocked, and still need the stock milestone for their feature.
- **`fallbackMapFeature`** (optional): used instead when the running game's progression has no section that unlocks `mapFeature`, for example a start your progression file doesn't add the section to. Use the map feature that builds the track, so the signals still come on with the line.
- **`feature`** and **`modules`**: the feature your modules are in, and their keys.

SignalsEverywhere itself doesn't read this file. [Tetz's SignalsEverywhere Fixes](https://www.nexusmods.com/railroader/mods/1807) 1.8.0 or later reads it from every mod folder and switches the modules with the map feature, so the file works under RailForge, FUSE and Railloader alike. Without SignalsEverywhere Fixes the file does nothing and the modules come on with their feature, so make sure that's acceptable, or list SignalsEverywhere Fixes as a requirement. In sandbox, the modules follow the map feature as set in the sandbox options.

## Blocks

A **block** is one or more stretches of track. When any car is on it, the block is **occupied**. Signals look at blocks to decide whether the way ahead is clear.

A block also shows occupied when a switch inside it is thrown by hand, or (in CTC) unlocked for hand operation. See [Locations and blocks](03-locations-and-blocks.md).

## Signals

Every signal has a **direction**, `Left` or `Right`. This is the direction of travel it governs, using the same left and right as the CTC panel: a `Right` signal is for trains moving toward the right-hand side of the panel. On the main line the panel starts at Andrews on the left, so `Right` is eastbound. For a branch, it's whichever way you draw the branch on the panel.

There are two kinds:

- **Auto signals** (`autoSignals`) work out their aspect from the blocks they protect and, inside a control point, from the route that's lined.
- **Predicate signals** (`predicateSignals`) show a proceed aspect on a head when all of that head's conditions are true: a switch position, clear blocks, a direction set at a control point.

A signal has one, two or three **heads**. The top head shows the normal route, the second the diverging route, and the third a restricting move. The combination gives the aspect the game uses (and that the auto engineer obeys): Clear, Approach, Diverging Clear, Diverging Approach, Restricting or Stop. [Signals](04-signals.md) explains how each kind decides.

## Control points

An **interlocking** is a control point. It lists:

- **Switch sets**: the switches it controls, grouped so one panel knob throws a set together.
- **Outlets**: the ways out of the control point. Each has a direction, the block just beyond it, and the next signal beyond that block.
- **Routes**: for each combination of switch positions, which outlet a train leaves by in each direction.

In CTC, the dispatcher sets a route by turning the knobs on the panel lever and pressing the code button: the switches move, a direction is set, and the signals for that direction can clear. An interlocking with no switches at all is a **signal-only control point**, used to split a line or start a section.

## Intermediates

Between two control points, the signals are automatic. An **intermediate** ties them together: the blocks in order from left to right, the signals in order from left to right, and the control point signal at each end. It lets a signal look ahead to the next one, and lets a train stop at an intermediate signal and then proceed (stop and proceed).

## Directions and traffic

In CTC, when a route is set at a control point, the blocks on that route are given a **traffic direction**. An auto signal facing the other way shows Stop. This is what keeps two trains from being let onto the same single track from opposite ends. Blocks inside an intermediate don't hold a traffic direction themselves; the control points at each end decide.

## CTC and ABS

The player can switch the railroad between **CTC** and **ABS**.

- In **CTC**, switches at control points are locked, routes come from the panel, and traffic directions apply.
- In **ABS**, there are no panel routes. Signals clear by themselves from the switch positions and block occupancy, and anyone can throw switches. Predicate conditions about a control point's direction count as true in ABS.

Test both. A signal that's right in CTC can be wrong in ABS, especially predicate signals.

## The auto engineer

The auto engineer (AE) reads the same signals. It obeys Stop, slows for Approach and Restricting, and looks ahead for the next signal. That makes it a good tester: if the AE won't leave a siding or stops for no reason, look at the signal it's facing. Two game rules worth knowing:

- In Road mode the AE never throws switches; it stops before one that's set against it ("Switch Against").
- The AE's Approach speed and how long it holds it come from the game, not the signal mod.
