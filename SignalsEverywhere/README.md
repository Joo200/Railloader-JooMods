# Railroader CTC Signal Creation Guide

This guide explains how to define new signaled sections, blocks, and interlocking behaviors using the `signals.json` configuration file. This system allows you to extend the game's signaling logic by defining physical track occupation areas (Blocks), signal placements, and the logic that connects them (Intermediates and Interlockings).

---

## 1. File Structure

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

## 2. Component Documentation

Detailed documentation for each component can be found in the `docs/` directory:

*   [**Blocks**](docs/blocks.md): Define track segments for train detection.
*   [**Signals**](docs/signals.md): Define automatic and predicate-based signals.
*   [**Interlockings**](docs/interlockings.md): Manage complex junctions and routing.
*   [**Intermediates**](docs/intermediates.md): Group blocks and signals between interlockings.
*   [**Crossovers**](docs/crossovers.md): Manage connections between parallel tracks.

---

## 3. Summary Tips

1.  **Unique IDs**: Ensure every `block` and `signal` has a unique ID across the entire file.
2.  **Directionality**: `Left` and `Right` are relative to the track's internal direction. If a signal is facing "the wrong way," check the `direction` and `location.end` properties.
3.  **Module Scope**: You can reference blocks and signals across modules, but it's best practice to keep related items in the same section for organization.

---

## 4. FAQ

### Can you help me setting up signals on my branch?

No. I don't have the time to write signal support for every branch. You can ask questions in the [discord thread for my mods](https://discordapp.com/channels/795878618697433097/1453773453810466816).

### Can I look into an example for signals? How can I report bugs?

TODO: -- Insert the correct link here. 
This signal mod was developed for the branch [Macon County](https://nexusmodes.com/). You can download and look into the signals file.

### Can I modify existing signals from the base game?

That's currently not possible. It will be possible in a future update but there is no ETA for it.

### Is it possible to add custom models to signals?

I hope we can add other signal models at some point in the future. If you have any ideas or suggestions, feel free to share them in the discord thread or direct message me.

### Can I see block positions?

There is a drop down list in the mod options to show block positions.

### Blocks don't detect trains, how to fix it?

Make sure the block has correct locaitons. Try to switch the `end` value of one end position.

