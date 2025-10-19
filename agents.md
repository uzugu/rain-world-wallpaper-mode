# Rain World Wallpaper Mod - Technical Documentation for AI Agents

This document provides comprehensive technical information about the Rain World Wallpaper Mod codebase for AI assistants working on this project.

## Project Management

- Task tracking: Uses Beads system (run `bd quickstart` for usage)
- Version control: Git with standard practices

## Code Contribution Guidelines

When generating commits or code contributions:
- Write clear, descriptive commit messages explaining what changed and why
- Use imperative mood (e.g., "Fix bug" not "Fixed bug")
- Do not add AI attribution in commits - commits should reflect human authorship only
- Avoid co-author tags or AI-generated notices in commit messages

## Project Overview

**Purpose**: Transform Rain World into a dynamic wallpaper with automatic camera transitions between random rooms and regions

**Technology Stack**:
- Platform: BepInEx plugin
- Target Framework: .NET Framework 4.8
- Language: C#
- Game: Rain World (with Downpour DLC support)

## Development Environment

### Build System

#### Standard Build (Native Windows or Linux)

```bash
# Build the mod
dotnet build

# Build with specific configuration
dotnet build -c Debug
dotnet build -c Release

# Output location
# artifacts/bin/RainWorldWallpaperMod/Debug_AnyCPU/mod/
```

#### Build from WSL (Windows Subsystem for Linux)

**Context**: When working in WSL environment, use PowerShell to invoke Windows' dotnet toolchain:

```bash
# Build using Windows dotnet from WSL
powershell.exe -Command "cd 'C:\Users\uzuik\Documents\VRmakes\Projects\RainWorldWallpaperMod'; dotnet build -c Debug"

# Alternative: Build and copy to game in one command
powershell.exe -Command "cd 'C:\Users\uzuik\Documents\VRmakes\Projects\RainWorldWallpaperMod'; dotnet build -c Debug" && \
  cp -v artifacts/bin/RainWorldWallpaperMod/Debug_AnyCPU/mod/plugins/RainWorldWallpaperMod.dll \
    "/mnt/e/SteamLibrary/steamapps/common/Rain World/RainWorld_Data/StreamingAssets/mods/vrmakes.wallpapermod/plugins/" && \
  cp -v artifacts/bin/RainWorldWallpaperMod/Debug_AnyCPU/mod/modinfo.json \
    "/mnt/e/SteamLibrary/steamapps/common/Rain World/RainWorld_Data/StreamingAssets/mods/vrmakes.wallpapermod/"
```

**Rationale for PowerShell approach**:
- WSL environment typically lacks .NET SDK
- Windows-side Visual Studio Build Tools may not include Microsoft.NET.Sdk
- Windows' dotnet provides full .NET Framework 4.8 toolchain access
- PowerShell enables Windows command execution from WSL with proper path translation

### Installation and Deployment

Copy the entire `mod` folder from build output to:
```
RainWorld_Data/StreamingAssets/mods/vrmakes.wallpapermod/
```

#### Installation Target Location

**Deployment path**: `RainWorld_Data/StreamingAssets/mods/vrmakes.wallpapermod/`

#### Quick Deploy from WSL

```bash
# Copy built files to game directory
cp -v artifacts/bin/RainWorldWallpaperMod/Debug_AnyCPU/mod/plugins/RainWorldWallpaperMod.dll \
  "/mnt/e/SteamLibrary/steamapps/common/Rain World/RainWorld_Data/StreamingAssets/mods/vrmakes.wallpapermod/plugins/"

cp -v artifacts/bin/RainWorldWallpaperMod/Debug_AnyCPU/mod/modinfo.json \
  "/mnt/e/SteamLibrary/steamapps/common/Rain World/RainWorld_Data/StreamingAssets/mods/vrmakes.wallpapermod/"
```

**Note**: Adjust Steam library path (`/mnt/e/SteamLibrary/...`) based on actual installation location.

### Dependencies

**Required DLLs** (copy from Rain World installation to `lib/` folder before building):
- `UnityEngine.dll` (from `RainWorld_Data/Managed/`)
- `UnityEngine.CoreModule.dll` (from `RainWorld_Data/Managed/`)
- `UnityEngine.InputLegacyModule.dll` (from `RainWorld_Data/Managed/`)
- `Assembly-CSharp.dll` (from `RainWorld_Data/Managed/`)
- `HOOKS-Assembly-CSharp.dll` (from `BepInEx/plugins/`)

