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

            // Hook into MainMenu constructor
            On.Menu.MainMenu.ctor += MainMenu_ctor;

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

            Vector2 buttonSize = new Vector2(200f, 30f);

            wallpaperButton = new Menu.SimpleButton(
                menu,
                menu.pages[0],
                menu.Translate("WALLPAPER MODE"),
                "WALLPAPER_MODE",
                Vector2.zero,
                buttonSize
            );

            int insertIndex = -1;
            try
            {
                FieldInfo field = typeof(MainMenu).GetField("mainMenuButtons", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(menu) is List<Menu.SimpleButton> buttonList && buttonList.Count > 0)
                {
                    insertIndex = Mathf.Clamp(buttonList.Count - 1, 0, buttonList.Count);
                }
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"MenuIntegration: Unable to access mainMenuButtons via reflection - {ex.Message}");
            }

            menu.AddMainMenuButton(wallpaperButton, () => OnWallpaperButtonClicked(menu), insertIndex);

            WallpaperMod.Log?.LogInfo("MenuIntegration: Button added successfully");
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
            initialized = false;
        }

    }
}
