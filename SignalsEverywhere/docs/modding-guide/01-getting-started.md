# 1. Getting started

## What you need

- **Railroader** with the signal system available: in company mode the signal progressions must be bought; in sandbox, the CTC map feature must be on. SignalsEverywhere builds signals when the CTC panel exists, so nothing happens before that.
- **A mod loader that runs SignalsEverywhere**, one of:
  - Railloader with StrangeCustoms;
  - RailForge;
  - FUSE.
  RailForge and FUSE each replace both Railloader and StrangeCustoms, so don't install those alongside them.
- **SignalsEverywhere**.
- A text editor that understands JSON. SignalsEverywhere ships JSON schemas (`signals.schema.json`, `ctc-panel-layout.schema.json`) in its mod folder. Point your file's `$schema` at them for hints; SignalsEverywhere removes the `$schema` key before it reads the file. Be aware that the schemas in SignalsEverywhere 1.4 reject some valid data, so treat schema errors as hints, not proof.

## The mod definition

A signal mod is a data-only mod: a folder in `Railroader/Mods` with a `Definition.json` and your JSON files. Keep it data only: under FUSE, a folder that also has a Unity Mod Manager entry (an `info.json` with a DLL) doesn't get its `signals` and `ctcPanel` files passed to SignalsEverywhere, so its signals never appear. If your mod needs code, ship the code as a separate mod. SignalsEverywhere reads two mixinto types:

- `signals`: blocks, signals and control points.
- `ctcPanel`: the CTC panel layout.

```json
{
  "manifestVersion": 5,
  "id": "yourname.my.signals",
  "name": "My Signals",
  "version": "1.0.0",
  "requires": [ { "id": "Joo.SignalsEverywhere" } ],
  "loadAfter": [ { "id": "Joo.SignalsEverywhere" } ],
  "mixintos": {
    "signals": [ "file(signals.json)" ],
    "ctcPanel": [ "file(ctc-panel.json)" ]
  }
}
```

- **`loadAfter`** controls the order patches are applied. List SignalsEverywhere and every mod whose signals you change or build on, so their changes are already there when yours are applied.
- A mixinto can be one string or a list. A list entry can also be an object with its own `requires`, for a file that only applies when another mod is installed (example below).
- You can split `signals` across several files. They are applied in the order listed. A second file is useful when one patch needs something an earlier one creates (see `$moveTo` in [Patching](07-patching.md)).

A mixinto that only applies when another mod is installed:

```json
"game-graph": [ { "mixinto": "file(signs.json)", "requires": [ "ALW.SceneryAssets" ] } ]
```

## How SignalsEverywhere uses your files

When the map loads, SignalsEverywhere:

1. Reads every signal object the game already has and writes it out as one big JSON document. This is the **original data**.
2. Applies every mod's `signals` files to that document, in load order. The result is the **patched data**.
3. Rebuilds only the parts of the railroad a patch actually changed. It records which paths each patch touched; anything untouched is left exactly as the game built it.

The CTC panel works the same way, starting from SignalsEverywhere's own `CTCPanel-MainLine.json`.

This has two consequences you'll meet again and again:

- **Your file is a patch, not a full copy.** You only write what you add or change.
- **"Touched" matters.** A change that doesn't count as touching something doesn't reach the game, and touching some things has side effects. [Patching](07-patching.md) covers both.

## The edit–test loop

1. **Dump what's there.** Run `/signaldebug dump signals` in the game console (host only). SignalsEverywhere writes `signal-old.json` (original data) and `signal-patched.json` (after all mods) into its own mod folder. `/signaldebug dump panel` writes `panel-dump.json`. These are your map: real ids, real locations and the exact structure to patch.
2. **Write your patch.**
3. **Reload.** SignalsEverywhere's mod tab has **Reload Signal Definitions** and **Rebuild CTCPanel** buttons, so small changes don't need a restart. Restart the game when something looks stale.
4. **Check it.** Dump again and compare `signal-patched.json` with what you meant. Then check the game itself: patched data is not proof that a change reached the game (see [Testing and debugging](09-testing-and-debugging.md)).
5. **Drive it.** Run a train through in both directions, in CTC and in ABS.

## Finding track ids

Everything is placed by track **segment** ids (like `S4u5`) and switch **node** ids (like `Nuam`). The dumps give you every id the game's signals already use. For new places you need the track graph: get it from a track graph export, or from the `game-graph` file of a mod that changes that area.

Track ids mean nothing to a reader, so in your notes and commit messages always pair them with a plain name, like `Nuam (Cochran West switch)`.
