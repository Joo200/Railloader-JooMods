# Joo's Railroader Mods

This repository contains a collection of mods for the game Railroader.

## Mods Overview

### [TimeSync Mod](TimeSyncMod/README.md)
A small mod that syncs game time across clients.
- Automatically syncs every 10 minutes.
- Adds `/timesync` command for manual synchronization.

### [SignalsEverywhere](SignalsEverywhere/README.md)
Extends the game's signaling logic with custom blocks, signals, and interlocking behaviors via JSON configuration.
- Detailed documentation can be found in the [SignalsEverywhere/docs](SignalsEverywhere/docs/) folder.

### [ColorPatcher](ColorPatcher/docs/README.md)
Allows for patching colors within the game.

### [InterchangeReloader](InterchangeReloader/docs/README.md)
Provides functionality related to reloading interchanges.

### [NotEnoughRosters](NotEnoughRosters/)
A mod to address roster limitations.

---

## Project Setup

In order to get going with these mods, follow these steps:

1. Get a copy of this repository.
2. Copy `Paths.user.example` to `Paths.user`, open it, and set the `<GameDir>` to your game's directory.
3. Open the `JooMods.sln` solution.
4. You're ready!

## Facts & Behaviours

- **Multi-project solution**: This solution is multi-project/multi-mod-able; i.e. you can have multiple projects inside the solution, which all produce individual mods.
- **Auto-deployment**: Builds will always land directly in the correct folder (i.e. `GameDirectory/Mods/_AssemblyName_`).
- **Assembly references**: You can reference assemblies in the game directory (i.e. `Railroader_Data/Managed`) directly and conveniently by using `<GameAssembly Include="" />`.
- **Versioning**: Unless you specify an `<AssemblyVersion>` yourself, it will automatically generate a version based on `<MajorVersion>`, `<MinorVersion>`, and the current year, day of year, and time.

## Development & Publishing

### During Development
Make sure you're using the _Debug_ configuration. Every time you build your project, the files will be copied to your Mods folder and you can immediately start the game to test it.

### Publishing
Make sure you're using the _Release_ configuration. The build pipeline will:
1. Ensure it's a proper release build without debug symbols.
2. Replace `$(AssemblyVersion)` in the `Definition.json` with the actual assembly version.
3. Create a zip file in `bin` with a ready-to-extract structure.
