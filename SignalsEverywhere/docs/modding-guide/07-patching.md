# 7. Patching

Your `signals` and `ctcPanel` files are patches. SignalsEverywhere merges them into the data it starts from (the game's signals, or the main line panel), one file at a time in load order. This page covers how the merge works, the instructions you can use, what each change rebuilds in the game, and the known problems.

## The plain merge

With no instructions, a patch merges by key:

- **Objects** merge property by property. Properties you don't mention are kept. Keys are matched without regard to case.
- **Plain values** (strings, numbers, `true`/`false`) replace the old value.
- **`null`** as the value of a block, signal or module removes it (see the notes on [removing](#removing-things)).
- **Arrays can't be plain-merged.** To change an existing array you must say what to do: replace it, or edit its items with `$find`, `$index`, `$add` or `$append`. A plain array where one already exists is an error.

So this patch moves one existing signal and changes nothing else about it:

```json
{
  "BR-W": {
    "BR-W": {
      "predicateSignals": {
        "br-we": {
          "location": { "segmentId": "Sabc", "distance": 12.5, "end": "Start" }
        }
      }
    }
  }
}
```

## Instructions

Instructions are properties that start with `$`.

### On an object

| Instruction | Does |
|---|---|
| `"$replace": { ... }` | Replaces the whole object with the given value. |
| `"$moveTo": "<path>"` | Moves this object into another object, then merges the rest of the patch into it there. |
| `"$remove": true` | Removes the property. Works when the property holds an array or a plain value. On a property that holds an object it fails in 1.4 (see [known problems](#known-problems)). |

`$replace` is the safe tool for anything structural, like rewriting an interlocking:

```json
"interlocking": {
  "$replace": {
    "id": "aj-w",
    "displayName": "Alarka Jct West",
    "switchSets": [ [ "Nowf" ] ],
    "outlets": [ ... ],
    "routes": [ ... ]
  }
}
```

`$moveTo` takes a JSON path from the root of the document, quoted in brackets because ids contain dashes and spaces:

```json
{ "MY-FEATURE": { "OLD-MODULE": { "autoSignals": {
  "my-signal": { "$moveTo": "['MY-FEATURE']['NEW-MODULE']['autoSignals']" }
} } } }
```

The destination must already exist when this patch is applied. If your own mod creates it, create it in an earlier file and move in a later one. Moving a signal deletes the old game object and builds it again in the new module.

**Never move a control point's signal out of that control point's module.** A signal belongs to the interlocking of the module it's in. Moved anywhere else, the game treats it as an intermediate signal, and the auto engineer will pass it at Stop.

### On an array

Each item in the patch array is an object of instructions.

| Item | Does |
|---|---|
| `{ "$add": <value> }` | Appends one item. |
| `{ "$append": [ <values> ] }` | Appends several. |
| `{ "$find": [ <conditions> ], ...fields }` | Finds the first matching item and merges the fields into it. |
| `{ "$index": 2, ...fields }` | The same, for the item at that position (from 0). |
| `{ "$find": [...], "$selectAll": true, ...fields }` | Every matching item, not just the first. |
| `{ "$find": [...], "$replace": <value> }` | Replaces the matching item. |
| `{ "$find": [...], "$remove": true }` | Removes the matching item. |
| `{ "$find": [...], "$clone": true, ...fields }` | Copies the matching item, merges the fields into the copy, and appends it. |
| `"$optional": true` | With `$find` or `$index`: if nothing matches, skip quietly instead of failing. |

To replace an existing array outright, use `$replace` on the property, not an array: `"blocks": { "$replace": [ "a", "b" ] }`.

### `$find` conditions

```json
"$find": [
  { "path": "direction", "value": "Left" },
  { "path": "nextSignal", "comparison": "StartsWith", "value": "aj-" }
]
```

- **`path`**: a JSON path inside the item (`"direction"`, `"blocks[0]"`).
- **`comparison`**: `Equals` (default), `NotEquals`, `StartsWith`, `EndsWith` or `Contains`. Text comparisons ignore case.
- **`value`**: what to compare against.

All conditions must match. **Every item in the array must have every path you test**: an item without it throws an error instead of simply not matching, and the error drops your whole file.

## Errors drop the whole file

If any instruction in a file fails (a `$find` with no match, an unsupported instruction, a bad path), SignalsEverywhere logs the error and stops applying **that file**. Everything after the error is lost, and you can't count on what came before it either. A mod whose changes all silently vanish usually has one broken patch. Keep risky or optional patches in a separate file, and use `$optional` where something may legitimately be missing.

## What gets rebuilt

SignalsEverywhere records every path a patch changes, then rebuilds only what was touched:

| What you touch | What happens in the game |
|---|---|
| Nothing in a module | The module is left exactly as it was. |
| A block | Its spans are replaced in place. Things that point at the block keep working. |
| A signal | The signal is **deleted and built again**. |
| A module's `interlocking` / `crossover` | The same object is kept and its switch sets, outlets and routes are rewritten from the patched data. |
| A module's `intermediate` | Rebuilt the same way. |
| Anything else in a module, but not its `interlocking`, `intermediate` or `crossover` | See the next rule. |

Two rules follow from this.

**Touch the control point when you patch its module.** In SignalsEverywhere 1.4, when a module is rebuilt and its interlocking, intermediate or crossover was not touched, that component is **deleted**. RailForge prevents this, but under plain Railloader it happens. So whenever you patch anything in a module that has one, also touch it. The easiest harmless touch is replacing an array with its current value:

```json
"interlocking": { "switchSets": { "$replace": [ [ "Nuam" ] ] } }
```

**Touch everything that names a signal you changed.** A rebuilt signal is a new object. Interlocking outlets (`nextSignal`) and predicate heads (`nextCtcSignal`) that aren't rebuilt still point at the deleted one, which counts as "no next signal", so the signal behind it is stuck at Approach. Intermediates are relinked by id on every build, so they're fine. So when you change a signal, find every outlet and predicate head that names it (search the dump for its id) and touch those control points or signals too, even if the value is the same.

## Removing things

| To remove | Write | Notes |
|---|---|---|
| A block | `"block-id": null` | Update everything that names it. |
| An auto signal | `"signal-id": null` | Safe. |
| A predicate signal | `"signal-id": null` | Crashes SignalsEverywhere 1.4 (see below). Rewrite it instead, or `$moveTo` it if it isn't a control point signal. |
| A whole module | `"MODULE": null` | Deletes the module's game object and everything in it. |
| An array item | `{ "$find": [...], "$remove": true }` | Works. |
| A property | `"prop": { "$remove": true }` | Fine for an array or plain value. For a property holding an object it fails in 1.4 (see below); `$replace` the parent instead. |

## Known problems

These are bugs in SignalsEverywhere 1.4 with fixes sent upstream. Tetz's SignalsEverywhere Fixes applies the first four at run time. Write your mod so it works without it.

| Problem | Symptom | Workaround | Upstream |
|---|---|---|---|
| **Array item edits don't count as a touch.** `$find`, `$index`, `$add` and `$append` inside an existing array change the patched data but are recorded under the wrong path, so the component isn't rebuilt. | Your edit shows in `signal-patched.json`, but the game behaves as before. | Also put a harmless `$replace` on the same component, such as `"switchSets": { "$replace": [ ...same value... ] }`. | PR #7 |
| **Only one interlocking per module is seen.** A stock module with two (Alarka Jct: `aj-e` and `aj-w`) only exposes the first. | You can't patch `aj-w`; patching `aj-e` adds a second interlocking. | Needs Tetz's SignalsEverywhere Fixes (moves the second one into its own module, `AJ-W`). | PR #4 |
| **`null` predicate signal crashes.** | The predicate signal is deleted, then the rest of the module is skipped. | Rewrite it instead of removing it. | PR #5 |
| **Stock intermediates on a feature object** keep pointing at old signals next to a new control point. | Errors about aspects; the new control point's signals clear without a route. | See [Intermediates](05-control-points.md#intermediates). | PR #8 |
| **`$remove` on a property that holds an object** throws "Unsupported patch instructions". | The file stops applying at that point. | `$replace` the parent object, or `$remove` the whole array item. | PR #9 |