## Architecture

### Core Components

**WallpaperMod.cs** - BepInEx plugin entry point
- Manages On.Hook registrations for Rain World lifecycle events
- Coordinates the transition from main menu to wallpaper mode via `BeginWallpaperMode()`
- Handles world reload requests through `QueueRegionReload()`
- Integrates with Remix config system via `LoadOI()` and `RainWorld_OnModsInit`
- Blocks player updates entirely during wallpaper mode

**WallpaperController.cs** - Central orchestrator for wallpaper experience
- Enforces spectator state: removes player entities and detaches camera from creatures
- Manages two concurrent timers: room transition timer and region dwell timer
- Implements smooth camera transitions with cubic easing between camera positions
- Handles all user input (room skip, region cycling, HUD toggle, settings overlay)
- Coordinates with RegionManager for automatic region changes

**RegionManager.cs** - Region selection and cycling
- Maintains shuffled list of all vanilla + Downpour regions
- Tracks visited regions and rooms explored per region
- Provides forward/backward cycling through region list
- Triggers world reload when region changes

**EchoMusicManager.cs** - Echo music system
- Maps regions to echo music tracks (NA_32 through NA_42)
- Detects Ghost entities in rooms and plays corresponding music
- Scans for and disables AudioHighPassFilter components to prevent distortion
- Must receive OnRoomChanged() notifications from WallpaperController

**WallpaperHUD.cs** - On-screen information display
- Uses Futile directly (adds to `Futile.stage`, not `camera.hud`)
- Displays current region, room, next room, region timer, and stats
- Auto-fades after configurable delay (default 3s), wakes on any input
- Toggle-able "always show" mode via `H` key

**WallpaperSettingsOverlay.cs** - In-game settings panel
- Activated via F1 or Tab key
- Allows live adjustment of region duration and HUD always-show mode
- Changes take effect immediately without restarting wallpaper session

**MenuIntegration.cs** - Main menu button injection
- Hooks `MainMenu.ctor` to add "Wallpaper Mode" button
- Button triggers `WallpaperMod.Instance.BeginWallpaperMode()`

**WallpaperModOptions.cs** - Remix configuration interface
- Provides dropdowns for campaign selection and starting region
- Text boxes for durations (region, transition, stay, HUD fade)
- Checkbox for HUD always-show setting
- Registered with Remix as `vrmakes_wallpapermod`

### Hook Points

The mod hooks into these Rain World methods:
- `RainWorld.OnModsInit` - Register Remix options
- `ProcessManager.RequestMainProcessSwitch_ProcessID` - Intercept game launch
- `RainWorldGame.ctor` - Initialize WallpaperController when entering game
- `RainWorldGame.Update` - Tick wallpaper logic (timers, transitions)
- `RainWorldGame.ShutDownProcess` - Cleanup when exiting
- `RoomCamera.Update` - Apply camera position during transitions
- `Player.Update` - Block all player updates (returns early)

### Key Design Patterns

**Spectator State Enforcement**
The mod removes player entities and detaches camera tracking to create a spectator experience. This happens in `EnsureSpectatorState()` called from the update loop.

**Dual Timer System**
- Room timer: Controls transition between rooms (configurable stay duration)
- Region timer: Controls when to reload world into new region (configurable dwell duration)

**History-Based Room Selection**
SelectRandomRoom() maintains a history of recently visited rooms (MAX_HISTORY = 10) to avoid immediate repeats while ensuring all rooms eventually appear.

**World Reload Flow**
Region changes require full world reload:
1. `RegionManager.AdvanceToNextRegion()` → `SetCurrentRegion()`
2. `controller.OnRegionChanged()` → `PrepareForWorldReload()`
3. `WallpaperMod.Instance.QueueRegionReload()` sets up MenuSetup
4. Triggers `ProcessManager.RequestMainProcessSwitch(Game)`
5. `RainWorldGame.ctor` creates new WallpaperController with new region

