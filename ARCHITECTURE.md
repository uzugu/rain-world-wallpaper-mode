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
   - HUD updates and opening or closing the settings menu
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
  - keeps the pause button from opening the vanilla pause menu during wallpaper sessions

- `WallpaperHostMode.cs`
  - the game is launched by a wallpaper host (Wallpaper Engine, Lively) that embeds its window as the desktop wallpaper: detected from the parent process (`wallpaper32/64.exe`, `ui32.exe`, `lively.exe`, `livelywpf.exe`, through a Toolhelp process snapshot), or asked for with `--wallpaper-host` (alias `--wallpaper-engine`). The launcher is logged on every start
  - turns on `Application.runInBackground`, hides the BepInEx console window, and keeps the game windowed
  - hooks `Options.OnLoadFinished`, where Rain World applies its saved resolution and fullscreen setting: once a host has embedded the game window (class `UnityWndClass`), the window is left alone and only Futile's render width is set; before that, the original runs forced windowed. The saved options are not changed

- `WallpaperEngineSetup.cs`
  - the Remix page's WALLPAPER ENGINE button: finds Wallpaper Engine through the Steam libraries (registry `SteamPath`, `libraryfolders.vdf`, the game's own library) and writes `projects\myprojects\rainworld_wallpaper_mode\project.json` (type `application`, pointing at the game's executable, UTF-8 without BOM) with the mod's thumbnail as `preview.png`

- `WallpaperSound.cs`
  - the Game sound setting: with it off, a wallpaper session sets the master volume (`AudioListener.volume`, through Futile's `FSoundManager.volume`) to zero
  - applied when each wallpaper session starts and from the settings menu toggles, and undone when the main menu opens; the game never sets or saves the master volume itself

- `WallpaperController.cs`
  - central runtime coordinator
  - owns the active room/region state
  - enforces spectator mode
  - drives transitions and input
  - coordinates HUD, settings menu, echo music, and chaos mode

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
  - draws into the camera's `HUD2` layer (`RoomCamera.ReturnFContainer`), above the room and below menus
  - uses the game's fonts, menu colours and FlatLight shadows: location at the bottom left, rain state and region progress at the bottom right, controls hint at the bottom centre
  - announces a new region, and a new sub-region once the camera has settled in it, the way the game's `SubregionTracker` does
  - fades out after the idle delay unless it is set to always visible

- `WallpaperHudParts.cs`
  - the HUD's text lines, independently fading blocks and text helpers

- `WallpaperSettingsMenu.cs`
  - in-game settings and quick-travel panel, a `Menu.Menu` built from the game's own menu objects (labels, page-arrow buttons, checkboxes, buttons and a rounded rect)
  - works with the mouse, the arrow keys and controllers like the game's other menus
  - holds the quick-travel selection lists and applies travel (campaign, region, room, camera mode) from its `TRAVEL` button
  - exposes the Watcher world state, room lock, chaos, HUD, and rain settings

- `WallpaperMinimalMenu.cs`, `WallpaperMinimalMenuParts.cs`
  - the minimalistic style of the same menu: a text-only column of `MinimalRow`s over a soft shade (`MinimalShade`)
  - every row is a `ButtonTemplate`, so the mouse, the arrow keys and controllers navigate it like any other game menu
  - same settings, selection logic and travel behaviour as the panel

- `WallpaperUiController.cs`
  - owns the HUD and the settings menu of one wallpaper session
  - opens the menu (creates it and adds it to `ProcessManager.sideProcesses`) and closes it (`StopSideProcess`)
  - picks the menu style each time it opens the menu from `WallpaperMod.CurrentMenuStyle`: the `--wallpaper-menu-style` override, or else the Remix **Menu style** setting
  - stops the menu when the session reloads or shuts down, because side processes outlive the game process

- `WallpaperModOptions.cs`
  - Remix configuration UI, laid out by the nested `RemixPage` helper as cards (TRAVEL, CREATURES, TIMING, DISPLAY) that mirror the settings menu's sections, plus a CONTROLS card
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

### Hook Notes

- `RainWorldGame.Update` sets the game's private `lastPauseButton` field to `true` before the original update runs. The vanilla pause menu only opens on a new press of the pause button, so it never opens in a wallpaper session and the pause button (Escape, Start) is free for the settings menu and for leaving. If the field cannot be found, one warning is logged and the pause menu is closed again after it opens instead. A pause opened by the Steam overlay is left alone. The same hook then runs `WallpaperController.Update` once per game tick.
- `RainWorldGame.ShutDownProcess` runs `WallpaperController.Shutdown`, which stops the settings menu together with the rest of the UI.

## Key Design Decisions

### Wallpaper Sessions Never Save

Wallpaper sessions start through `StoryGameInitCondition.RegionSelect`, so the game builds a blank save state for the selected campaign and pre-saves its death-persistent data (karma, karma cap, echoes, passages) "as a death" while the `RainWorldGame` constructor runs. Without a guard, every session start and region reload would overwrite that campaign's real progress. The save hooks skip all progression saves while a wallpaper game is being built or is running. The vanilla pause menu is kept from opening (see Hook Notes), because its EXIT also saves as a death.

### Game Ticks, Not Frames

`RainWorldGame.Update` runs once per game tick (`framesPerSecond`, normally 40), not once per rendered frame. All wallpaper timers advance by `1 / framesPerSecond` per tick, so durations stay in real seconds at any monitor refresh rate.

### Spectator-State Enforcement

The mod continuously removes or suppresses player-controlled slugcats while allowing slugpups and NPC-style slugcats to keep updating. This is necessary for a wallpaper presentation that still allows the world to feel alive.

### Rain-Driven Region Traversal

The original fixed region dwell concept has been replaced in practice by rain-cycle-driven transitions. Documentation and future work should treat the rain cycle as the authoritative region-change trigger unless the implementation changes again.

### HUD In The Camera's HUD Layer

The camera only builds a vanilla `HUD.HUD` when it follows a player, and vanilla code dereferences `hud.textPrompt` and `hud.map` whenever one exists, so the wallpaper never creates one. Its HUD draws straight into the camera's `HUD2` layer instead: screen space, above the room, below menus, and kept across room changes until the game shuts down.

### The Settings Menu Is A Side Process

The settings menu is a regular game menu (`Menu.Menu`) that `WallpaperUiController` adds to `ProcessManager.sideProcesses`, the way the game drives its own arena overlay. The game process keeps updating underneath it, so the wallpaper never pauses. A `Dialog` (`ProcessManager.ShowDialog`) is not used, because an open dialog blocks process switches and region reloads depend on them.

A side process outlives the game process, so it has to be stopped explicitly. `WallpaperUiController.ResetForReload` (region reloads) and `Shutdown` (reached from the `RainWorldGame.ShutDownProcess` hook through `WallpaperController.Shutdown`) both close it. The menu also asks to be closed when a process switch is pending, and the controller does not open it then.

While the menu is open, `WallpaperController` reacts only to the toggle key (`F1`, `Tab`, controller Start) and Escape, and both close the menu. The other hotkeys keep tracking their buttons but do nothing, so a key that is still held when the menu closes is not read as a new press. Escape works the same way: its state is tracked every tick, so closing the menu with it does not also return to the main menu.

### Full Reload For Region Changes

Cross-region travel is not a lightweight teleport. The mod prepares state for a full process-level world reload, then resumes wallpaper mode in the new region.

## Testing Aids

- `--wallpaper-open-settings` opens the settings menu once the first wallpaper session of the process has finished initialising its UI, so the menu can be put on screen without any input. That session uses up the request, so region reloads do not open the menu again. The flag does not start a session by itself, so combine it with `--wallpaper`:

  ```text
  --wallpaper --wallpaper-open-settings
  ```

- `--wallpaper-menu-style panel|minimal` overrides the Remix **Menu style** setting for that process, so both styles can be compared without touching the config. It does not start a session by itself either:

  ```text
  --wallpaper --wallpaper-open-settings --wallpaper-menu-style minimal
  ```

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
