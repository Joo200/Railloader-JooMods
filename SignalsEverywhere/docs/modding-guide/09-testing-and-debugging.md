# 9. Testing and debugging

## Tools

**SignalsEverywhere's mod tab** (in the game's mod settings):

| Button | Does |
|---|---|
| Reload Signal Definitions | Re-reads every `signals` file and rebuilds. |
| Dump Signals | Writes `signal-old.json` and `signal-patched.json` to SignalsEverywhere's folder. |
| Rebuild CTCPanel | Re-reads every `ctcPanel` file and redraws the panel. |
| Dump CTCPanel | Writes `panel-dump.json`. |
| Blocks (dropdown) | Highlights the chosen block's track on the map. |

**Console commands** (host only):

- `/signaldebug dump signals`: same as Dump Signals.
- `/signaldebug dump panel`: same as Dump CTCPanel.

**Files** in SignalsEverywhere's mod folder:

| File | Holds |
|---|---|
| `signal-old.json` | The game's signals before any mod. Your source of real ids and locations. |
| `signal-patched.json` | After every mod's patches. Shows what SignalsEverywhere **meant** to build. |
| `panel-dump.json` | The merged panel layout. |
| `ctcpanel-failed.json` | Written when the panel layout can't be read at all. |

**Logs**: SignalsEverywhere logs patch errors ("Error loading mixinto …"), missing ids ("Block with ID 'x' not found in patching context") and what it created. Under RailForge, also read the `## Signal authoring` section of `Mods/RTM.RailForge/RailForge-support-report.txt` (see [RailForge](08-railforge.md)).

## Patched data is not proof

`signal-patched.json` shows the result of the merge. Whether that reached the game depends on what counted as touched (see [What gets rebuilt](07-patching.md#what-gets-rebuilt)) and on RailForge's checks. A change can be in the patched data and still not be in the game. Always confirm on the railroad: the panel, the signals, and a train.

## A test run

For every control point you add or change:

1. **Blocks**: highlight each new block in the mod tab. Then run a train through: each block light on the panel should come on as the train enters and go off when the last car leaves.
2. **Each route, each direction (CTC)**: code the route, check the right signal clears and the others stay at Stop. Check the aspect: Clear or Approach on the normal route, Diverging on the diverging one.
3. **Next signal**: with the next signal at Stop, a signal should show Approach. With it clear, Clear. A signal that never shows Clear has a missing or stale next signal.
4. **Opposing moves**: set a route one way, then try to code the opposite direction at the next control point. It should refuse.
5. **Occupancy**: put a car on the OS block. Every signal at that control point should go to Stop, and the switches shouldn't move.
6. **Approach**: code a route, then put a car in the block a train approaches on (an outlet block). The signal should stay clear, and the dispatcher should still be able to cancel and recode the route. If the route drops, that block is in the control point's module.
7. **Hand-thrown switch**: unlock a switch in CTC or throw one in ABS. Its block should show occupied.
8. **ABS**: switch the railroad to ABS and repeat the basic moves. Predicate signals are where ABS surprises happen.
9. **Auto engineer**: send an AE train through on Road mode both ways. It should obey every aspect without stopping where it shouldn't.
10. **Reload the save** and check the routes and directions came back.

## Symptoms and causes

| Symptom | Likely cause |
|---|---|
| None of the mod's changes appear. | One broken instruction stopped the whole file. Check the log for "Error loading mixinto". |
| One module's changes are missing under RailForge. | RailForge deferred it. Read `## Signal authoring` in the support report. |
| The change is in `signal-patched.json` but not in the game. | An array item edit (`$find`/`$index`/`$add`) that didn't count as a touch. Add a harmless `$replace` to the same component. |
| A signal is stuck at Approach and never shows Clear. | Its next signal is `null`, or points at a signal that was rebuilt (a stale reference). Touch the control point or predicate signal that names it. |
| A route drops as soon as a train enters the block approaching the control point, and the lever won't code or cancel while it's there. | That outlet block is defined in the control point's module, which makes it an OS block. Move it to a module without an interlocking. |
| A control point's lever does nothing ("no route"). | No route matches the current switch positions, or `switchFilters` don't line up with `switchSets`. |
| A control point vanished after a patch (plain Railloader). | You patched its module without touching its interlocking/intermediate. |
| A block never shows occupied. | A span's ends don't connect along the track. Try measuring one end from the other end of its segment. |
| A block shows occupied with no train. | It overlaps a hand-thrown switch, or another train's block. Check with the Blocks highlight. |
| A signal faces the wrong way. | Its location is measured from the wrong end. |
| A signal stands on the wrong side or in the track. | `leftSide` or `offset`. |
| A signal clears for the wrong direction. | Its `direction`, or the outlet direction in the interlocking. |
| A control point signal never clears, with an error about an empty `interlockingRouteMapping`. | The auto signal is missing its route mapping. |
| Intermediate signals stay red after the train has passed, or clear against a route. | The intermediate's block or signal order isn't left to right, or its `signalLeft`/`signalRight` don't match the control points. |
| The panel won't open. | The panel layout couldn't be read; see `ctcpanel-failed.json` and the log. |
| Levers overlap on the panel. | Columns too close together. See [How X works](06-ctc-panel.md#how-x-works). |
