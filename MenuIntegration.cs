using System;
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
        private static MainMenu pendingAutoLaunchMenu;
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            // Hook into MainMenu constructor and update
            On.Menu.MainMenu.ctor += MainMenu_ctor;
            On.Menu.MainMenu.Update += MainMenu_Update;

            initialized = true;
            WallpaperMod.Log?.LogInfo("MenuIntegration: Initialized");
        }

        private static void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
        {
            // Put the player's own slugcat selection back after a wallpaper session, before the menu reads it, and the
            // sound if Game sound kept the session silent.
            WallpaperMod.Instance?.RestoreSelectedSlugcat(manager);
            WallpaperSound.Restore();

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

            if (WallpaperMod.Instance?.HasPendingAutoLaunchRequest() == true)
            {
                pendingAutoLaunchMenu = self;
            }
        }

        private static void MainMenu_Update(On.Menu.MainMenu.orig_Update orig, MainMenu self)
        {
            orig(self);

            if (!ReferenceEquals(self, pendingAutoLaunchMenu))
            {
                return;
            }

            pendingAutoLaunchMenu = null;

            try
            {
                WallpaperMod.Instance?.TryAutoLaunchFromMainMenu(self.manager);
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"MenuIntegration: Failed to auto-launch wallpaper mode - {ex}");
            }
        }

        private static void AddWallpaperModeButton(MainMenu menu)
        {
            // Vanilla sizes its main menu buttons with a private helper, so copy the size of the EXIT button.
            Vector2 buttonSize = new Vector2(110f, 30f);
            foreach (var subObject in menu.pages[0].subObjects)
            {
                if (subObject is Menu.SimpleButton button && button.signalText == "EXIT")
                {
                    buttonSize = button.size;
                    break;
                }
            }

            var wallpaperButton = new Menu.SimpleButton(
                menu,
                menu.pages[0],
                menu.Translate("WALLPAPER MODE"),
                "WALLPAPER_MODE",
                Vector2.zero,
                buttonSize
            );

            // AddMainMenuButton lays out the button column, wires controller navigation and calls the callback
            // when the button is pressed. Index 1 from the bottom places it directly above EXIT.
            menu.AddMainMenuButton(wallpaperButton, () => OnWallpaperButtonClicked(menu), 1);

            WallpaperMod.Log?.LogInfo("MenuIntegration: Wallpaper button added above EXIT");
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
            On.Menu.MainMenu.Update -= MainMenu_Update;
            pendingAutoLaunchMenu = null;
            initialized = false;
        }

    }
}
