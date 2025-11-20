# Rain World Wallpaper Mod - Status & Modding Report

## Project Status
The project is in **V2.0** state with a functional BepInEx plugin.

### Implemented Features
- **Wallpaper Mode**: Spectator camera that transitions between rooms.
- **Region Cycling**: Automatic (timer-based) and manual (`G`/`B` keys) region changes.
- **HUD**: Displays location info, fades on idle, wakes on input.
- **Settings Overlay**: In-game (`F1`/`Tab`) controls for duration and HUD.
- **Menu Integration**: "Wallpaper Mode" button on the main menu.

### Active Focus Areas
- **Remix Interface**: Need to implement `OptionInterface` for user-friendly configuration.
- **Persistence**: Settings currently reset on launch; need to save them.
- **Controller Support**: HUD wake detection needs improvement for gamepads.

## How to Mod Rain World
Based on `RainMeadowReference` and standard practices:

1.  **Plugin Framework**:
    - Use **BepInEx**.
    - Main class inherits `BaseUnityPlugin`.
    - Attribute: `[BepInPlugin("id", "Name", "Version")]`.

2.  **Hooking (MonoMod)**:
    - Intercept game methods using `On.Namespace.Class.Method += MyHook;`.
    - **Key Hook**: `On.RainWorld.OnModsInit` for initialization and Remix registration.
    - **Update Loop**: `On.RainWorldGame.Update` for per-frame logic.

3.  **User Interface**:
    - Rain World uses **Futile** (a 2D framework) for UI.
    - Add elements to `Futile.stage` for overlays that persist or bypass standard HUDs.

4.  **Configuration (Remix)**:
    - Create a class inheriting from `OptionInterface`.
    - Register it in `OnModsInit` using `MachineConnector.SetRegisteredOI()`.
    - Use `OpTab`, `OpSlider`, `OpCheckBox`, etc., to build the menu.

## Next Steps
Recommended immediate tasks:
1.  Implement the **Remix OptionInterface** to replace the temporary `F1` overlay and enable setting persistence.
2.  Verify controller inputs for the HUD wake feature.
