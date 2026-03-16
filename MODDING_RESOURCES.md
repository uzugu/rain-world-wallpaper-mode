# Rain World Modding Resources

This document collects the most useful local and external references for improving `RainWorldWallpaperMod`.

## What To Use First

For this project, the highest-value resources are:

1. local `RainMeadowReference` for proven Rain World hook and menu patterns
2. BepInEx docs for plugin structure and config/logging conventions
3. Rain World Modding Wiki for Downpour-era mod layout, `modinfo.json`, and hook mechanics

Rain Meadow is useful as a reference implementation, but it is much larger and more complex than this mod. Copy patterns selectively, not wholesale.

## Local References

### Rain Meadow Reference Repo

Path:

`C:\Users\uzuik\Documents\VRmakes\Projects\RainMeadowReference`

High-signal files:

- `RainMeadow.cs`
  - central plugin bootstrap
  - grouped hook registration
  - `OnModsInit` guard and Remix registration
- `Game/MeadowRemixOptions.cs`
  - large but useful `OptionInterface` examples
  - keybinds, tabs, labels, and mixed controls
- `Menu/RainMeadow.MenuHooks.cs`
  - examples of menu hooks, process switching, manual hooks, and IL hooks
- `Readme.md`
  - high-level project framing

Why it matters for this mod:

- it shows real-world Downpour/Remix-era hook organization
- it demonstrates menu integration and process-manager hooks at scale
- it is a better source for UI/menu patterns than older pre-Remix mods

What to borrow:

- grouped hook registration by subsystem
- explicit init guard in `OnModsInit`
- keeping menu hooks separate from game hooks
- Remix option patterns when the settings surface grows

What not to borrow directly:

- multiplayer infrastructure
- custom networking/resource/session architecture
- broad IL patching unless a simpler `On.` hook is impossible

## External Resources

### 1. BepInEx Docs

Use these for plugin fundamentals and cleaner mod architecture:

- Guide index: https://docs.bepinex.dev/articles/
- Basic plugin tutorial: https://docs.bepinex.dev/master/articles/dev_guide/plugin_tutorial/
- Project setup: https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/2_plugin_start.html

Why this matters:

- confirms the expected `BaseUnityPlugin` structure
- gives cleaner guidance for logging and config than ad-hoc patterns
- useful if we refactor the plugin bootstrap or split code into more subsystems

### 2. Rain World Modding Wiki

Use this for Rain World specific conventions:

- Main page: https://rainworldmodding.miraheze.org/wiki/Main_Page
- Hooking: https://rainworldmodding.miraheze.org/wiki/Hooking
- BepInPlugins: https://rainworldmodding.miraheze.org/wiki/BepInPlugins
- Downpour mod directories / `modinfo.json`: https://rainworldmodding.miraheze.org/wiki/Downpour_Reference/Mod_Directories

Why this matters:

- documents the `On.` hook model used by this mod
- clarifies packaging and Remix-era mod folder layout
- useful when we need Rain World specific behavior rather than generic Unity modding advice

### 3. Official Rain World Wiki Page For Remix

- Remix overview: https://rainworld.miraheze.org/wiki/Remix

Why this matters:

- documents the in-game mod surface players actually use
- useful for release flow, Workshop expectations, and UX assumptions

### 4. SlugBase Docs

- Docs: https://slimecubed.github.io/slugbase/
- Template: https://github.com/SlimeCubed/SlugTemplate

Why this matters:

- only relevant if this project expands into custom slugcats or campaign-specific content
- less critical for the wallpaper core than BepInEx + Rain World Modding Wiki

### 5. Rain Meadow Upstream

- GitHub repo: https://github.com/henpemaz/Rain-Meadow

Why this matters:

- useful to compare your local reference copy against upstream
- good source for advanced hook and menu patterns when local code is not enough

## Current Mod: Best Immediate Improvement Areas

Given the current codebase, the best next technical improvements are:

### 1. Split `WallpaperController` into smaller subsystems

Current problem:

- it owns input, transitions, rain logic, room history, HUD wiring, overlay wiring, and reload state

Recommended split:

- `WallpaperSessionState`
- `WallpaperInputController`
- `WallpaperTransitionController`
- `WallpaperRegionFlow`

Why:

- easier reasoning
- fewer regressions when changing one behavior
- better testability

### 2. Tighten launch/reload flow

Current risk:

- process switching and region reload behavior is spread between `WallpaperMod`, `MenuIntegration`, `WallpaperController`, and `RegionManager`

Target:

- one explicit launch/reload state machine
- one place responsible for region reload preparation

### 3. Reduce direct game-state mutation in multiple places

Current risk:

- spectator cleanup, room changes, chaos behavior, and echo behavior all touch live game state

Target:

- centralize the rules for:
  - player removal
  - camera detachment
  - room realization/transition completion
  - region-change cleanup

### 4. Improve observability

Current opportunity:

- the mod has some logging, but debugging live Rain World behavior is still expensive

Target:

- add a small debug mode with concise, high-signal logs
- avoid always-on noisy logs
- optionally add an overlay debug line for transition state, room name, and rain-cycle state

## How To Use These Resources Practically

### When changing hooks

- start with the Rain World Modding Hooking page
- compare with `RainMeadow.cs` and `Menu/RainMeadow.MenuHooks.cs`
- prefer `On.` hooks first
- use IL/manual hooks only when the normal hook surface is insufficient

### When changing Remix UI

- start with local `WallpaperModOptions.cs`
- compare with `Game/MeadowRemixOptions.cs`
- keep this mod's UI much simpler than Rain Meadow's

### When changing menu behavior

- inspect `MenuIntegration.cs`
- compare with `Menu/RainMeadow.MenuHooks.cs`
- prefer minimal hooks over broad menu interception

### When changing packaging or release metadata

- use the Downpour mod directories page
- verify against `assets/modinfo.json`
- confirm the packaged folder in `artifacts/bin/RainWorldWallpaperMod/<Configuration>_AnyCPU/mod/`

## Recommended Next Step

Before adding new features, do a small architecture pass:

1. document the current launch -> session -> room transition -> region reload flow
2. split `WallpaperController` along runtime responsibilities
3. only then add new wallpaper features or polish

That gives a better base than continuing to pile behavior into one controller class.
