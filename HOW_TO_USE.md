# Rain World Wallpaper Mode - User Guide

This guide describes the current workflow for the mod as implemented in the codebase today.

## Starting Wallpaper Mode

1. Launch Rain World with the mod enabled in Remix.
2. From the main menu, click `WALLPAPER MODE`.
3. The mod starts a normal game process in the selected campaign/region and immediately takes over as a spectator-style wallpaper experience.

There is no in-game `F9` toggle workflow in the current implementation.

## What Happens In Wallpaper Mode

- The camera is detached from player creatures.
- Player-controlled slugcats are suppressed so the game can run hands-free.
- The mod automatically tours rooms in the current region.
- Region changes are driven by the rain cycle instead of a fixed dwell timer.
- Echo music and optional chaos spawning continue to run while the wallpaper is active.

## Region Changes

Wallpaper mode uses the active region's rain cycle:

- Standard mode: once the cycle reaches 85%, a random countdown starts before changing regions.
- No Rain Wait mode: the mod skips the countdown and changes regions at 95% cycle completion.
- When every region in the current campaign has been visited, the mod advances to the next campaign automatically.

## Controls

### Normal Wallpaper Mode

| Key | Action |
| --- | --- |
| `Right Arrow` / `D` | Jump to the next room immediately |
| `Left Arrow` / `A` | Go back to the previous room in history |
| `Up Arrow` / `W` | Next camera position in the current room |
| `Down Arrow` / `S` | Previous camera position in the current room |
| `N` | Force an immediate random room change |
| `G` | Force next region |
| `B` | Force previous region |
| `L` | Toggle room lock |
| `H` | Toggle HUD always visible |
| `F1` / `Tab` | Open or close the settings menu |
| `Escape` | Close the settings menu if it is open, otherwise return to the main menu |

### Settings Menu

The settings menu is a panel on the left side of the screen. It is built from Rain World's own menu controls, so it behaves like the game's other menus, and the wallpaper keeps running on the right while it is open.

- Move around with the mouse, the arrow keys or a controller (stick or d-pad), and press the selected control with the menu's confirm button. The menu's back button (usually `B` on a controller) jumps the selection to `CLOSE`.
- Press the arrows on either side of a setting to cycle its value. Checkboxes toggle right away.
- `TRAVEL` applies the selected campaign, region, room and camera mode, then closes the menu.
- `CLOSE` closes the menu without applying them.
- `F1` / `Tab` (`Start` on a controller) and `Escape` also close the menu. No other wallpaper key (room, camera, `N`, `G`, `B`, `L`, `H`) does anything while it is open.

## Settings Menu Features

The top of the panel shows the rain state ("Rain in 2m 31s", "Waiting for the rain (43%)" or, with No Rain Wait, "No Rain Wait: 43% / 95%") and how many of the campaign's regions have been visited.

`TRAVEL`:

- Campaign
- Region
- Room: a room of the current region. While another region is selected its arrows are greyed out; press `TRAVEL` to go to that region, then open the menu again to pick a room there.
- Camera: the camera mode
- Watcher world: Natural Mix, Clean, Rotted, Karma Flowers, or Rot + Karma. Greyed out unless the Watcher campaign is selected.
- Lock room

`CREATURES`:

- Chaos creatures toggle
- Chaos level (1 to 10)
- Spawn every creature (experimental)

`DISPLAY`:

- Always show HUD
- No Rain Wait

Campaign, region, room and camera mode are applied when you press `TRAVEL`. Watcher world, room lock, always show HUD and No Rain Wait apply immediately. Creature changes fully take effect on the next region load.

## Watcher World States

The Watcher state selector uses the DLC's room systems:

- `Natural Mix`: generates a session-only Watcher-style rot mixture for the current region and refreshes that pattern while you watch.
- `Clean`: clears wallpaper-injected Watcher rot and karma-flower state.
- `Rotted`: applies sentient rot infection to rooms through Watcher's region state.
- `Karma Flowers`: adds cosmetic karma flower patches and flower wind to loaded rooms.
- `Rot + Karma`: combines sentient rot infection, karma flower patches, and flower wind in the same room.

Some Watcher rooms have rot, flowers, or special objects authored directly into their room files. The mod does not erase those original room designs.

## Camera Modes

- `Random Exploration`: picks a random starting camera position and may visit additional unvisited positions before leaving the room.
- `Random`: uses one random camera position per room.
- `Sequential`: walks through all camera positions in order.
- `First Only`: always uses camera position `0`.

## Chaos Mode

Chaos mode can populate rooms with creatures for a more active wallpaper:

- level `1` through `10` controls spawn interval and creature cap
- slugpups and scavengers are intentionally common
- `Spawn All` (Spawn every creature in the settings menu) bypasses the normal creature blacklist and is explicitly experimental

Chaos changes are safest to evaluate after a region reload.

## Echoes

Echoes can appear naturally in eligible rooms. When this happens:

- the mod detects the echo room
- echo music fades based on room proximity
- audio high-pass filters applied by the echo are disabled to avoid distorted playback

## Troubleshooting

### Wallpaper button does not appear

- Confirm the mod is enabled in Remix.
- Confirm `modinfo.json` and `plugins/RainWorldWallpaperMod.dll` are both present in the installed mod folder.
- Open `BepInEx/LogOutput.log` and find `Wallpaper Mod initialized with game`. The line now records exact active mod IDs and versions; include it when reporting a compatibility problem.

### A region falls back, freezes, or has the wrong name

- Region names are read from Rain World's campaign-specific `DisplayName` files.
- Region availability is read from the game's Story/Optional lists; CRS custom regions must be marked compatible in their `MetaProperties.txt`.
- Start rooms use the region author's `safariStartRoom` when present and otherwise must exist in the resolved world file.
- Constructor failures log the selected campaign, region, resolved start room, active mod versions, and full exception in `BepInEx/LogOutput.log`.

### The mod builds but Rain World does not load it

- Verify the built package was copied from `artifacts/bin/RainWorldWallpaperMod/<Configuration>_AnyCPU/mod/`.
- Verify the installed folder contains:
  - `modinfo.json`
  - `plugins/RainWorldWallpaperMod.dll`

### Controls do nothing after starting

- Wait for the world to finish loading.
- If the game is mid-transition or reloading a region, some actions are intentionally ignored.

### Echo or chaos behavior seems inconsistent

- Both systems depend on room and region state.
- After changing settings, let the next room or region load complete before judging the result.

## Related Docs

- `README.md`: project overview, build, installation
- `ARCHITECTURE.md`: code/file map and system overview
- `WORKSHOP_PUBLISHING.md`: release and Steam Workshop checklist
