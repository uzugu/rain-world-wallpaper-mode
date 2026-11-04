using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private static readonly IntPtr HwndBottom = new IntPtr(1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint ENABLE_MOUSE_INPUT = 0x0010;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        // A double-click opens the settings menu, since a hosted wallpaper gets mouse clicks but no keys.
        private const float DoubleClickSeconds = 0.5f;
        private const float DoubleClickDistance = 24f;
        private const float ConsoleCheckSeconds = 2f;

        private static float lastClickTime = float.NegativeInfinity;
        private static Vector2 lastClickPosition;
        private static float nextConsoleCheck;

        // Programs that run application wallpapers: Wallpaper Engine (wallpaper32/64.exe, and ui32.exe for its editor
        // preview) and Lively Wallpaper. When one of them starts the game, host mode turns on without launch arguments.
        private static readonly string[] HostProcessNames =
        {
            "wallpaper64.exe", "wallpaper32.exe", "ui32.exe", "lively.exe", "livelywpf.exe"
        };

        private const string RainWorldSteamAppId = "312520";

        internal static bool Active { get; private set; }

        /// <summary>
        /// Programs that are Steam apps themselves, such as Wallpaper Engine (app 431960), pass their own SteamAppId and
        /// SteamGameId on to the programs they start. Steam reads SteamAppId before steam_appid.txt, so Rain World would
        /// sign in to Steam as that app; its start-up Workshop check then waits for the other app's Workshop items to
        /// download and never finishes. This puts Rain World's app ID back. It has to run before the game starts Steam,
        /// which the plugin's OnEnable does: BepInEx loads plugins before the first MonoBehaviour wakes up, and the game's
        /// SteamManager is only created later, by the ProcessManager. Steam cannot be started twice in one process, so
        /// there is no later chance.
        /// </summary>
        internal static void EnsureRainWorldSteamAppId()
        {
            try
            {
                string appId = Environment.GetEnvironmentVariable("SteamAppId");
                string gameId = Environment.GetEnvironmentVariable("SteamGameId");
                bool foreignAppId = !string.IsNullOrEmpty(appId) && appId != RainWorldSteamAppId;
                bool foreignGameId = !string.IsNullOrEmpty(gameId) && gameId != RainWorldSteamAppId;
                if (!foreignAppId && !foreignGameId)
                {
                    return;
                }

                // The process environment itself, which Steam's own DLL reads when it loads.
                SetEnvironmentVariable("SteamAppId", RainWorldSteamAppId);
                SetEnvironmentVariable("SteamGameId", RainWorldSteamAppId);
                WallpaperMod.Log?.LogInfo(
                    $"The program that started the game passed Steam app ID {(foreignAppId ? appId : gameId)}; set it back to " +
                    $"Rain World's ({Environment.GetEnvironmentVariable("SteamAppId")}) before Steam starts");
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Could not set Rain World's Steam app ID: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the executable name of the process that started the game when it is a known wallpaper host, and null
        /// otherwise. Every launcher is logged, so a host that is not on the list shows up in the log.
        /// </summary>
        internal static string DetectLaunchingHost()
        {
            try
            {
                string launcher = GetParentProcessName();
                WallpaperMod.Log?.LogInfo($"Game started by {launcher ?? "an unknown process"}");
                if (launcher != null && HostProcessNames.Any(name => string.Equals(name, launcher, StringComparison.OrdinalIgnoreCase)))
                {
                    return launcher;
                }
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Could not tell which program started the game: {ex.Message}");
            }

            return null;
        }

        // The parent of this process, from a snapshot of all processes. The parent may have exited since; then its
        // process ID is not in the snapshot (or belongs to another program), which at worst means no host is detected.
        private static string GetParentProcessName()
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == InvalidHandleValue || snapshot == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var processes = new Dictionary<uint, (uint parent, string exe)>();
                var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
                if (!Process32First(snapshot, ref entry))
                {
                    return null;
                }

                do
                {
                    processes[entry.th32ProcessID] = (entry.th32ParentProcessID, entry.szExeFile);
                }
                while (Process32Next(snapshot, ref entry));

                uint self = (uint)Process.GetCurrentProcess().Id;
                if (processes.TryGetValue(self, out var current) && processes.TryGetValue(current.parent, out var parent))
                {
                    return parent.exe;
                }

                return null;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }

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
            bool quickEditOff = consoleHidden && DisableConsoleQuickEdit();
            On.Options.OnLoadFinished += Options_OnLoadFinished;

            WallpaperMod.Log?.LogInfo(
                $"Wallpaper host mode: runs without focus (was {ranInBackground}), " +
                $"{(consoleHidden ? $"BepInEx console hidden{(quickEditOff ? " and its QuickEdit turned off" : string.Empty)}" : "no console window")}, " +
                "the game stays windowed");
        }

        /// <summary>
        /// Keeps the BepInEx console out of the way while a host holds the game. Wallpaper Engine moves every window of
        /// the program into its own wallpaper window, the console included, and passes mouse clicks on to them; with the
        /// console on top it took the clicks meant for the game. Called every frame, it only acts every few seconds.
        /// </summary>
        internal static void MaintainConsole()
        {
            if (!Active || Time.unscaledTime < nextConsoleCheck)
            {
                return;
            }

            nextConsoleCheck = Time.unscaledTime + ConsoleCheckSeconds;
            try
            {
                IntPtr console = GetConsoleWindow();
                if (console == IntPtr.Zero)
                {
                    return;
                }

                if (IsWindowVisible(console))
                {
                    ShowWindow(console, SW_HIDE);
                }

                IntPtr parent = GetAncestor(console, GA_PARENT);
                if (parent != IntPtr.Zero && parent != GetDesktopWindow())
                {
                    // Bottom of the host window's children, under the game window, without moving or resizing it.
                    SetWindowPos(console, HwndBottom, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Wallpaper host mode: could not tidy the console window: {ex.Message}");
                nextConsoleCheck = float.PositiveInfinity;
            }
        }

        /// <summary>
        /// True on the frame that completes a double-click with the left mouse button. Called every frame rather than
        /// per game tick, because a host passes clicks on as window messages and a press and its release can land in
        /// the same frame, between two ticks. While tracking is off (no wallpaper session, or the settings menu is open)
        /// earlier clicks are forgotten, so the click that closes the menu cannot count as half of a double-click.
        /// </summary>
        internal static bool ConsumeDoubleClick(bool track)
        {
            if (!Active || !track)
            {
                lastClickTime = float.NegativeInfinity;
                return false;
            }

            if (!Input.GetMouseButtonDown(0))
            {
                return false;
            }

            float now = Time.unscaledTime;
            Vector2 position = Input.mousePosition;
            bool isDoubleClick = now - lastClickTime <= DoubleClickSeconds &&
                (position - lastClickPosition).sqrMagnitude <= DoubleClickDistance * DoubleClickDistance;

            // A completed double-click starts over, so a third click does not open the menu again.
            lastClickTime = isDoubleClick ? float.NegativeInfinity : now;
            lastClickPosition = position;
            return isDoubleClick;
        }

        // QuickEdit lets a click start a text selection in the console, and while a selection is open every write to the
        // console blocks the thread that writes. BepInEx logs to the console from the game's main thread, so one click
        // that a host passed on to the hidden console froze the whole game. Turning QuickEdit (and console mouse input)
        // off in the console's input mode stops clicks from selecting anything.
        private static bool DisableConsoleQuickEdit()
        {
            IntPtr input = InvalidHandleValue;
            try
            {
                input = CreateFile("CONIN$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (input == InvalidHandleValue || !GetConsoleMode(input, out uint mode))
                {
                    return false;
                }

                uint quietMode = (mode | ENABLE_EXTENDED_FLAGS) & ~(ENABLE_QUICK_EDIT_MODE | ENABLE_MOUSE_INPUT);
                return quietMode == mode || SetConsoleMode(input, quietMode);
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Wallpaper host mode: could not turn off the console's QuickEdit: {ex.Message}");
                return false;
            }
            finally
            {
                if (input != InvalidHandleValue && input != IntPtr.Zero)
                {
                    CloseHandle(input);
                }
            }
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);

        [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", EntryPoint = "SetEnvironmentVariableW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetEnvironmentVariable(string name, string value);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(string fileName, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

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
