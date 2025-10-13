# Rain World Wallpaper Mod - Development Status

## Implemented
- BepInEx plugin (`WallpaperMod.cs`) wires Rain World hooks, menu button, and spectator launch flow.
- `WallpaperController` enforces spectator camera state, performs eased room transitions, and manages a time-based region dwell timer (default five minutes).
- `RegionManager` shuffles all vanilla + Downpour regions, tracks visits, and supports manual cycling in both directions.
- `WallpaperHUD` surfaces region, area, next room, next region, progress metrics, and the region timer; it fades after three seconds of inactivity and wakes on any input.
- `WallpaperSettingsOverlay` (F1) exposes live settings for dwell duration and HUD visibility without leaving the session.
- Manual overrides: `N` forces the next room, `G` advances to the next region, `B` returns to the previous region, `+/-` or `PageUp/PageDown` adjust the dwell timer, `Left/Right` (inside the overlay) do the same with Shift for ±5 minutes, `H` toggles HUD always-on mode.
- Main-menu integration adds a "Wallpaper Mode" button that launches directly into the experience.
- Rain Meadow sources remain cloned for reference only; they are excluded from the project build so no third-party dependencies are required.

## Current Controls
- `N` - Jump to the next room immediately.
- `G` - Advance to the next region (queued reload).
- `B` - Step back to the previous region.
- `+` / `-` or `PageUp` / `PageDown` - Increase or decrease the time spent in each region (1-minute steps, 1-30 minute bounds).
- `F1` or `Tab` - Toggle the in-game settings overlay (Left/Right adjust duration, Shift+Left/Right adjust by five minutes, `H` toggles HUD always visible).
- `Escape` - Return to the main menu.

## Remaining Work
- Add Remix OptionInterface for dwell duration, HUD fade, colours, and control hints.
- Persist dwell duration and HUD settings between sessions (BepInEx config or Remix save).
- Broaden input wake detection to include controllers without keyboard focus.
- Optional: HUD customisation toggles, alternate layouts, or compact mode.
- Extended polish: transition effects on region reload, additional camera behaviours, music hooks.

## Known Limitations
- Controller-only setups may not trigger the HUD wake if Unity reports no key/button change.
- Region selection is limited to next/previous cycling (no full list picker yet).
- Settings are not persisted; timer resets to five minutes on each launch.

## Testing Checklist
- `dotnet build` — ensure binaries compile against shipped Rain World DLLs.
- Launch from the main menu, enable wallpaper mode, validate:
  - HUD fades after idle and revives on key/button input.
  - `N`, `G`, `B`, `+/-`, `PageUp/PageDown` respond instantly.
  - F1 overlay opens, reflects dwell duration, and responds to Left/Right and `H`.
  - Region timer hits the configured limit and triggers automatic reload.
  - No crashes during prolonged multi-region runs.

## File Quick Reference
- `WallpaperMod.cs` - plugin lifecycle, menu button, hook registration.
- `WallpaperController.cs` - spectator management, transitions, timers, input handling.
- `RegionManager.cs` - region order, visit tracking, manual cycling.
- `WallpaperHUD.cs` - Futile HUD container, labels, fade logic, control hints.
- `WallpaperSettingsOverlay.cs` - in-game settings panel.
- `MenuIntegration.cs` - main-menu constructor hook.
- `assets/modinfo.json` - Remix metadata.



