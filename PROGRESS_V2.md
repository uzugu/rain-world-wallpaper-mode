# Rain World Wallpaper Mode - V2.0 Progress

## Current Build Snapshot
- `WallpaperMod.cs` owns plugin lifecycle, Rain World hook registration, and menu launch.
- `WallpaperController.cs` manages spectator setup, smooth camera transitions, and a configurable five-minute dwell per region.
- `RegionManager.cs` shuffles all vanilla + Downpour regions, tracks visits, and supports manual forward/back cycling.
- `WallpaperHUD.cs` surfaces region, area, next room, next region, and region timer; it fades after three seconds and pops back in when any input is detected.
- `MenuIntegration.cs` injects the "Wallpaper Mode" button directly into the main menu.
- `WallpaperSettingsOverlay.cs` provides an in-game configuration panel (F1) for quick tweaks.

### Fresh Additions
- HUD visibility is now driven by keyboard/mouse/controller input instead of mouse motion alone.
- Manual overrides: Right Arrow/Dpad Right (or  `D`) now jump to the next room, `N` remains as an alternative, `G` advances to the next region, and `B` returns to the previous region. 
- Time-based region cycling (default five minutes) replaces the old room-count system and can be adjusted in-game with  `+/-` or `PageUp/PageDown`; the HUD reflects the timer in real time. 
- New F1/Tab overlay lets you adjust dwell duration with Left/Right (Shift for +/- 5 min) and toggle the HUD's always-on mode without leaving the game.
- Rain Meadow sources remain checked out for reference, but the build now excludes `external/` so downstream dependencies (Steamworks, Rewired, etc.) are no longer required.

## What Still Needs Attention

### High Priority
- Persistable configuration via Remix OptionInterface (region duration, transition timing, HUD fade delay, color themes).
- HUD customization toggles (always-on mode, hide control hints, alternate colour/font).
- Broader input detection for gamepads/Steam Deck so the HUD wakes on button presses.
- Save/load of preferred dwell duration and HUD options between sessions.

### Medium Priority
- Additional camera behaviours (preset fly-throughs, slow pan paths, creature-follow).
- Optional on-screen region picker for manual jumps beyond next/previous.
- Enhanced fades or loading states during region reload to mask hitching.

### Low Priority / Stretch
- Music or ambience blending per region.
- Creature spotlight / postcard framing modes.
- Timed screenshot capture or export automation.

## Testing & Validation
- `dotnet build` succeeds with the current codebase.
- Manual playtests confirm: HUD fades after idle, reappears on any key/button, and override keys respond instantly.
- Long-run soak needed to verify the five-minute timers continue to trigger reloads without memory creep.

## Open Questions
- Is a full region selection UI required, or are next/prev shortcuts sufficient for V2?
- Preferred persistence surface: BepInEx config, Remix sliders, or both?
- Should we expose keybinding remaps before release, or document defaults only?

## Status
**Version**: 2.0-alpha (interactive)

**State**: Playable with manual overrides and time-based region cycling

**Next Milestone**: Add Remix options for dwell timers, HUD behaviour, and control remapping
