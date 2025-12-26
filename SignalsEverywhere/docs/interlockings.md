# Interlocking (`interlocking`)

Interlockings manage complex junctions where multiple routes are possible.

### Properties
*   **`id` / `displayName`**: Identification for the CTC panel.
*   **`switchSets`**: A list of Switch Node IDs included in this junction.
*   **`outlets`**: Possible exit points from the interlocking.
*   **`direction`**: `Left` or `Right`.
*   **`blocks`**: The physical blocks inside this outlet's path.
*   **`nextSignal`**: The signal guarding the next section.
*   **`routes`**: Defines which switch positions lead to which outlets.
*   **`switchFilters`**: A list matching the `switchSets` order (e.g., `["Normal"]` or `["Reversed"]`).
*   **`outletLeft`**: Index of the outlet used when traveling Left.
*   **`outletRight`**: Index of the outlet used when traveling Right.
