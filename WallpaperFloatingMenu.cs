using System;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Placeholder for the floating-elements settings menu prototype (--wallpaper-menu-style floating).
    /// </summary>
    internal sealed class WallpaperFloatingMenu : global::Menu.Menu
    {
        private static readonly ProcessManager.ProcessID FloatingProcessID = new ProcessManager.ProcessID("WallpaperSettingsFloating", register: true);

        public WallpaperFloatingMenu(ProcessManager manager, WallpaperController controller, Action closeRequested)
            : base(manager, FloatingProcessID)
        {
            // The base constructor already put the menu on the stage, so take it off before failing.
            ShutDownProcess();
            throw new NotImplementedException("The floating-elements settings menu is not built yet.");
        }
    }
}