**Remix Configuration Surface**
Campaign and region selection use dropdowns (OpComboBox) populated from enums. The config values are strings that map to SlugcatStats.Name and region codes via helper methods.

**Camera Modes**
The mod supports four camera modes that control how camera positions are selected within rooms:

1. **Random Exploration** (default)
   - When entering a room: randomly picks a starting camera position
   - Randomly decides how many additional jumps to make (0 to N-1 remaining positions)
   - Each jump goes to a random unvisited position in that room
   - Never repeats the same position within a room
   - Example: Room has 6 positions, starts at position 3, decides to make 3 more jumps → shows positions 3, 5, 1, 6 (all random, no repeats)
   - Most dynamic and varied viewing experience

2. **Single Random**
   - Picks one random camera position per room
   - Moves to next room immediately
   - Quick, unpredictable exploration

3. **All Positions** (Sequential)
   - Shows all camera positions in order (1→2→3→4→5→6)
   - Ensures every angle of a room is seen
   - Comprehensive but predictable

4. **First Only**
   - Always uses the first camera position (position 0)
   - Fastest room transitions
   - Consistent framing

Implementation details:
- `SelectCameraPosition()` in WallpaperController handles mode logic
- `StartTransitionToRandomRoom()` checks if mode wants to stay in current room
- RandomExploration tracks `unvisitedPositions` list and `remainingJumps` counter
- Sequential tracks `currentCameraPositionIndex` and wraps when exhausted

**Echoes (Spiritual Beings) and Music**

Echoes spawn naturally in wallpaper mode and their music is managed by EchoMusicManager. Key findings:

1. **Natural Spawning**: Echoes appear automatically when the camera visits rooms where they naturally spawn
2. **No Special Code Needed**: The `EnableEchoSpawning()` method exists but is currently disabled (line 511 in WallpaperController.cs)
3. **Player Entity Handling**: Players must be fully removed (RemoveFromRoom + Destroy + Clear Players list) to prevent echo interactions
4. **Echo Music System**: EchoMusicManager handles music playback for all echo rooms
5. **Config Toggle**: `EnableEchoes` checkbox exists in WallpaperModOptions but doesn't actually control spawning since echoes spawn naturally

**EchoMusicManager.cs** - Echo music system
- Maps each region to its echo music track (NA_32 through NA_42 for SH, DS, CC, SI, LF, SB, UW, SL)
- Detects Ghost (echo) entities in realized rooms via `FindEcho()`
- Plays region-specific echo music when entering echo rooms
- Continuously scans for and disables AudioHighPassFilter components that Ghost applies (prevents distorted sound)
- **Critical**: Must be notified via `OnRoomChanged()` when room transitions complete - without this call, no music will play

Implementation notes:
- `EchoMusic.Update()` is called from `WallpaperController.Update()` every frame (WallpaperController.cs:173)
- `EchoMusic.OnRoomChanged(currentTargetRoom)` is called from `CompleteTransition()` (WallpaperController.cs:840)
- Uses reflection to scan all GameObject fields in camera for AudioHighPassFilter components
- Disables filters on both `musicPlayer.gameObj` and any camera GameObject fields

**Important**: If you need to prevent echo interactions with lingering player entities, ensure `EnsureSpectatorState()` calls:
```csharp
realizedPlayer.RemoveFromRoom();
realizedPlayer.Destroy();
Game.Players.Clear();
```

Partial removal (only `RemoveFromRoom()`) leaves players in the `Game.Players` list, allowing echoes to detect and interact with them (including triggering sleep behavior).

## Common Modification Patterns

### Adding a New Region

**Steps**:
1. Add enum value to `RegionChoice` in `WallpaperModOptions.cs`
2. Add display name mapping in `GetRegionDisplayName()`
3. Add to `VANILLA_REGIONS` or `DOWNPOUR_REGIONS` list in `RegionManager.cs`
4. Add start room mapping to `RegionStartRooms` dictionary in `WallpaperMod.cs`

### Adding New Input Controls

**Target**: `HandleInput()` method in `WallpaperController.cs`

**Input Modes**:
- **Settings overlay active**: Limited input (arrow keys for duration adjustment, H for HUD toggle)
- **Normal mode**: Full input set (N, G, B, +/-, PageUp/Down, H, F1, Escape)

### Adjusting Transition Behavior

