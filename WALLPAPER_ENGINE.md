# Wallpaper Engine Integration

This mod integrates with Wallpaper Engine by launching Rain World as an application wallpaper.

## Required Setup

1. Install BepInEx for Rain World.
2. Install this mod into Rain World's `StreamingAssets/mods/...` folder.
3. Enable the mod in Remix once from a normal game launch.
4. In Wallpaper Engine, create or configure an application wallpaper that launches `RainWorld.exe` or a Steam launch for Rain World.

The mod must already be installed and enabled. Wallpaper Engine only launches the game process; it does not inject the mod by itself.

## Launch Arguments

Use these launch arguments for Rain World:

```text
--wallpaper
```

Optional overrides:

```text
--wallpaper --wallpaper-region SU --wallpaper-campaign White
```

Supported values:

- `--wallpaper-region`: Rain World region codes such as `SU`, `HI`, `CC`, `GW`, `SH`, `DS`, `SL`, `SI`, `LF`, `UW`, `SS`, `SB`, `LM`, `RM`, `DM`, `LC`, `MS`, `VS`, `CL`, `OE`
- `--wallpaper-campaign`: `White`, `Yellow`, `Red`, `Gourmand`, `Artificer`, `Rivulet`, `Spearmaster`, `Saint`, `Watcher`

Behavior:

- `--wallpaper` auto-starts wallpaper mode from the main menu without a click.
- Region and campaign overrides apply to that process only and do not change your Remix settings.
- The campaign override covers the whole session (region tour, HUD names, Watcher world states) until you pick another campaign in the overlay or the tour advances to the next campaign.
- Auto-launch is one-shot per process, so using `Escape` to return to the menu will not immediately restart wallpaper mode.

## Recommended Wallpaper Engine Configuration

Use the application wallpaper approach, then tune Wallpaper Engine around Rain World:

- launch Rain World borderless or windowed if needed
- mute or lower audio if the wallpaper should stay silent
- pause or stop when fullscreen applications are active
- keep this mod's HUD fade enabled unless you want persistent overlay text

## Limits

- This is still Rain World running normally, so it keeps the game's CPU/GPU cost profile.
- Stability depends on the installed mod set. For wallpaper use, keep the stack minimal.
- If you want a cleaner wallpaper product later, the next step is startup polish: borderless launch, focus behavior, audio defaults, and optional low-activity throttling.
