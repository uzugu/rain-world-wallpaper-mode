using System.Collections.Generic;

namespace RainWorldWallpaperMod
{
    internal enum WallpaperInputButton
    {
        Pause,
        ToggleOverlay,
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

    internal sealed class WallpaperInputState
    {
        private readonly Dictionary<WallpaperInputButton, bool> buttonStates = new Dictionary<WallpaperInputButton, bool>();

        public bool WasPressed(WallpaperInputButton button, bool isDown)
        {
            bool wasDown = buttonStates.TryGetValue(button, out bool previousState) && previousState;
            buttonStates[button] = isDown;
            return isDown && !wasDown;
        }

        public void Reset()
        {
            buttonStates.Clear();
        }
    }
}
