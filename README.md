# Rain World Wallpaper Mode

A Rain World mod that transforms the game into a dynamic wallpaper with smooth transitions between random locations.

## Features

- **Automatic Room Transitions**: Smoothly moves through different rooms and regions
- **Smooth Easing**: Uses cubic easing for pleasant camera transitions
- **Random Exploration**: Randomly selects locations to create an ever-changing view
- **Safari Mode Inspired**: Based on Rain World's Safari mode but automated for wallpaper use

## Installation

1. Ensure you have Rain World v1.9+ (Downpour) installed
2. Install BepInEx if not already installed
3. Copy the mod folder to `RainWorld_Data/StreamingAssets/mods/`
4. Enable the mod in the Remix menu

## Development

### Prerequisites

- .NET SDK
- Rain World game files
- BepInEx

### Building

```bash
dotnet build
```

The compiled mod will be in `artifacts/bin/RainWorldWallpaperMod/debug_win-x86/mod/`

### Setup

Before building, you need to copy the following DLLs from your Rain World installation to the `lib/` folder:

- `UnityEngine.dll` (from `RainWorld_Data/Managed/`)
- `Assembly-CSharp.dll` (from `RainWorld_Data/Managed/`)
- `HOOKS-Assembly-CSharp.dll` (from `BepInEx/plugins/`)

## Configuration

Currently configuration is done through code. Future versions will include:

- Transition duration adjustment
- Stay duration per location
- Region selection
- Camera behavior options

## TODO

- [ ] Implement room selection logic
- [ ] Add camera positioning system
- [ ] Implement region traversal
- [ ] Add configuration options
- [ ] Test with different regions
- [ ] Add support for custom region packs

## License

Apache-2.0

## Credits

Inspired by Rain World's Safari mode by Videocult.
