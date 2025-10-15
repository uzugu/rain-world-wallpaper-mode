# Rain World Wallpaper Mode - V2.0 Development Status

## Recent Wins
- Eliminated the separate `WallpaperProcess` experiment in favour of the proven hook-based flow in `WallpaperMod`.
- Added input-driven HUD logic that fades after inactivity and wakes on any key/button press.
- Introduced time-based region cycling (default five minutes) with in-game adjustment keys.
- Introduced time-based region cycling (default five minutes) with in-game adjustment keys.
- Added manual overrides: Right Arrow/Dpad Right (or  `D`) and `N` skip to the next room, `G` advances to the next region, `B` returns to the previous region. 
- HUD now surfaces region/room/next information plus timer and control hints.
- Added an F1/Tab settings overlay for adjusting dwell duration (Left/Right, Shift for +/- 5 min) and toggling HUD always-on mode without leaving the session.

## Active Focus
- Build a Remix OptionInterface to expose dwell duration, HUD fade delay, color palette, and toggleable control hints.
- Persist dwell-time and HUD preferences between sessions using BepInEx config or Remix storage.
- Expand HUD wake detection to cover controller/gamepad input consistently.
- Evaluate UI for direct region selection (beyond next/previous cycling) if testers request finer control.

## Upcoming Milestones
1. **V2.0-Beta**
   - Remix sliders/toggles for timers and HUD behaviour.
   - Config persistence.
   - Documentation refresh with control cheatsheet.
2. **V2.1**
   - Enhanced transitions during region reloads (fade overlay, loading tips).
   - Optional HUD themes/layout presets.
   - Experimental camera paths (slow pan, orbit).

## Risks & Considerations
- Without persisted settings, players must reapply dwell-duration tweaks every launch.
- Controller-only users may miss the HUD because Unity’s `anyKeyDown` does not always fire for joystick buttons.
- Manual region cycling is still sequential; may need a grid/list UX later.

## Testing Status
- `dotnet build` succeeds.
- Manual smoke test confirms overrides, HUD fade, and 5-minute region swap.
- Long-run soak pending to ensure no memory creep across repeated region reloads.

## Next Actions
- Prototype OptionInterface skeleton and decide on Remix vs. BepInEx storage backend.
- Audit input layer for controller button detection (likely `Input.GetJoystickNames` / `Input.GetKeyDown` combos).
- Collect feedback on keybind defaults before locking documentation.
