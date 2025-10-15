using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Menu;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Handles integration with Rain World's menu system
    /// Adds "Wallpaper Mode" button to the main menu
    /// </summary>
    public static class MenuIntegration
    {
        private static Menu.SimpleButton wallpaperButton;
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            // Hook into MainMenu constructor and signal handler
            On.Menu.MainMenu.ctor += MainMenu_ctor;
            On.Menu.MainMenu.Singal += MainMenu_Singal;

            initialized = true;
            WallpaperMod.Log?.LogInfo("MenuIntegration: Initialized");
        }

        private static void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
        {
            // Call original constructor
            orig(self, manager, showRegionSpecificBkg);

            try
            {
                AddWallpaperModeButton(self);
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"MenuIntegration: Failed to add button - {ex}");
            }
        }

        private static void AddWallpaperModeButton(MainMenu menu)
        {
            WallpaperMod.Log?.LogInfo("MenuIntegration: Adding Wallpaper Mode button");

            // Get the last button (EXIT) position to align below it
            Vector2 buttonPos = new Vector2(683f, 50f); // Default: centered horizontally, below menu
            Vector2 buttonSize = new Vector2(200f, 30f); // Match standard button size

            try
            {
                FieldInfo field = typeof(MainMenu).GetField("mainMenuButtons", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(menu) is List<Menu.SimpleButton> buttonList && buttonList.Count > 0)
                {
                    // Get the last button (EXIT)
                    Menu.SimpleButton lastButton = buttonList[buttonList.Count - 1];
                    // Position below EXIT button with extra spacing
                    buttonPos = new Vector2(lastButton.pos.x, lastButton.pos.y - lastButton.size.y - 40f);
                    buttonSize = lastButton.size; // Match the size
                }
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"MenuIntegration: Unable to access mainMenuButtons - {ex.Message}");
            }

            wallpaperButton = new Menu.SimpleButton(
                menu,
                menu.pages[0],
                menu.Translate("WALLPAPER MODE"),
                "WALLPAPER_MODE",
                buttonPos,
                buttonSize
            );

            // Add directly to page, not using AddMainMenuButton
            menu.pages[0].subObjects.Add(wallpaperButton);

            WallpaperMod.Log?.LogInfo($"MenuIntegration: Wallpaper button added at position {buttonPos}");
        }

        private static void MainMenu_Singal(On.Menu.MainMenu.orig_Singal orig, MainMenu self, Menu.MenuObject sender, string message)
        {
            if (message == "WALLPAPER_MODE")
            {
                OnWallpaperButtonClicked(self);
            }
            else
            {
                orig(self, sender, message);
            }
        }

        private static void OnWallpaperButtonClicked(MainMenu menu)
        {
            WallpaperMod.Log?.LogInfo("MenuIntegration: Wallpaper Mode button clicked!");

            try
            {
                // Play click sound
                menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);

                // Launch wallpaper mode through the mod
                WallpaperMod.Instance.BeginWallpaperMode(menu.manager);
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"MenuIntegration: Failed to launch wallpaper mode - {ex}");
            }
        }

        public static void Cleanup()
        {
            if (!initialized)
            {
                return;
            }

            On.Menu.MainMenu.ctor -= MainMenu_ctor;
            On.Menu.MainMenu.Singal -= MainMenu_Singal;
            initialized = false;
        }

    }
}
