using System.Collections.Generic;

namespace RainWorldWallpaperMod
{
    internal enum WallpaperInputButton
    {
        Pause,
        ToggleOverlay,
        Cancel,
        NextRoom,
        RegionForward,
        RegionBack,
        HudToggle,
        Up,
        Down,
        Left,
        Right,
        Enter,
        Lock
    }

    public enum WallpaperInputDevice
    {
        KeyboardMouse,
        Controller
    }

    internal sealed class WallpaperInputState
    {
        private readonly Dictionary<WallpaperInputButton, bool> buttonStates = new Dictionary<WallpaperInputButton, bool>();

        public WallpaperInputDevice ActiveDevice { get; private set; } = WallpaperInputDevice.KeyboardMouse;

        public bool WasPressed(WallpaperInputButton button, bool isDown)
        {
            bool wasDown = buttonStates.TryGetValue(button, out bool previousState) && previousState;
            buttonStates[button] = isDown;
            return isDown && !wasDown;
        }

        public bool SetActiveDevice(WallpaperInputDevice device)
        {
            if (ActiveDevice == device)
            {
                return false;
            }

            ActiveDevice = device;
            return true;
        }

        public void Reset()
        {
            buttonStates.Clear();
        }
    }
}
