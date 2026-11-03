# Rain World Wallpaper Mode - Architecture

This document describes the current code structure and runtime flow for the mod.

## Purpose

The mod turns Rain World into a hands-free animated wallpaper by launching into a playable world, removing player control, and continuously driving camera movement, room changes, region traversal, music behavior, HUD rendering, and optional creature spawning.

## Runtime Flow

1. `MenuIntegration` injects a `WALLPAPER MODE` button into the main menu.
2. Clicking the button calls `WallpaperMod.BeginWallpaperMode`.
3. `WallpaperMod` prepares `MenuSetup`, records the requested region, and switches to the game process.
4. `RainWorldGame.ctor` creates a `WallpaperController`.
5. `WallpaperController.Update` becomes the main driver for:
   - spectator-state enforcement
   - input handling
   - room transition timing
   - rain-based region changes
   - HUD and overlay refresh
   - echo and chaos updates
6. Region changes trigger a full world reload through `WallpaperMod.QueueRegionReload`.

## File Map

### Core Runtime

- `WallpaperMod.cs`
  - BepInEx entry point
  - installs hooks
  - registers Remix options
  - launches wallpaper mode from the main menu
  - handles region reload setup and campaign advancement

- `WallpaperController.cs`
  - central runtime coordinator
  - owns the active room/region state
  - enforces spectator mode
  - drives transitions and input
  - coordinates HUD, overlay, echo music, and chaos mode

- `RegionManager.cs`
  - maintains region order
  - tracks visited regions
  - supports next, previous, forced, and random-unvisited selection
  - notifies the controller when region changes are requested

### UI

- `MenuIntegration.cs`
  - adds the `WALLPAPER MODE` button above `EXIT` through `MainMenu.AddMainMenuButton`, which handles layout, controller navigation and the click callback
  - starts the command-line auto-launch from the main menu's first update

- `WallpaperHUD.cs`
  - Futile-based HUD added directly to `Futile.stage`
  - shows region, room, next room, next region, and rain-cycle state
  - supports fade-after-idle or always-visible behavior

- `WallpaperSettingsOverlay.cs`
  - Futile-based in-game settings and quick-travel overlay
  - exposes campaign, region, camera mode, room, lock, chaos, and rain settings

- `WallpaperModOptions.cs`
  - Remix configuration UI
  - binds persisted settings
  - defines campaign/camera/state choices and builds the region selector from installed campaign APIs

- `WallpaperRegionCatalog.cs`
  - queries Rain World's Story/Optional region APIs (including CRS hooks)
  - reads campaign-specific region display names
  - resolves authored and validated world-entry rooms

### Audio and Atmosphere

- `EchoMusicManager.cs`
  - detects echo rooms
  - starts and fades echo music by room proximity
  - disables echo-applied audio filters that distort playback

- `ChaosManager.cs`
  - optional creature spawning system
  - scales spawn timing and caps by chaos level
  - re-analyzes regions after reloads
  - protects the wallpaper session from problematic creature types through a blacklist unless `Spawn All` is enabled

### Packaging

- `assets/modinfo.json`
  - Rain World mod metadata copied into the packaged mod folder

- `RainWorldWallpaperMod.csproj`
  - .NET Framework 4.8 project
  - references Rain World and Unity DLLs from `lib/`
  - copies the built DLL and `assets/` into `artifacts/bin/RainWorldWallpaperMod/<Configuration>_AnyCPU/mod/`

## Hook Surface

The current runtime is built around these hooks:

- `RainWorld.OnModsInit`
- `ProcessManager.RequestMainProcessSwitch_ProcessID`
- `RainWorldGame.ctor`
- `RainWorldGame.Update`
- `RainWorldGame.ShutDownProcess`
- `RoomCamera.Update`
- `Room.Loaded`
- `Player.Update`
- `Overseer.Update`
- `PlayerProgression.SaveDeathPersistentDataOfCurrentState`
- `PlayerProgression.SaveProgression`
- `MainMenu.ctor`
- `MainMenu.Update`

## Key Design Decisions

### Wallpaper Sessions Never Save

Wallpaper sessions start through `StoryGameInitCondition.RegionSelect`, so the game builds a blank save state for the selected campaign and pre-saves its death-persistent data (karma, karma cap, echoes, passages) "as a death" while the `RainWorldGame` constructor runs. Without a guard, every session start and region reload would overwrite that campaign's real progress. The save hooks skip all progression saves while a wallpaper game is being built or is running. The vanilla pause menu that the pause button opens is closed again, because its EXIT also saves as a death.

### Game Ticks, Not Frames

`RainWorldGame.Update` runs once per game tick (`framesPerSecond`, normally 40), not once per rendered frame. All wallpaper timers advance by `1 / framesPerSecond` per tick, so durations stay in real seconds at any monitor refresh rate.

### Spectator-State Enforcement

The mod continuously removes or suppresses player-controlled slugcats while allowing slugpups and NPC-style slugcats to keep updating. This is necessary for a wallpaper presentation that still allows the world to feel alive.

### Rain-Driven Region Traversal

The original fixed region dwell concept has been replaced in practice by rain-cycle-driven transitions. Documentation and future work should treat the rain cycle as the authoritative region-change trigger unless the implementation changes again.

### Futile-First UI

Both the HUD and settings overlay render directly through Futile rather than the room HUD stack. This keeps them stable across camera changes and world transitions.

### Full Reload For Region Changes

Cross-region travel is not a lightweight teleport. The mod prepares state for a full process-level world reload, then resumes wallpaper mode in the new region.

## Build and Packaging

The packaged mod output is:

`artifacts/bin/RainWorldWallpaperMod/<Configuration>_AnyCPU/mod/`

Expected package contents:

- `modinfo.json`
- `plugins/RainWorldWallpaperMod.dll`

The project depends on DLLs in `lib/`, which are not committed to the repo.

## Documentation Status

- `README.md` is the primary public-facing overview.
- `HOW_TO_USE.md` is the current user guide.
- `WORKSHOP_PUBLISHING.md` is the release checklist.

## Maintenance Notes

- Keep mod identifiers aligned across plugin metadata, Remix registration, packaged folder naming, and Workshop publishing.
- When changing controls, update both `README.md` and `HOW_TO_USE.md`.
- When changing region logic, update both this file and the user-facing workflow docs.
