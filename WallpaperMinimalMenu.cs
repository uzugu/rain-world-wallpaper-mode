using System;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Placeholder for the minimalistic settings menu prototype (--wallpaper-menu-style minimal).
    /// </summary>
    internal sealed class WallpaperMinimalMenu : global::Menu.Menu
    {
        private static readonly ProcessManager.ProcessID MinimalProcessID = new ProcessManager.ProcessID("WallpaperSettingsMinimal", register: true);

        public WallpaperMinimalMenu(ProcessManager manager, WallpaperController controller, Action closeRequested)
            : base(manager, MinimalProcessID)
        {
            // The base constructor already put the menu on the stage, so take it off before failing.
            ShutDownProcess();
            throw new NotImplementedException("The minimalistic settings menu is not built yet.");
        }
    }
}
