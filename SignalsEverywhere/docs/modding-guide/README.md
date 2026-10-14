# SignalsEverywhere Modding Guide

A guide to adding and changing signals in Railroader with SignalsEverywhere.

SignalsEverywhere lets a mod add blocks, signals, control points and CTC panel levers, or change the ones the game ships with, all from JSON files. The [reference pages](../) one level up list the properties. This guide explains how the pieces fit together, how patching works, and the traps that cost us hours, so you don't have to find them the hard way.

It's written for mod authors who are comfortable editing JSON. You don't need to know C#.

## Contents

1. [Getting started](01-getting-started.md): what you need, the mod definition, and the edit–test loop.
2. [How CTC works](02-how-ctc-works.md): blocks, signals, control points, intermediates and directions.
3. [Locations and blocks](03-locations-and-blocks.md): placing things on the track.
4. [Signals](04-signals.md): auto signals, predicate signals, heads and aspects.
5. [Control points](05-control-points.md): interlockings, routes, intermediates and crossovers.
6. [The CTC panel](06-ctc-panel.md): drawing track, lights and levers.
7. [Patching](07-patching.md): changing what's already there, and the patch instructions.
8. [RailForge](08-railforge.md): its extra checks and how to pass them.
9. [Testing and debugging](09-testing-and-debugging.md): tools, files and a symptom checklist.
10. [Worked examples](10-examples.md): a signal-only control point, a passing siding, and a junction added to an existing control point.

[Quick reference](reference.md): every property and patch instruction on one page.

## Versions

Written against SignalsEverywhere 1.4 (October 2026). Some behavior described here is a bug with a fix waiting to be merged; those spots are marked, with a workaround. [Tetz's SignalsEverywhere Fixes](https://www.nexusmods.com/railroader/mods/1807) applies most of those fixes at run time, but write your mod so it works without it.

## Credits

This guide is by Tetz95, from building the signal mods on Nexus (Branch Junctions, TRD Entry Signal, Alarka Branch Signals) and reading SignalsEverywhere's and the game's code along the way. It is also kept, with PDF and single-file builds, at [Tetz95/SignalsEverywhere-Modding-Guide](https://github.com/Tetz95/SignalsEverywhere-Modding-Guide). Corrections are welcome as issues or pull requests.