**Relevant methods**:
- `UpdateTransition()`: Interpolation logic
- `EaseInOutCubic()`: Easing curve function
- `StartTransitionToRandomRoom()`: Room selection logic

### HUD Customization

**Target**: `WallpaperHUD.cs`

**Architecture**:
- Labels are FLabel instances added to a Futile container
- Fade logic in `Update()` method: compares `timeSinceUserActivity` against `fadeDelay`

## User Control Reference

**In-game controls** (available during wallpaper mode):
- `N` - Skip to next room immediately
- `G` - Advance to next region (triggers world reload)
- `B` - Go back to previous region
- `+/-` or `PageUp/PageDown` - Adjust region duration (±60s, ±300s with Shift)
- `H` - Toggle HUD always-show mode
- `F1` or `Tab` - Open/close settings overlay
- `Escape` - Return to main menu

## Technical Constraints and Considerations

### Unity Input System

**Constraint**: Project uses Unity's legacy Input system

**Implementation notes**:
- Avoid `Input.GetKey()` for controller input
- Use `Input.GetAxisRaw()` with debouncing for controller axes
- Keyboard input uses button state tracking for reliable edge detection

### Futile Rendering

**Architecture**: HUD elements bypass `RoomCamera.hud` and add directly to `Futile.stage`

**Rationale**: Ensures UI persistence across room transitions

### Reflection Usage

**Location**: `ConfigureMenuSetupForRegion()` method

**Purpose**: Sets private fields on MenuSetup for region loading

**Justification**: Rain World doesn't expose these fields through public APIs

### Region-Campaign Compatibility

**Constraint**: Different slugcat campaigns have different region availability

**Current behavior**: No validation of region-campaign compatibility

**Risk**: Invalid combinations may fail at world load time

## File Organization

```
RainWorldWallpaperMod/
├── WallpaperMod.cs              # Plugin entry point, hook registration
├── WallpaperController.cs       # Main update loop, transitions, input
├── RegionManager.cs             # Region list management, cycling
├── EchoMusicManager.cs          # Echo music playback and AudioFilter management
├── WallpaperHUD.cs             # Futile-based info overlay
├── WallpaperSettingsOverlay.cs  # F1 settings panel
├── MenuIntegration.cs           # Main menu button injection
├── WallpaperModOptions.cs       # Remix config UI
├── assets/
│   └── modinfo.json            # Remix metadata
└── lib/                        # Rain World DLLs (not in repo)
```

### Documentation Files

- **README.md**: User-facing feature list and installation instructions
- **CLAUDE.md**: Development guidance for Claude Code AI assistant
- **agents.md**: This file - technical documentation for AI assistants
- **DESIGN.md**: Original architecture planning document
- **STATUS.md**: Implementation checklist
- **PROGRESS_V2.md**, **V2_STATUS.md**: Development progress tracking

## Recent Changes

### 2025-10-15 14:14:24 - Clean up logging and document echo spawning behavior

**Changed files:**
- PROGRESS_V2.md
- RainWorldWallpaperMod.sln
- STATUS.md
- V2_STATUS.md
- WallpaperController.cs
- WallpaperHUD.cs
- WallpaperModOptions.cs
- WallpaperSettingsOverlay.cs

**Summary:**
- Removed verbose logging from WallpaperController (transition details, camera position logs)
- Documented echo spawning behavior - echoes work naturally without special code
- Reverted to full player removal (RemoveFromRoom + Destroy + Clear) to prevent echo interactions
- Added comprehensive echo documentation to CLAUDE.md
- Created agents.md for other AI assistants
- Cleaned up code formatting

---

**Document Version**: Updated with echo spawning findings and cleaned logging implementation
**Last Updated**: Session focusing on echo behavior investigation and code cleanup

### 2025-10-15 18:52:00 - Position wallpaper button below main menu with proper alignment

**Changed files:**
- MenuIntegration.cs

**Summary:**
- Repositioned WALLPAPER MODE button to appear below EXIT button instead of in the menu column
- Added MainMenu.Singal hook to handle button click events
- Button now manually positioned 40 pixels below EXIT button, aligned with menu column
- Matches size of existing menu buttons for visual consistency
- Avoids interfering with menu's column layout system

