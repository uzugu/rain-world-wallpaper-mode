using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Makes the game behave as an application wallpaper when a wallpaper host such as Wallpaper Engine or Lively
    /// launches RainWorld.exe with --wallpaper-host. A host takes the program's window, puts it behind the desktop
    /// icons and sizes it to the monitor. For that to work the game has to show a single window, stay windowed, keep
    /// running without focus, and leave its window alone once the host has taken it.
    /// </summary>
    internal static class WallpaperHostMode
    {
        private const string UnityWindowClass = "UnityWndClass";
        private const int SW_HIDE = 0;
        private const uint GA_PARENT = 1;

        internal static bool Active { get; private set; }

        /// <summary>
        /// Switches host mode on for this process. Called once from the plugin's OnEnable, after the command line is read.
        /// </summary>
        internal static void Activate()
        {
            if (Active)
            {
                return;
            }

            Active = true;

            // A wallpaper never has focus, so the game must not stop updating when it loses it.
            bool ranInBackground = Application.runInBackground;
            Application.runInBackground = true;

            bool consoleHidden = HideConsoleWindow();
            On.Options.OnLoadFinished += Options_OnLoadFinished;

            WallpaperMod.Log?.LogInfo(
                $"Wallpaper host mode: runs without focus (was {ranInBackground}), " +
                $"{(consoleHidden ? "BepInEx console hidden" : "no console window")}, the game stays windowed");
        }

        internal static void Deactivate()
        {
            if (!Active)
            {
                return;
            }

            On.Options.OnLoadFinished -= Options_OnLoadFinished;
            Active = false;
        }

        // The BepInEx console is a second top-level window of the process. A host embeds the process's main window and
        // Lively refuses programs with more than one window, so the console is hidden. It still shows for the first
        // seconds of a launch, before plugins load; turning it off in BepInEx.cfg avoids even that.
        private static bool HideConsoleWindow()
        {
            try
            {
                IntPtr console = GetConsoleWindow();
                if (console == IntPtr.Zero)
                {
                    return false;
                }

                ShowWindow(console, SW_HIDE);
                return true;
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Wallpaper host mode: could not hide the console window: {ex.Message}");
                return false;
            }
        }

        // Rain World applies its saved resolution and fullscreen choice here, once its options have loaded.
        //
        // When a host has already embedded the window by then, the window is left alone: Unity applies a resolution
        // change at the end of the frame and may restyle the window while doing it, which would pull it out of the
        // host's layout. Only the game's own render width is set up, the part of OnLoadFinished that does not touch
        // the window (the game draws into a fixed-size texture that is scaled to whatever size the window has).
        //
        // Otherwise the original runs with the game forced windowed, so the host later finds a plain window. The saved
        // setting is put back right after: OnLoadFinished never saves the options, so it is not written either way.
        private static void Options_OnLoadFinished(On.Options.orig_OnLoadFinished orig, Options self)
        {
            if (IsGameWindowEmbedded())
            {
                int width = (int)self.ScreenSize.x;
                if (Futile.screen.pixelWidth != width)
                {
                    Futile.instance.UpdateScreenWidth(width);
                }

                WallpaperMod.Log?.LogInfo($"Wallpaper host mode: options loaded after the host took the window; left its size alone (render width {width})");
                return;
            }

            bool savedWindowed = self.windowed;
            self.windowed = true;
            try
            {
                orig(self);
            }
            finally
            {
                self.windowed = savedWindowed;
            }

            WallpaperMod.Log?.LogInfo($"Wallpaper host mode: options loaded before a host took the window; kept it windowed at {Screen.width}x{Screen.height}");
        }

        private static bool IsGameWindowEmbedded()
        {
            try
            {
                IntPtr window = FindGameWindow();
                if (window == IntPtr.Zero)
                {
                    return false;
                }

                IntPtr parent = GetAncestor(window, GA_PARENT);
                return parent != IntPtr.Zero && parent != GetDesktopWindow();
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Wallpaper host mode: could not inspect the game window: {ex.Message}");
                return false;
            }
        }

        // The game's own window (class UnityWndClass) in this process: a top-level window until a host embeds it, a
        // child of the host's window afterwards. The BepInEx console belongs to the process too, so the class decides.
        private static IntPtr FindGameWindow()
        {
            uint processId = (uint)Process.GetCurrentProcess().Id;
            IntPtr found = IntPtr.Zero;

            EnumWindows((window, _) =>
            {
                if (IsGameWindow(window, processId))
                {
                    found = window;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            if (found != IntPtr.Zero)
            {
                return found;
            }

            EnumWindowsProc matchChild = (child, _) =>
            {
                if (IsGameWindow(child, processId))
                {
                    found = child;
                    return false;
                }

                return true;
            };

            EnumWindows((window, _) =>
            {
                EnumChildWindows(window, matchChild, IntPtr.Zero);
                return found == IntPtr.Zero;
            }, IntPtr.Zero);

            return found;
        }

        private static bool IsGameWindow(IntPtr window, uint processId)
        {
            GetWindowThreadProcessId(window, out uint owner);
            if (owner != processId)
            {
                return false;
            }

            var className = new StringBuilder(64);
            return GetClassName(window, className, className.Capacity) > 0 && className.ToString() == UnityWindowClass;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();
    }
}
