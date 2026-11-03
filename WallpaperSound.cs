namespace RainWorldWallpaperMod
{
    /// <summary>
    /// The Game sound setting. With it off, wallpaper mode plays silently: the master volume (AudioListener.volume, set
    /// through Futile's FSoundManager) goes to zero. The game never sets the master volume itself and never saves it, and
    /// it is put back when the main menu opens, so normal play keeps its sound and the game's audio settings stay as
    /// they are. FSoundManager.isMuted is not used, because it also pauses all audio.
    /// </summary>
    internal static class WallpaperSound
    {
        private static bool muted;
        private static float volumeBeforeMute = 1f;

        /// <summary>
        /// Applies the saved Game sound setting. Called when a wallpaper session starts, including after region reloads.
        /// </summary>
        internal static void ApplySetting()
        {
            SetSoundOn(WallpaperMod.Options == null || WallpaperMod.Options.GameSound.Value);
        }

        internal static void SetSoundOn(bool soundOn)
        {
            if (soundOn != muted)
            {
                return;
            }

            if (soundOn)
            {
                FSoundManager.volume = volumeBeforeMute;
                muted = false;
                WallpaperMod.Log?.LogInfo("Game sound on");
            }
            else
            {
                volumeBeforeMute = FSoundManager.volume;
                FSoundManager.volume = 0f;
                muted = true;
                WallpaperMod.Log?.LogInfo("Game sound off while wallpaper mode runs");
            }
        }

        /// <summary>
        /// Puts the sound back once wallpaper mode is left. Called when the main menu opens; region reloads do not pass
        /// through it, so a silent wallpaper stays silent while it changes region.
        /// </summary>
        internal static void Restore()
        {
            SetSoundOn(true);
        }
    }
}
