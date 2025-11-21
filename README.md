# Rain World Wallpaper Mode

A Rain World mod that transforms the game into a dynamic wallpaper with smooth transitions between random locations.

## Features

- **Single‑Click Launch**: Adds a “Wallpaper Mode” button to Rain World’s main menu.
- **Hands‑Free Exploration**: Automatically tours rooms with smooth eased camera transitions and time-based region cycling.
- **Smart Region Manager**: Stays in a region for a configurable duration, then reloads into fresh territory.
- **Overlay & HUD**: F1/Tab opens an in-game settings overlay; HUD shows current room/region, next stop, timers, and control hints.
- **Manual Overrides**: Right Arrow/D-pad Right or `N` jump to the next room, `G`/`B` cycle regions, `H` toggles HUD visibility, +/- or PgUp/PgDn adjust dwell time.
- **Camera Modes**: Four camera modes control how views are selected in multi-angle rooms (configurable in Remix or in-game overlay).
- **Remix Preparation**: A Remix tab is included for future configuration (UI present; persistence WIP).

## Camera Modes

The mod supports four camera modes that control how camera positions are selected within rooms:

- **Random Exploration** (default) — When entering a room, randomly picks a starting camera position, then randomly decides how many additional jumps to make (0 to N-1 remaining positions). Each jump goes to a random unvisited position in that room, never repeating. Example: In a 6-position room, might start at position 3, then jump to 5, 1, and 6 before moving to the next room. Provides the most dynamic and varied viewing experience.

- **Single Random** — Picks one random camera position per room and immediately moves to the next room. Quick, unpredictable exploration.

- **All Positions** — Shows all camera positions sequentially (1→2→3→4→5→6). Ensures every angle of a room is seen. Comprehensive but predictable.

- **First Only** — Always uses the first camera position (position 0). Fastest room transitions with consistent framing.

Camera mode can be changed in the Remix menu or via the in-game settings overlay (F1/Tab).

## Installation

1. Ensure you have Rain World v1.9+ (Downpour) installed
2. Install BepInEx if not already installed
3. Copy the entire `mod` folder from `artifacts/bin/RainWorldWallpaperMod/Debug_AnyCPU/mod/` to `RainWorld_Data/StreamingAssets/mods/uzugu.wallpapermod/`
4. Launch Rain World, open the Remix menu, enable “Rain World Wallpaper Mode”, and apply changes (a restart is required)

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

Two configuration surfaces are available:

1. **In-game overlay** (`F1` / `Tab`) — adjust region dwell duration, toggle HUD always-on mode, and review controls without leaving the wallpaper session.
2. **Remix tab** — current build exposes sliders/toggles for future persistence work. Values are displayed but do not yet survive restarts; overlay settings take priority during play.

If you change dwell duration via keyboard controls or overlay, the HUD instantly reflects the new value.



## License

Apache-2.0

## Credits

Inspired by Rain World's Safari mode by Videocult.
