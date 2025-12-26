# Interchange Reloader

The **Interchange Reloader** mod adds a new industry component to Railroader that acts as an automated interchange
point. It manages the arrival, processing, and redirection of freight cars, facilitating a dynamic flow of traffic
between your railroad and the "outside world" or other local industries.

## Behavior

The Interchange Reloader performs several key functions:

1. **Car Ordering**: The component automatically orders cars to be delivered to the itself. It can order both empty cars
   and cars carrying specific loads defined in the configuration.
2. **Processing (Unloading/Reloading)**: When a car arrives at the interchange reloader (specifically on the defined
   `trackSpans`), the mod waits for a randomized duration (between `minCarTime` and `maxCarTime`).
3. **Redirection (Waybilling)**:
    * **Local Delivery**: The mod searches for other industries on your railroad that have active contracts and need the
      car's type/load. If a match is found, it generates a new waybill for that local destination.
    * **External Routing**: If no local demand is found, the mod will either restock the car with one of the configured
      `loads` and send it away loaded, or send it away empty.
4. **SKOM Integration**: If the Some Kind of Madness (SKOM) mod is detected, the Interchange Reloader automatically
   adjusts its ordering logic (reducing `maxCars` by half) to better balance with SKOM's mechanics.

## Configuration

The Interchange Reloader is configured as a component within an industry in your mod track file.

### Component Properties

| Property        | Type     | Description                                                                               | Default |
|:----------------|:---------|:------------------------------------------------------------------------------------------|:--------|
| `type`          | `string` | Must be `InterchangeReloader.Ops.InterchangeReloader`.                                    | -       |
| `carTypeFilter` | `string` | Comma-separated list of car types this interchange handles (e.g., `RS,XM,FM`).            | -       |
| `loads`         | `array`  | A list of load IDs that the interchange can order or use to restock cars.                 | `[]`    |
| `maxCars`       | `int`    | The maximum number of cars the interchange will attempt to keep on order/at the facility. | `12`    |
| `minCarTime`    | `float`  | Minimum hours a car must stay at the interchange before being reassigned.                 | `2.0`   |
| `maxCarTime`    | `float`  | Maximum hours a car must stay at the interchange before being reassigned.                 | `6.0`   |
| `trackSpans`    | `array`  | List of track span names where cars are considered "at the interchange".                  | -       |
| `sharedStorage` | `bool`   | Whether the industry uses shared storage mechanics.                                       | `false` |

## Example Configuration

Below is an example of how to define an Interchange Reloader in your mod track file:

```json
{
  "industries": {
    "mca-interchange": {
      "name": "Interchange",
      "usesContract": true,
      "localPosition": {
        "x": 6758,
        "y": 613,
        "z": -17935
      },
      "components": {
        "mca-interchange": {
          "name": "Macon County Airport Interchange",
          "type": "InterchangeReloader.Ops.InterchangeReloader",
          "carTypeFilter": "RS,XM,FM",
          "loads": [
            "lumber-dimensional",
            "boxcar-generic",
            "whiskybarrels",
            "mining-supplies",
            "wine-bottles"
          ],
          "maxCars": 16,
          "trackSpans": [
            "MCA"
          ],
          "sharedStorage": true
        }
      }
    }
  }
}
```