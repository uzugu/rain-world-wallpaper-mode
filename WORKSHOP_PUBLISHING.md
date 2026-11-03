# Rain World Wallpaper Mode - Workshop Publishing

This checklist is for publishing or updating the mod on Steam Workshop.

## Before The First Upload

### 1. Freeze The Release Build

- build `Release`
- confirm the packaged mod folder is `artifacts/bin/RainWorldWallpaperMod/Release_AnyCPU/mod/`
- verify the package contains:
  - `modinfo.json`
  - `plugins/RainWorldWallpaperMod.dll`

### 2. Review Metadata

Check `assets/modinfo.json` before publishing:

- `name`
- `version`
- `description`
- `tags`
- target game version

Keep identifiers consistent across:

- BepInEx plugin ID
- Remix registration ID
- packaged folder name
- Workshop item branding

### 3. Prepare Store Assets

Have these ready before the first public upload:

- a square or landscape preview image for the Workshop page
- a short description for the upload dialog
- a full Workshop description with features, controls, requirements, and known limitations
- at least one or two gameplay screenshots if you want the page to look complete on day one

### 4. Test The Installed Package

Install the packaged `mod` folder into Rain World locally and verify:

- the mod appears in Remix
- the `WALLPAPER MODE` button appears on the main menu
- wallpaper mode launches
- HUD and settings menu work
- at least one region reload succeeds

## First Upload Flow

1. Launch Rain World through Steam.
2. Open `Remix`.
3. Select the mod.
4. Use the upload button in the mod panel to create the Workshop item.
5. Fill in the title, summary, and visibility.
6. Publish the first version as hidden or friends-only if you want a final test pass before public release.
7. Open the resulting Workshop page in Steam and complete any remaining page setup.
8. Accept the Steam Workshop legal agreement if Steam requires it for the first upload.

## Recommended First-Publish Sequence

1. Upload hidden.
2. Review the Workshop page.
3. Subscribe to your own item and confirm the Workshop-installed version works (move the local copy out of `mods/` first, see "Local copy wins" below).
4. Only after that, set the item to public.

## Updating An Existing Workshop Item

For each update:

1. bump the version everywhere it is declared:
   - `assets/modinfo.json`
   - `RainWorldWallpaperMod.csproj`
   - the `BepInPlugin` attribute in `WallpaperMod.cs`
2. rebuild `Release`
3. verify the packaged output at `artifacts/bin/RainWorldWallpaperMod/Release_AnyCPU/mod/`
4. copy the packaged files over the local install at `RainWorld_Data/StreamingAssets/mods/uzugu_wallpapermod/`, and keep anything else out of that folder
5. disable and re-enable the mod in Remix, then return to the main menu or restart Rain World
6. confirm Remix shows the new version and verify the changed behavior in-game
7. upload from the mod's panel in Remix
8. paste the full page text from `STEAM_DESCRIPTION.md` back into the Workshop description, because the upload replaced it
9. add a concise change note on the Workshop item, because the upload submits an empty one

Do not change the mod `id` between updates unless you intend to create a new Workshop item.

**Important Remix cache note**: Rain World/Remix can show the updated version number while still using stale plugin or options state from the previous activation. If a fix appears not to apply, toggle the mod off and on in Remix before testing. `enabledMods.txt` only stores the mod id, so it cannot show which copy is loaded; see "Local copy wins" below.

## What A Remix Upload Actually Does

Checked in November 2026 against the installed game's code (`SteamWorkshopUploader`, `RainWorldSteamManager.UploadWorkshopMod`, `ModManager.RefreshModsLists`):

- **Source folder**: Workshop-subscribed copies cannot be uploaded, so the upload always comes from the local `StreamingAssets/mods/uzugu_wallpapermod/` folder. Everything in that folder is uploaded.
- **Which item is updated**: the uploader searches the Workshop for an item tagged with this mod's `id` (`uzugu_wallpapermod`) that you own, and updates it. No local file stores the Workshop ID. If no item matches, a new one is created as Unlisted.
- **Overwritten on every upload**: the title (`name`), the description (`description`), the tags, the id/version/game-version/authors/requirements key tags, the preview image (`thumbnail.png`, which must be under 1 MB and 16:9), and the files.
- **Left alone on updates**: visibility. The change note is always submitted empty.
- **Local copy wins**: when the local folder and a subscribed Workshop copy share the same `id`, Rain World loads the local copy and ignores the Workshop one. In-game testing therefore runs the local folder; testing the Workshop download itself requires moving the local folder out of `mods/` first.

## Suggested Workshop Page Structure

Use a simple page layout:

- one-paragraph summary
- feature list
- controls
- requirements
- known caveats
- compatibility notes for base game and Downpour

## Project-Specific Notes

- The current implementation launches from a main-menu `WALLPAPER MODE` button, not an in-game toggle.
- Region changes are rain-cycle-driven, so mention that in the Workshop description.
- Chaos mode and `Spawn All` should be described as optional and potentially unstable.
- Echo support is present and should be mentioned because it is a visible differentiator.

## Final Pre-Public Checklist

- release build completed
- metadata reviewed
- Workshop preview image ready
- local install tested
- hidden upload tested
- public description proofread
- version number matches the package you uploaded
