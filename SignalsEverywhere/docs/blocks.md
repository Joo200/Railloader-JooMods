# Blocks (`blocks`)

Blocks define the track segments that detect train presence. They are composed of one or more "Spans".

### Property Definition
*   **`spans`**: A list of track segments defining the block's physical footprint.
    *   **`lower` / `upper`**: The start and end points of the span.
        *   **`segmentId`**: The internal ID of the track segment.
        *   **`distance`**: Distance from the node (in meters).
        *   **`end`**: `Start` or `End` of the segment.

**Example:**
```json
"my-block-id": {
  "spans": [
    {
      "lower": { "segmentId": "TRACK_A", "distance": 0, "end": "Start" },
      "upper": { "segmentId": "TRACK_A", "distance": 50, "end": "Start" }
    }
  ]
}
```
