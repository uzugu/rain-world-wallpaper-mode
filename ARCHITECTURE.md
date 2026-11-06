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
  - Wallpaper Engine starts application wallpapers with Unity's `-WINDOWED -parentHWND <its window>`, so the game window becomes a child of WE's `WPEAppIntermediateWorker`. It moves the console window in there too and passes mouse clicks on to those windows. A click on the console started a QuickEdit selection, and while one is open every console write blocks, which froze the game at its next log line. Host mode therefore turns QuickEdit and console mouse input off (`SetConsoleMode` on `CONIN$`), and `MaintainConsole` (from the plugin's `Update`, every 2 s) keeps the console hidden and at the bottom of the host's children, so clicks reach the game window
  - a double-click (left button, within 0.5 s and 24 px, detected per frame in the plugin's `Update` because a press and release can arrive in one frame) opens the settings menu, the only way into it without keys; clicks are not tracked while the menu is open. The HUD's controls line says "Double-click Settings" in host mode
  - puts Rain World's Steam app ID (312520) back into the process environment first thing in `OnEnable`: a launcher that is a Steam app itself (Wallpaper Engine, 431960) passes its own `SteamAppId`/`SteamGameId` on, Steam reads that before `steam_appid.txt`, and the game would then sign in as that app and hang at "Downloading Workshop Content (0%)" waiting for the other app's Workshop items. Plugins load before the game's `SteamManager` starts Steam, which can only happen once per process
  - hooks `Options.OnLoadFinished`, where Rain World applies its saved resolution and fullscreen setting: once a host has embedded the game window (the visible `UnityWndClass` window; under Wallpaper Engine's `-parentHWND` Unity also keeps a hidden 128x128 top-level window of that class, which must not count), the window is left alone and only Futile's render width is set; before that, the original runs forced windowed. The saved options are not changed

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
  - coordinates HUD, settings menu, echoes, and chaos mode

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
  - shows a padlock and "Room locked" at the top right for as long as the room is locked, whatever the rest of the HUD is doing

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

- `EchoManager.cs`
  - wakes the region's echo on a blank save, through a post-hook on `World.SpawnGhost`
  - drives Spinning Top shading from the middle of the screen, where the game uses the followed player
  - runs the echo song with the camera in the player's place, using the presence's own song
  - walks the tour in to the echo once per region and picks the screen that shows it

- `ChaosManager.cs`
  - runs the region's own creature list more times; see "Chaos Multiplies The Region's Own List"
  - levels are exponential: 150 extra creatures at level 5, doubling per level
  - slugpups are not listed in `game.Players`, which stays empty in a wallpaper session: the `Players[0]` lookups a pup can reach are all behind `ModManager.CoopAvailable`, and anyone in that list can start an echo's ending or overrun `ScavengersWorldAI`'s arrays of four

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
- `StoryGameSession.ctor`
- `RainWorldGame.Update`
- `RainWorldGame.ShutDownProcess`
- `RoomCamera.Update`
- `Room.Loaded`
- `Player.Update`
- `Overseer.Update`
- `World.SpawnGhost`
- `ShortcutHandler.SuckInCreature`
- `World.ActivateRoom`
- `RoomCamera.NewObjectInRoom`
- `PlayerProgression.SaveDeathPersistentDataOfCurrentState`
- `PlayerProgression.SaveProgression`
- `MainMenu.ctor`
- `MainMenu.Update`

### Hook Notes

- `RainWorldGame.Update` sets the game's private `lastPauseButton` field to `true` before the original update runs. The vanilla pause menu only opens on a new press of the pause button, so it never opens in a wallpaper session and the pause button (Escape, Start) is free for the settings menu and for leaving. If the field cannot be found, one warning is logged and the pause menu is closed again after it opens instead. A pause opened by the Steam overlay is left alone. The same hook then runs `WallpaperController.Update` once per game tick.
- `RainWorldGame.ShutDownProcess` runs `WallpaperController.Shutdown`, which stops the settings menu together with the rest of the UI.
- `StoryGameSession.ctor` turns Jolly Co-op off (`ModManager.CoopAvailable = false`) while a wallpaper game is being built, as the `RainWorldGame` constructor itself does for Safari mode. A wallpaper session has no players; with co-op on, the session would spawn every Jolly player, the HUD would add the Jolly parts, and the co-op camera would follow whatever is left in `game.Players` (chaos slugpups). The main menu and the next story game turn co-op back on.

## Key Design Decisions

### Wallpaper Sessions Never Save

Wallpaper sessions start through `StoryGameInitCondition.RegionSelect`, so the game builds a blank save state for the selected campaign and pre-saves its death-persistent data (karma, karma cap, echoes, passages) "as a death" while the `RainWorldGame` constructor runs. Without a guard, every session start and region reload would overwrite that campaign's real progress. The save hooks skip all progression saves while a wallpaper game is being built or is running. The vanilla pause menu is kept from opening (see Hook Notes), because its EXIT also saves as a death.

### Game Ticks, Not Frames

`RainWorldGame.Update` runs once per game tick (`framesPerSecond`, normally 40), not once per rendered frame. All wallpaper timers advance by `1 / framesPerSecond` per tick, so durations stay in real seconds at any monitor refresh rate.

### Spectator-State Enforcement

The mod continuously removes or suppresses player-controlled slugcats while allowing slugpups and NPC-style slugcats to keep updating. This is necessary for a wallpaper presentation that still allows the world to feel alive.

### Rooms The Wallpaper Uses

`WallpaperRoomTracker.IsSafeWallpaperRoom` decides which rooms the camera tours, chaos spawns into and the settings menu offers for travel. It leaves out gates, the off-screen den, the oracle chambers (their room scripts start story events that expect a player) and every room in `World.DisabledMapRooms`. Rain World's world loader keeps another campaign's exclusive rooms (`EXCLUSIVEROOM`) and the rooms the current campaign hides (`HIDEROOM`) in the world as unreachable rooms whose exits lead nowhere back, and only lists them there; the game itself uses that list to keep creature spawns and overseers out of them.

### Chaos Multiplies The Region's Own List

A region's world file has a `CREATURES` list per campaign, which the game reads into `World.spawners`. `WorldLoader.GeneratePopulation` runs through it once and puts each creature in the den its spawner names. Chaos does the same thing more times: it picks a random den place from the list (a place appears once per creature the list makes, so the mix stays the region's), creates the creature at that den with the spawner's data and calls `MoveEntityToDen`. The game lets it out when it is ready, through the den's pipe if the room is loaded. For a lineage, a den whose creature changes over the cycles, any creature of the chain can come out.

This keeps every creature in a place the game put it, per campaign and in custom regions too, so there is no list of creature types in the mod. A blacklist or a table of guests would be a guess: the old ones kept out Monster Kelp and Pole Mimics, which live in 25 regions each, and brought in scavengers and slugpups where the game has none (19 and 31 of the 51 regions). `CHAOS_CREATURES.md` shows what the lists hold.

Nothing steers the creatures. Only the room on screen is realized, so whether the camera meets one is chance, and the numbers do the work: levels are exponential, 150 extra creatures at level 5 and double per level, up to 4800 at level 10. `ChaosManager` keeps the region at that number, so whoever dies is replaced.

- `ignoreCycle` is set and `preCycle` cleared on every chaos creature. Otherwise those from a `{Night}` or `{PreCycle}` line would wait in their dens for an hour that never comes, and the whole crowd would leave for the dens in the last minute of the cycle.
- Swarm creatures (`quantified`: batflies, leeches, coalescipedes) are only a count per room until the room loads, so they stay as the game has them.
- Slugpups are not on the lists. The game gives each region a chance per cycle (`regionParams.slugPupSpawnChance`) and only in the Survivor, Hunter and Gourmand campaigns (`StoryGameSession.slugPupMaxCount`). That chance is their share of the extra creatures, up to half. The game's other condition, a beaten Gourmand campaign, is left out, because a wallpaper should not depend on the save.
- A slugpup is a `Player`, and `ShortcutHandler.SuckInCreature` loads the room behind any Player entering a room exit. Nothing would unload that room, so `WallpaperMod` stops that load and the pup carries on in the abstract world, the way other creatures leave a room.
- The room a session starts in is unloaded on the first move, like any room the camera leaves. The tracker has no previous room yet, so `WallpaperTransitionController.CompleteTransition` takes it from the camera.
- The `Every creature` setting makes half of the extra creatures come from every template in the game, placed in a random room that allows them and has nodes they can use. Creatures that only work in their own place stay out of that (`NeedsOwnDen`). `AddEntity` only registers a creature, so these go into rooms that are not loaded.
- The game cannot draw everything it has a template for. `GrappleSnakeGraphics` leaves one sprite slot empty, so any GrappleSnake throws when a camera sees it, and the camera then retries that room change on every update, which stops the wallpaper for good. No region's list has one, so `NeverSpawned` leaves it out, and `WallpaperMod` catches what `RoomCamera.NewObjectInRoom` throws, so any other creature the game cannot draw goes undrawn instead.

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
