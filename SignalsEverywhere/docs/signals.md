# Signals (`autoSignals` & `predicateSignals`)

Signals are used to control the movement of trains. There are two types of signals: `autoSignals` and `predicateSignals`.

### Common Properties
*   **`location`**: Where the physical signal stands (uses `segmentId`, `distance`, and `end`).
*   **`direction`**: `Left` or `Right` relative to the track direction.
*   **`headConfiguration`**: `Single`, `Double`, or `Triple`.
*   **`leftSide`**: (Optional) Boolean. Set to `true` if the signal is on the left side of the track.
*   **`offset`**: (Optional) Horizontal offset from track center (default is 3).

### Auto Signals (`autoSignals`)
Automatic signals usually protect a series of blocks or follow interlocking routes.
*   **`blocks`**: List of Block IDs this signal monitors.
*   **`interlockingRouteMapping`**: Indices of routes in the local interlocking that this signal should react to.

### Predicate Signals (`predicateSignals`)

Predicate signals are advanced signals where the displayed aspect is determined by a list of conditional logic rules ("Predicates"). This is essential for signals at interlockings where the aspect depends on switch positions or the state of a specific interlocking.

#### Structure
A `predicateSignal` contains a `heads` list, where each entry represents one signal head (usually ordered from top to bottom).

*   **`heads`**: A list of `HeadPredicates` objects.
    *   **`nextCtcSignal`**: The ID of the signal following this one in the sequence.
    *   **`predicates`**: A list of conditions that must be met for this head to display a proceeding aspect.

#### Predicate Properties
Each predicate in the list defines a specific check:

*   **`type`**: The type of logic to check. Available types:
    *   `Switch`: Checks the position of a specific track switch.
    *   `Block`: Checks if one or more blocks are occupied.
    *   `Interlocking`: Checks the state of an interlocking.
    *   `Direction`: Checks if a specific signal direction is established.
*   **`switchNode`**: (Required for `Switch`) The ID of the track node representing the switch.
*   **`switchSetting`**: (Required for `Switch`) Set to `Normal` or `Reversed`.
*   **`blocks`**: (Required for `Block`) A list of Block IDs that must be clear.
*   **`interlocking`**: (Required for `Interlocking`) The ID of the interlocking to monitor.
*   **`direction`**: (Required for `Direction`) The `SignalDirection` to check.

**Example Configuration:**
```json
"my-predicate-signal": {
  "location": { "segmentId": "MAIN_01", "distance": 10, "end": "Start" },
  "direction": "Right",
  "headConfiguration": "Double",
  "heads": [
    {
      "nextCtcSignal": "next-signal-id",
      "predicates": [
        {
          "type": "Switch",
          "switchNode": "SWITCH_NODE_01",
          "switchSetting": "Normal"
        },
        {
          "type": "Block",
          "blocks": ["block-ahead-01"]
        }
      ]
    },
    {
      "nextCtcSignal": "diverging-signal-id",
      "predicates": [
        {
          "type": "Switch",
          "switchNode": "SWITCH_NODE_01",
          "switchSetting": "Reversed"
        }
      ]
    }
  ]
}
```
