# ColorPatcher Mod

This mod adds bicolor callouts to the game, allowing players to customize the colors of their all-out attacks.

## Usage

To use bicolored callouts, instead of specifying 3 color values (RGB) apply 6 or 9 to a region.

The values are interpreted as RGBRGB, where the major color is the first interpreted value.

If you provide 9 float values (as 3 colors) (e.g. `[0.15, 0.23, 0.41, 0.15, 0.23, 0.41, 0.15, 0.23, 0.41 ]`) the first color is used when the mod is not installed.
ColorPatcher will then use the second and third color to color the callout. This allows you to use a different base color for the callout when the mod is not installed.

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

