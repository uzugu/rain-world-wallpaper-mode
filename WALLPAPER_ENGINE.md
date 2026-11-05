# Running Rain World As Your Desktop Wallpaper

The mod can turn Rain World into a live desktop wallpaper through a wallpaper host such as Wallpaper Engine or the free Lively Wallpaper. The host starts the game, takes its window and puts it behind your desktop icons. The mod starts wallpaper mode by itself and makes the game behave like a wallpaper program.

## Quick Setup With Wallpaper Engine

1. Start Rain World normally, open **Remix**, and select **Rain World Wallpaper Mode** to open its settings.
2. Click **WALLPAPER ENGINE** in the top right corner. The line under the title confirms that "Rain World Wallpaper Mode" was added to Wallpaper Engine.
3. Close Rain World.
4. In Wallpaper Engine, pick **Rain World Wallpaper Mode** (restart Wallpaper Engine if it is not listed yet). Wallpaper Engine asks once before it runs an application wallpaper.

Rain World starts in the background, and the wallpaper tour begins once the game reaches its main menu, usually after half a minute. The wallpaper uses the mod's Remix settings.

## How It Works

The host has to start `RainWorld.exe` itself. When the program that starts the game is Wallpaper Engine or Lively, the mod notices and runs as a hosted wallpaper. For other hosts, add the `--wallpaper-host` launch argument (or its alias `--wallpaper-engine`). As a hosted wallpaper, the mod:

- starts wallpaper mode from the main menu without a click
- keeps the game running when it does not have focus, which a wallpaper never has
- hides the BepInEx console window, so the game shows a single window, and turns off the console's QuickEdit so a click can never pause the game
- opens the settings menu on a double-click on the wallpaper
- keeps the game windowed, and leaves its window alone once the host has taken it

### Why A Steam Launch Does Not Work

`steam.exe -applaunch 312520 ...` (or a `steam://` link) only asks the running Steam client to start the game. The process the host started exits right away, and the game starts as a separate process the host never sees, so it opens as a normal window instead of becoming the wallpaper. Point the host at `RainWorld.exe` instead. Steam still has to be running, because the game uses it.

## Before You Start

1. Enable the mod in Remix once, from a normal game launch.
2. Recommended: turn off the BepInEx console. In `Rain World\BepInEx\config\BepInEx.cfg`, under `[Logging.Console]`, set `Enabled = false`. The mod hides the console once it has loaded, but the console opens a few seconds earlier, and a host may take it instead of the game window.
3. Find your Rain World folder: in Steam, right-click Rain World, then Manage > Browse local files.

## Wallpaper Engine By Hand

The Remix button above does this for you. To set it up yourself instead, choose **Create Wallpaper** in Wallpaper Engine and pick `RainWorld.exe` from the Rain World folder. No launch arguments are needed. This makes an application wallpaper; application wallpapers stay on your PC and cannot be shared on the Workshop.

If Rain World was last run fullscreen, set it to windowed in the game's options first, or point Wallpaper Engine at a shortcut whose target adds `-screen-fullscreen 0`.

Wallpaper Engine's own settings (Performance) decide whether the wallpaper pauses or stops while another program is maximized or fullscreen. When it pauses, it freezes the whole game process, so a wallpaper picked while a maximized window is in front does not start, and looks stuck, until that window is gone.

## Lively Wallpaper

Lively has no field for launch arguments, so the wallpaper is described in a `LivelyInfo.json` file:

1. Make a folder (for example `RainWorldWallpaper`) with a `LivelyInfo.json` like this, using your own Rain World folder (with doubled backslashes):

   ```json
   {
     "AppVersion": "1.0.0.0",
     "Title": "Rain World Wallpaper Mode",
     "Desc": "Rain World touring its own regions",
     "Type": 0,
     "FileName": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Rain World\\RainWorld.exe",
     "Arguments": "--wallpaper-host -screen-fullscreen 0",
     "IsAbsolutePath": true
   }
   ```

2. Zip the folder so `LivelyInfo.json` sits at the top of the zip, and drag the zip into Lively's library.

The mod also recognises Lively as the program that starts it; the arguments make sure of it and keep the game windowed from the first frame.

The Microsoft Store edition of Lively does not support application wallpapers.

## Launch Arguments

```text
--wallpaper-host -screen-fullscreen 0 --wallpaper-region SU --wallpaper-campaign White
```

- `--wallpaper-host` (or `--wallpaper-engine`): run as a hosted desktop wallpaper, as described above. Starts wallpaper mode. Not needed when Wallpaper Engine or Lively starts the game.
- `--wallpaper`: only start wallpaper mode without a click, for example to run it in a normal window on a second monitor.
- `-screen-fullscreen 0`: Unity's own option. Starts the game windowed even if it was fullscreen last time, before the mod has loaded.
- `--wallpaper-region`: Rain World region codes such as `SU`, `HI`, `CC`, `GW`, `SH`, `DS`, `SL`, `SI`, `LF`, `UW`, `SS`, `SB`, `LM`, `RM`, `DM`, `LC`, `MS`, `VS`, `CL`, `OE`
- `--wallpaper-campaign`: `White`, `Yellow`, `Red`, `Gourmand`, `Artificer`, `Rivulet`, `Spearmaster`, `Saint`, `Watcher`
- `--wallpaper-menu-style`: `panel` or `minimal`, the look of the in-game settings menu

Behavior:

- Region, campaign and menu style apply to that process only and do not change your Remix settings.
- Everything else comes from the mod's Remix settings, which a wallpaper launch reads like any other. The wallpaper plays the game's music and sounds; for a silent one, turn off **Game sound** in Remix (Display card).
- The campaign override covers the whole session (region tour, HUD names, Watcher world states) until you pick another campaign in the settings menu or the tour advances to the next campaign.
- Auto-launch is one-shot per process, so returning to the main menu does not immediately restart wallpaper mode.

## Limits

- A wallpaper gets mouse clicks (Wallpaper Engine passes them on) but no keyboard input, so the hotkeys do not work. **Double-click the wallpaper** to open the settings menu and use it with the mouse; its CLOSE button puts the wallpaper back. Changes made there last until the wallpaper restarts; for lasting ones, use Remix.
- The first start after a mod update takes longer, because Rain World re-applies its mods.
- This is still Rain World running, with the game's normal CPU and GPU cost.
- Stability depends on the installed mod set. For wallpaper use, keep the stack small.
