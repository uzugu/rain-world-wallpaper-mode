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

            Vector2 buttonPosition = new Vector2(200f, 180f);
            Vector2 buttonSize = new Vector2(200f, 40f);

            wallpaperButton = new WallpaperMenuButton(
                menu,
                menu.pages[0],
                menu.Translate("WALLPAPER MODE"),
                buttonPosition,
                buttonSize,
                () => OnWallpaperButtonClicked(menu)
            );

            menu.pages[0].subObjects.Add(wallpaperButton);

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

        private class WallpaperMenuButton : Menu.SimpleButton
        {
            private readonly Action onClick;

            public WallpaperMenuButton(MainMenu menu, Menu.Page owner, string label, Vector2 position, Vector2 size, Action onClick)
                : base(menu, owner, label, "WALLPAPER_MODE", position, size)
            {
                this.onClick = onClick;
            }

            public override void Clicked()
            {
                base.Clicked();
                onClick?.Invoke();
            }
        }
    }
}
