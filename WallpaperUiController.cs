using UnityEngine;

namespace RainWorldWallpaperMod
{
    internal sealed class WallpaperUiController
    {
        private WallpaperHUD hud;
        private WallpaperSettingsOverlay settingsOverlay;
        private bool hasInitializedHud;
        private bool hasInitializedSettingsOverlay;
        private bool settingsMenuVisible;

        public WallpaperHUD Hud => hud;

        public WallpaperSettingsOverlay SettingsOverlay => settingsOverlay;

        public bool IsSettingsActive => settingsMenuVisible && settingsOverlay != null && settingsOverlay.IsVisible;

        public void EnsureInitialized(RoomCamera camera, WallpaperController controller)
        {
            EnsureHudInitialized(camera, controller);
            EnsureSettingsOverlayInitialized(camera, controller);
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
        }

        public void EnsureSettingsOverlayInitialized(RoomCamera camera, WallpaperController controller)
        {
            if (camera == null || hasInitializedSettingsOverlay)
            {
                return;
            }

            settingsOverlay = new WallpaperSettingsOverlay(camera, controller);
            hasInitializedSettingsOverlay = true;
            WallpaperMod.Log?.LogInfo("WallpaperController: Settings overlay initialized successfully");
        }

        public void ToggleSettingsMenu(RoomCamera camera, WallpaperController controller)
        {
            if (!hasInitializedSettingsOverlay)
            {
                EnsureSettingsOverlayInitialized(camera, controller);
            }

            if (settingsOverlay == null)
            {
                return;
            }

            settingsMenuVisible = !settingsMenuVisible;
            settingsOverlay.SetVisible(settingsMenuVisible);
            settingsOverlay.Refresh();

            if (settingsMenuVisible)
            {
                hud?.RegisterUserActivity();
            }
        }

        public void RegisterUserActivity()
        {
            hud?.RegisterUserActivity();
        }

        public void RefreshOverlay()
        {
            settingsOverlay?.Refresh();
        }

        public void ResetForReload()
        {
            hud?.Destroy();
            hud = null;
            hasInitializedHud = false;

            settingsOverlay?.Destroy();
            settingsOverlay = null;
            hasInitializedSettingsOverlay = false;
            settingsMenuVisible = false;
        }

        public void Shutdown()
        {
            ResetForReload();
        }
    }
}
