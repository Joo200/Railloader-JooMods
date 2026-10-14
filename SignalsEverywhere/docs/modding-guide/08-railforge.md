# 8. RailForge

RailForge replaces Railloader and StrangeCustoms: it loads Railloader-style mods, including SignalsEverywhere, and adds its own handling for signal mods. Most players use it, so test under it. It changes three things.

## It keeps untouched control points

Under RailForge, patching part of a module no longer deletes an interlocking, intermediate or crossover you didn't touch (see [What gets rebuilt](07-patching.md#what-gets-rebuilt)). Keep touching them anyway, so your mod works under plain Railloader too.

## It checks every patched module first

Before SignalsEverywhere builds anything, RailForge checks each module a mod patches. If a module fails a check, RailForge **defers** it: it removes your changes to that module, leaves the game's version running, and builds nothing new there. Other modules still apply. Nothing about this shows on screen, so a deferred module looks exactly like a patch that didn't work.

RailForge writes the reason to its support report, `Mods/RTM.RailForge/RailForge-support-report.txt`, under **`## Signal authoring`**. The section is rewritten every time a map loads and lists each deferred module with the path and the message. **When a module's changes are missing under RailForge, read that section first.**

The checks we've run into:

### Forward references

Everything a signal or predicate names must already exist **when that signal is created**: in the same module, in an earlier module, or in the game's own data. Modules are created in order, feature by feature, with the game's modules first and new modules after them in the order they appear in the patched data.

This bites predicate signals that name another control point. If module `AB-S` has a predicate signal naming interlocking `ab-w`, and `ab-w` is in module `AB-W` which comes later, RailForge defers `AB-S`. Some RailForge versions also apply it to the game's own control points: a stock predicate signal in `BR-E` that names `br-w` in `BR-W` (which comes after it) gets deferred as soon as your mod patches it. SignalsEverywhere itself has no trouble with a control point that's already in the game, because it indexes every existing interlocking before building anything; the check is stricter than it needs to be. RailForge 0.14.71 no longer flags this case.

Fixes:

- **For a new control point:** define it in a module that comes before every signal that names it. A new module is added after the existing ones, in the order they appear.
- **For a signal at a control point:** keep it in its control point's module. **Don't move it to another module to get past this check.** A signal only belongs to a control point when it sits in that control point's module; anywhere else, the game treats it as an intermediate signal. The auto engineer then stops at it and passes it at Stop, as it would at any intermediate signal. Tetz's SignalsEverywhere Fixes 1.4.0 lets RailForge accept a reference to a control point that's already in the game, which covers the stock case above.
- **For a signal that isn't part of a control point** (an intermediate, or a stand-alone predicate signal), moving it to a later module with `$moveTo` is fine. The destination must already exist, so create it in one `signals` file and move into it from a second file.

Forward references to **blocks** are fine.

### Locations outside their segment

Every location you patch must lie within its segment as the track exists in the game right now. Other mods can change track: a mod that shortens a segment can make a stock signal's location invalid the moment you patch that signal, even if you didn't move it. If a module is deferred for a location you never changed, check whether another track mod moved or shortened that segment, and write the location from the other end or on the new segment.

### Removing predicate signals

RailForge refuses any patch that sets a predicate signal to `null`, because of the SignalsEverywhere crash (see [Known problems](07-patching.md#known-problems)), and defers the module. Tetz's SignalsEverywhere Fixes lets that one check through once the crash is fixed. Without it, don't remove predicate signals: move or rewrite them.

## Load order

RailForge orders mods by `loadAfter`. List every mod whose signals or panel you patch, and every mod that changes track your signals stand on, so your patches see their changes.
