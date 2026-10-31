# Signals Everywhere

Signals Everywhere is a comprehensive signaling and traffic control mod for Railroader. It extends the game's signaling logic, allowing you to define custom signaled sections, blocks, and complex interlocking behaviors. Whether you want to add signals to a new branch or create a fully functional Centralized Traffic Control (CTC) system, Signals Everywhere provides the tools to do it.

## Key Features

-   **Custom Signal Placement**: Define new automatic and predicate-based signals anywhere on your layout.
-   **Advanced Block Detection**: Define physical track occupation areas (Blocks) for precise train detection.
-   **Interlocking Management**: Create complex junctions and routing logic to manage traffic through busy areas.
-   **CTC Panel**: A fully functional, interactive Centralized Traffic Control panel. Monitor block occupancy, throw switches, and clear signals from a centralized window.
-   **New Signal Types**: Includes support for multi-head signals (Single, Double, Triple) with customizable logic.
-   **Configurable via JSON**: All signals, blocks, and panels are defined in easy-to-edit `json` files.

---

## 1. Getting Started

This mod allows you to extend the game's signaling logic by defining physical track occupation areas (Blocks), signal placements, and the logic that connects them (Intermediates and Interlockings) using the signals mixins.

Declare the mixin in your mod's `Definition.json` file like this:
```json
{
  "id": "ExampleMod",
  ...
  "mixintos": {
    "game-graphs": [ "file(my-game-graph.json)"],
    "signals": [ "file(my-signals.json)" ],
    "ctcPanel": [ "file(my-ctc-panel.json)" ]
  }
}
```

### Railroader 2026.1

On Railroader 2026.1 there is no Railloader: SignalsEverywhere reads the mixintos itself, from the mods the game has
loaded. Give your mod an `Info.json` (the game's own mod file) that requires SignalsEverywhere, and keep the
`Definition.json` above beside it (or put the same `mixintos` object into `Info.json`):
```json
{
  "identifier": "you/your-signals",
  "version": "1.0.0",
  "name": "Your Signals",
  "dependencies": { "required": ["rr/game", "joo/signals-everywhere"] }
}
```
Mixintos apply in dependency order (a mod after the mods it requires or loads after), as Railloader ordered them.
Track ids follow 2026.1's map. The console command `/signaldebug` has `dump signals|panel`, `reload` (signal
definitions), `rebuild-panel` and `show-panel`. Building for 2026.1: `dotnet build -p:RR=2026` (see `Paths.user`).

**Signal milestones (2026.1).** A signal mod can add its own milestone and map feature with a `milestones.json` in its
folder (2026.1 milestones are scene objects, out of reach of data files and Relay). The milestone is a delivery
milestone like the game's own signal milestones; its delivery site (a `ProgressionIndustryComponent`) comes from the
mod's map data, e.g. a Relay `Data.rl` `modifyMap`. A milestone whose prerequisites, map features or delivery site the
running progression doesn't have is left out. Tie the signal modules to the feature with a `map-feature-gates.json`.
```json
{
  "mapFeatures": [ { "identifier": "signals-alarka", "displayName": "Signals - Alarka Branch", "defaultEnableInSandbox": true } ],
  "milestones": [ {
    "identifier": "you-sig-alarka", "progressions": [ "ewh" ], "title": "Signals: Alarka Branch",
    "description": "Extend signals up the Alarka Branch.", "cost": 2500,
    "prerequisites": [ "sig-hw-bk", "alarka-branch" ], "enableFeaturesOnComplete": [ "signals-alarka" ],
    "industryComponentId": "your-site.site-signals",
    "deliveries": [ { "loadId": "rr/signal-parts", "carTypeFilter": "XM*", "count": 3 } ]
  } ]
}
```

### File Structure

The configuration is organized into a hierarchy:
1.  **Section (Group)**: A high-level collection of related modules (e.g., "Mainline-East").
2.  **Module**: A logical unit representing a specific location, like an interlocking or a stretch of track.
3.  **Components**: Inside each module, you define components such as blocks, signals, and logic.

```json
{
  "signals": {
    "YourSectionName": {
      "YourModuleName": {
        "blocks": { ... },
        "autoSignals": { ... },
        "interlocking": { ... },
        "intermediate": { ... },
        "crossover": { ... }
      }
    }
  }
}
```

---

## 2. Centralized Traffic Control (CTC)

The CTC Panel is a core feature of Signals Everywhere. It provides a schematic view of your railroad, allowing for remote operation.

-   **Interactive Schematic**: View real-time occupancy of blocks and the status of signals and switches.
-   **Remote Control**: Throw switches and set signal directions directly from the panel.
-   **Custom Layouts**: Define your own CTC panel layouts to match your specific track arrangements using `ctc-panel-layout.json`.
-   **System Modes**: Switch between ABS (Automatic Block Signaling) and CTC modes via the mod options.

---

## 3. Component Documentation

Detailed documentation for each component can be found in the `docs/` directory:

*   [**Blocks**](docs/blocks.md): Define track segments for train detection.
*   [**Signals**](docs/signals.md): Define automatic and predicate-based signals.
*   [**Interlockings**](docs/interlockings.md): Manage complex junctions and routing.
*   [**Intermediates**](docs/intermediates.md): Group blocks and signals between interlockings.
*   [**Crossovers**](docs/crossovers.md): Manage connections between parallel tracks.

---

## 4. Summary Tips

1.  **Unique IDs**: Ensure every `block` and `signal` has a unique ID across the entire file.
2.  **Directionality**: `Left` and `Right` are relative to the track's internal direction. If a signal is facing "the wrong way," check the `direction` and `location.end` properties.
3.  **Module Scope**: You can reference blocks and signals across modules, but it's best practice to keep related items in the same section for organization.

---

## 5. FAQ

### Can you help me setting up signals on my branch?

No. I don't have the time to write signal support for every branch. You can ask questions in the [discord thread for my mods](https://discordapp.com/channels/795878618697433097/1453773453810466816).

### Can I look into an example for signals? How can I report bugs?

This signal mod was developed for the branch [Macon County](https://nexusmods.com/). You can download it to see an example of a complete `signals.json` and `CTCPanel-MainLine.json` configuration.
Reports bugs on the [GitHub Issues](https://github.com/Joo200/Railloader.TimeSync/issues) page.

### Can I modify existing signals from the base game?

That's currently not possible. It will be possible in a future update but there is no ETA for it.

### Is it possible to add custom models to signals?

I hope we can add other signal models at some point in the future. If you have any ideas or suggestions, feel free to share them in the discord thread or direct message me.

### Can I see block positions?

There is a drop down list in the mod options to show block positions.

### Blocks don't detect trains, how to fix it?

Make sure the block has correct locations. Try to switch the `end` value of one end position.

