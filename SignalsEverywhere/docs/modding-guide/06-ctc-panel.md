# 6. The CTC panel

The panel is drawn from a layout: a grid of track pieces, block lights and labels, with a lever (switch knobs, a direction knob and a code button) under each control point. SignalsEverywhere's own `CTCPanel-MainLine.json` draws the main line; mods patch it with a `ctcPanel` mixinto.

## The layout

```json
{
  "panel": {
    "Mainline": [ ... elements ... ],
    "Alarka Branch": [ ... elements ... ]
  }
}
```

Each key under `panel` is a **section**. `Mainline` is the game's main line. Any other key adds its own section, drawn below with a collapsible header. A branch with its own control points reads best as its own section.

## Elements

```json
{ "Type": "Light", "Block": "cn-w", "X": 4, "Y": 2, "Color": "red", "ShowTrack": false,
  "Interlock": { "Interlock": "cn-w", "SwitchLabels": [ "CW" ], "SignalLabel": "CW" } }
```

| Field | Meaning |
|---|---|
| `Type` | What to draw (see below). |
| `X`, `Y` | Grid position. `Y` is the row; `X` the column (see [How X works](#how-x-works)). |
| `Block` | For `Light`: the block whose occupancy it shows. For `Label`: text to show instead of `Id`. |
| `Id` | For `Label`: the text. For switch pieces: an id for highlighting. |
| `Color` | `white` (default) or `red`. Red lights mark OS blocks; red labels stand out. |
| `ShowTrack` | For `Light`: draw the track line behind the light. Default `true`. |
| `SwitchLabel`, `LabelOffsetX`, `LabelOffsetY` | A small blue label on a switch piece, nudged by the offsets. |
| `Interlock` | Puts a control point lever under this column (see below). |
| `Crossover` | Puts a crossover lever under this column. |

### Types

| `Type` | Draws |
|---|---|
| `Label` | Text, usually a station name on row 0. |
| `Light` | A block light, with track behind it unless `ShowTrack` is `false`. |
| `Track` | A plain horizontal track piece. |
| `TrackRise`, `TrackFall` | A diagonal piece going up or down to the next row. |
| `SwitchLeftTop` | Main track with a branch leaving to the upper right. |
| `SwitchRightTop` | Main track with a branch leaving to the upper left. |
| `SwitchLeftBottom` | Main track with a branch leaving to the lower left. |
| `SwitchRightBottom` | Main track with a branch leaving to the lower right. |

Rows: the main line on the Mainline section sits on row 2, labels on row 0, sidings on rows 1 and 3. Follow the rows the surrounding panel uses.

## Levers

An element with `Interlock` gets a lever in the control row under its column. Put it on the control point's OS block light.

```json
"Interlock": {
  "Interlock": "cn-w",
  "SwitchLabels": [ "CW" ],
  "SignalLabel": "CW",
  "KnobOrder": [ 0 ]
}
```

- **`Interlock`**: the interlocking id.
- **`SwitchLabels`**: one label per switch knob, in `switchSets` order. One knob per switch set; a signal-only control point has none.
- **`SignalLabel`**: the label on the direction knob.
- **`KnobOrder`**: optional. Draws the switch knobs in a different order; must list every switch set.

Knob state is stored by id: `<interlocking>-0`, `<interlocking>-1`, … for switch knobs, `<interlocking>-D` for the direction knob. The game's own levers keep their old ids through `VanillaSwitchKnobIds` and `VanillaDirKnobId`; leave those as they are when you patch a stock lever.

For a crossover, `Crossover` takes `Crossover` (the id), `SwitchKnobOrder`, `SignalKnobOrder`, `SwitchLabels` and `SignalLabels` (one per signal group).

Levers only appear in CTC mode.

## How X works

SignalsEverywhere doesn't use `X` as a position. It sorts every distinct `X` in a section and numbers them 1, 2, 3, …. So `X` only sets the **order** of columns, and you can slot a new column between 37 and 38 with `37.5` without moving anything.

That squashing has two consequences:

- Each distinct `X` becomes one grid cell, 36 px wide. Every element sharing an `X` lands in the same column.
- A lever is wider than a cell: about 70 px for one knob and 135 px for two. Levers in neighboring columns overlap unless there are enough columns between them. As a rule of thumb, two one-knob levers need 2 columns between their centers, a one-knob and a two-knob lever need 3, and two two-knob levers need 4. Add `Track` spacer pieces at new `X` values to make room.

All levers share one control row, whatever their `Y`, so two levers at the same `X` always collide.

## Patching the panel

Sections are arrays, so patching uses the array instructions from [Patching](07-patching.md):

```json
{
  "panel": {
    "Mainline": [
      { "$add": { "Type": "Light", "Block": "rg-mine", "X": 37.3, "Y": 3 } },
      { "$find": [ { "path": "Type", "value": "Light" }, { "path": "Block", "value": "aj-bk-1" } ],
        "X": 38 },
      { "$find": [ { "path": "Type", "value": "SwitchLeftTop" }, { "path": "X", "value": 49 } ],
        "$remove": true }
    ]
  }
}
```

- `$add` adds an element.
- `$find` matches existing elements by field values; the other fields in the object are merged into the match.
- `$remove: true` with `$find` deletes the match.

Match on fields every element in the section has, like `Type`, `X` and `Y`. `$find` throws an error on any element that lacks the field you test, and one error stops your panel file from applying (see [Errors drop the whole file](07-patching.md#errors-drop-the-whole-file)). Most elements have no `Id`, so `$find` on `Id` alone is risky.

For a new section, you can write plain elements. But if two mods add to the same new section, the second one must use `$add`, so use `$add` everywhere and it works whichever mod loads first.

## Checking the panel

Use **Dump CTCPanel** in SignalsEverywhere's mod tab (or `/signaldebug dump panel`) to write the merged layout to `panel-dump.json` in SignalsEverywhere's folder. If the layout can't be read at all, SignalsEverywhere writes what it had to `ctcpanel-failed.json` and the panel won't open. **Rebuild CTCPanel** reloads the layout without restarting.
