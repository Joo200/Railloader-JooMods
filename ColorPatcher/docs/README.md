# ColorPatcher Mod

This mod adds bicolor callouts to the game, allowing players to customize the colors of their all-out attacks.

## Usage

To use bicolored callouts, instead of specifying 3 color values (RGB) apply 6 to a region.

The values are interpreted as RGBRGB, where the left/major color is the first value.

```json
{
  "areas": {
    "my_area": {
      "name": "Area Name",
      "tagColor": [ 0.16, 0.63, 0.55, 0.63, 0.14, 0.14]
    }
  }
}
```

