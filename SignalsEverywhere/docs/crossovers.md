# Crossover (`crossover`)

Crossovers are used to manage connections between parallel tracks, typically involving multiple switches and signal groups.

### Properties
*   **`id` / `displayName`**: Identification for the CTC panel.
*   **`switchSets`**: A list of Switch Node IDs included in this crossover.
*   **`outlets`**: Possible exit points from the crossover.
    *   **`direction`**: `Left` or `Right`.
    *   **`blocks`**: The physical blocks inside this outlet's path.
    *   **`nextSignal`**: The signal guarding the next section.
*   **`routes`**: Defines which switch positions lead to which outlets and which blocks are used.
    *   **`switchFilters`**: A list matching the `switchSets` order.
    *   **`outletLeft`**: Index of the outlet used when traveling Left.
    *   **`outletRight`**: Index of the outlet used when traveling Right.
    *   **`usedBlocks`**: List of blocks used by this route.
*   **`signalGroups`**: Groups of signals that are governed by the crossover's state and direction.
    *   **`groupId`**: Unique identifier for the group.
    *   **`signals`**: List of signal IDs in this group.
    *   **`allowedDirection`**: The direction allowed for this group.
    *   **`allowedRoutes`**: List of route indices that are allowed when this group is active.
