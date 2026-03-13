# Custom Spawn Points

CustomSpawnPoints is a mod for Railroader that allows you to define custom starting locations and initial equipment for
your game.

## Configuration via Mixins

Declare the mixin in your mod's `Definition.json` file like this:
```json
{
  "id": "ExampleMod",
  ...
  "mixintos": {
    "game-graphs": [ "file(my-game-graph.json)"],
    "spawnPoints": [ "file(my-start.json)" ]
  }
}
```

## JSON Configuration

Each custom start point is defined in a JSON file. Below is the structure and an explanation of the available fields.

### Root Object

| Field             | Type             | Description                                                     |
|:------------------|:-----------------|:----------------------------------------------------------------|
| `identifier`      | string           | Unique identifier for this setup.                               |
| `name`            | string           | The display name shown in the game's start menu.                |
| `progressionId`   | string           | The ID used for game progression tracking.                      |
| `showTutorial`    | boolean          | Whether to show the tutorial when starting with this point.     |
| `initialMoney`    | integer          | The amount of money the player starts with.                     |
| `spawnPoint`      | object           | Definition of the player's initial character spawn location.    |
| `enabledFeatures` | array of strings | A list of feature IDs to be enabled at the start.               |
| `carPlacements`   | array of objects | A list of cars/locomotives to be spawned at specific locations. |

### Spawn Point Object (`spawnPoint`)

| Field      | Type              | Description                                                                            |
|:-----------|:------------------|:---------------------------------------------------------------------------------------|
| `location` | array of 3 floats | The `[x, y, z]` coordinates for the spawn point.                                       |
| `rotation` | array of 3 floats | The `[x, y, z]` Euler angles for the spawn rotation.                                   |
| `range`    | float             | (Optional) The radius around the location where the player can spawn. Defaults to `3`. |
| `priority` | integer           | (Optional) Spawn priority. Defaults to `1`.                                            |

### Car Placement Object (`carPlacements`)

| Field           | Type             | Description                                                                                 |
|:----------------|:-----------------|:--------------------------------------------------------------------------------------------|
| `carIdentifier` | array of strings | A list of car identifiers to spawn. If multiple are provided, they are spawned in sequence. |
| `location`      | object           | The track location where the car should be placed.                                          |
| `wreck`         | boolean          | Whether the car should spawn as a wreck.                                                    |
| `oiled`         | float            | The oil level of the car (0.0 to 1.0). Defaults to `1.0`.                                   |
| `loadPercent`   | float            | The percentage of load in the car (0.0 to 1.0). Defaults to `0.0`.                          |
| `loadId`        | string           | (Optional) The identifier for the type of load (e.g., `"coal"`, `"logs"`).                  |

#### Track Location Object (`location`)

| Field       | Type   | Description                                                     |
|:------------|:-------|:----------------------------------------------------------------|
| `segmentId` | string | The identifier of the track segment.                            |
| `distance`  | number | The distance along the segment.                                 |
| `end`       | string | Which end of the segment to measure from: `"Start"` or `"End"`. |

## Example Configuration

```json
{
  "identifier": "oc",
  "name": "Old Cowee Start",
  "progressionId": "oc",
  "showTutorial": false,
  "initialMoney": 5000,
  "spawnPoint": {
    "location": [
      6210,
      594,
      -13015
    ],
    "rotation": [
      0,
      180,
      0
    ],
    "range": 3
  },
  "enabledFeatures": [
    "oc-nf",
    "ij-oh",
    "ogr-oc"
  ],
  "carPlacements": [
    {
      "carIdentifier": [
        "ls-460-t17"
      ],
      "location": {
        "segmentId": "SOGR-OC10_e6xf",
        "distance": 30,
        "end": "Start"
      },
      "wreck": false,
      "oiled": 0.8,
      "loadPercent": 0.42,
      "loadId": "coal"
    },
    {
      "carIdentifier": [
        "ls-460-t22"
      ],
      "location": {
        "segmentId": "SOC-NF10_h5xp",
        "distance": 5,
        "end": "Start"
      },
      "wreck": false,
      "oiled": 0.8,
      "loadPercent": 0.84,
      "loadId": "coal"
    },
    {
      "carIdentifier": [
        "fl-skeleton01",
        "fl-skeleton01",
        "fl-skeleton01",
        "fl-skeleton01",
        "fl-skeleton01",
        "fl-skeleton01",
        "fl-skeleton01",
        "fl-skeleton01"
      ],
      "location": {
        "segmentId": "SOGR-OC10_j2w7",
        "distance": 190,
        "end": "Start"
      },
      "wreck": false,
      "oiled": 1.0,
      "loadPercent": 1.0,
      "loadId": "logs"
    }
  ]
}
```

