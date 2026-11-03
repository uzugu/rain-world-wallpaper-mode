using System;

namespace RainWorldWallpaperMod
{
    internal sealed class WallpaperUiController
    {
        private WallpaperHUD hud;
        private global::Menu.Menu settingsMenu;
        private bool hasInitializedHud;

        public WallpaperHUD Hud => hud;

        public bool IsSettingsActive => settingsMenu != null;

        public void EnsureInitialized(RoomCamera camera, WallpaperController controller)
        {
            EnsureHudInitialized(camera, controller);
        }

        public void EnsureHudInitialized(RoomCamera camera, WallpaperController controller)
        {
            if (camera == null || hasInitializedHud)
            {
                return;
            }

            var createdHud = new WallpaperHUD(camera, controller);
            if (!createdHud.IsReady)
            {
                return;
            }

            hud = createdHud;
            hasInitializedHud = true;
            hud.RegisterUserActivity();
            WallpaperMod.Log?.LogInfo("WallpaperController: HUD initialized successfully");

            OpenSettingsIfRequestedAtLaunch(controller);
        }

        /// <summary>
        /// Opens the settings menu if the process was started with --wallpaper-open-settings, a testing aid. The request
        /// is used up by the first session that gets here, so later sessions (region reloads) do not open it again.
        /// </summary>
        private void OpenSettingsIfRequestedAtLaunch(WallpaperController controller)
        {
            if (WallpaperMod.Instance == null || !WallpaperMod.Instance.TryConsumeOpenSettingsRequest())
            {
                return;
            }

            WallpaperMod.Log?.LogInfo("WallpaperController: Opening the settings menu (--wallpaper-open-settings)");
            OpenSettingsMenu(controller);
        }

        public void ToggleSettingsMenu(WallpaperController controller)
        {
            if (settingsMenu != null)
            {
                CloseSettingsMenu();
            }
            else
            {
                OpenSettingsMenu(controller);
            }
        }

        /// <summary>
        /// Opens the settings menu as a side process of the game, which keeps running underneath it.
        /// </summary>
        private void OpenSettingsMenu(WallpaperController controller)
        {
            ProcessManager manager = controller?.Game?.manager;

            // A pending process switch freezes menu input and closes the menu again, so do not open it then.
            if (settingsMenu != null || manager == null || manager.upcomingProcess != null)
            {
                return;
            }

            WallpaperModOptions.MenuStyle style = WallpaperMod.CurrentMenuStyle;
            global::Menu.Menu menu;
            try
            {
                menu = CreateSettingsMenu(manager, controller, style);
                manager.sideProcesses.Add(menu);
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"WallpaperController: Failed to open the settings menu: {ex}");
                return;
            }

            settingsMenu = menu;
            menu.PlaySound(SoundID.MENU_Switch_Page_In);
            hud?.SetSettingsMenuOpen(true);
            WallpaperMod.Log?.LogInfo($"WallpaperController: Opened the settings menu ({style} style)");
        }

        // The style comes from the Remix "Menu style" setting, or from --wallpaper-menu-style when it was given.
        private global::Menu.Menu CreateSettingsMenu(ProcessManager manager, WallpaperController controller, WallpaperModOptions.MenuStyle style)
        {
            Action closeRequested = () => CloseSettingsMenu();
            switch (style)
            {
                case WallpaperModOptions.MenuStyle.Minimal:
                    return new WallpaperMinimalMenu(manager, controller, closeRequested);
                default:
                    return new WallpaperSettingsMenu(manager, controller, closeRequested);
            }
        }

        /// <summary>
        /// Stops the settings menu. Side processes outlive the game they were started from, so every path that ends or
        /// reloads the wallpaper session has to come through here.
        /// </summary>
        public void CloseSettingsMenu(bool playSound = true)
        {
            global::Menu.Menu menu = settingsMenu;
            if (menu == null)
            {
                return;
            }

            settingsMenu = null;
            hud?.SetSettingsMenuOpen(false);

            try
            {
                if (playSound)
                {
                    menu.PlaySound(SoundID.MENU_Switch_Page_Out);
                }

                menu.manager.StopSideProcess(menu);
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"WallpaperController: Failed to close the settings menu: {ex}");
            }
        }

        public void RegisterUserActivity()
        {
            hud?.RegisterUserActivity();
        }

        public void ResetForReload()
        {
            CloseSettingsMenu(playSound: false);

            hud?.Destroy();
            hud = null;
            hasInitializedHud = false;
        }

        public void Shutdown()
        {
            ResetForReload();
        }
    }
}
