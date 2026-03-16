using System;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    internal sealed class WallpaperRainCycleController
    {
        private const float CycleCompletionThreshold = 0.85f;
        private const float NoRainThreshold = 0.95f;
        private const float DefaultRainCountdownMin = 60f;
        private const float DefaultRainCountdownMax = 180f;

        private readonly System.Random random;

        private bool hasTriggeredRainCountdown;
        private bool isRainCountdownActive;
        private float rainCountdownTimer;
        private float rainCountdownDuration = 120f;

        public WallpaperRainCycleController(System.Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public float RainCountdownRemaining => isRainCountdownActive ? (rainCountdownDuration - rainCountdownTimer) : 0f;

        public bool IsRainCountdownActive => isRainCountdownActive;

        public bool IsNoRainWaitMode => WallpaperMod.Options?.NoRainTransition.Value ?? false;

        public float GetCycleProgress(RainWorldGame game)
        {
            if (game?.world?.rainCycle == null)
            {
                return 0f;
            }

            int cycleLength = game.world.rainCycle.cycleLength;
            if (cycleLength <= 0)
            {
                return 0f;
            }

            return (float)game.world.rainCycle.timer / cycleLength;
        }

        public void ResetForRegion(RainWorldGame game)
        {
            hasTriggeredRainCountdown = false;
            isRainCountdownActive = false;
            rainCountdownTimer = 0f;

            if (game?.world?.rainCycle == null)
            {
                return;
            }

            int timer = game.world.rainCycle.timer;
            int cycleLength = game.world.rainCycle.cycleLength;
            float cycleProgress = cycleLength > 0 ? (float)timer / cycleLength : 0f;

            WallpaperMod.Log?.LogInfo($"Rain tracking reset for new region (cycle: {timer}/{cycleLength}, progress: {cycleProgress:P0})");

            if (cycleProgress >= CycleCompletionThreshold)
            {
                rainCountdownDuration = GetRandomCountdownDuration();
                rainCountdownTimer = 0f;
                isRainCountdownActive = true;
                hasTriggeredRainCountdown = true;
                WallpaperMod.Log?.LogInfo($"Cycle already {cycleProgress:P0} complete! Starting countdown: {rainCountdownDuration:F1}s");
            }
        }

        public void Clear()
        {
            hasTriggeredRainCountdown = false;
            isRainCountdownActive = false;
            rainCountdownTimer = 0f;
            rainCountdownDuration = 120f;
        }

        public bool Update(RainWorldGame game, float dt, bool isRoomLocked)
        {
            if (game?.world?.rainCycle == null)
            {
                return false;
            }

            int timer = game.world.rainCycle.timer;
            int cycleLength = game.world.rainCycle.cycleLength;
            if (cycleLength <= 0)
            {
                return false;
            }

            float cycleProgress = (float)timer / cycleLength;
            bool noRainWait = IsNoRainWaitMode;

            if (Time.frameCount % 300 == 0)
            {
                WallpaperMod.Log?.LogInfo($"Rain Cycle: timer={timer}/{cycleLength} ({cycleProgress:P1}), threshold={CycleCompletionThreshold:P0}, countdown_active={isRainCountdownActive}, triggered={hasTriggeredRainCountdown}, noRainWait={noRainWait}");
            }

            if (noRainWait)
            {
                if (!hasTriggeredRainCountdown && cycleProgress >= NoRainThreshold && !isRoomLocked)
                {
                    hasTriggeredRainCountdown = true;
                    WallpaperMod.Log?.LogInfo($"[Rain World Wallpaper Mode] No Rain Wait: Instant transition at {cycleProgress:P1}!");
                    return true;
                }

                return false;
            }

            if (!hasTriggeredRainCountdown && cycleProgress >= CycleCompletionThreshold)
            {
                float minCountdown = WallpaperMod.Options?.RainCountdownMin.Value ?? DefaultRainCountdownMin;
                float maxCountdown = WallpaperMod.Options?.RainCountdownMax.Value ?? DefaultRainCountdownMax;
                rainCountdownDuration = GetRandomCountdownDuration(minCountdown, maxCountdown);
                rainCountdownTimer = 0f;
                isRainCountdownActive = true;
                hasTriggeredRainCountdown = true;

                WallpaperMod.Log?.LogInfo($"[Rain World Wallpaper Mode] Day ending ({cycleProgress:P1} complete)! Changing region in {rainCountdownDuration:F1}s (range: {minCountdown}-{maxCountdown}s)");
            }

            if (isRainCountdownActive && !isRoomLocked)
            {
                rainCountdownTimer += dt;

                if (rainCountdownTimer >= rainCountdownDuration)
                {
                    isRainCountdownActive = false;
                    rainCountdownTimer = 0f;
                    return true;
                }
            }

            return false;
        }

        private float GetRandomCountdownDuration()
        {
            return GetRandomCountdownDuration(DefaultRainCountdownMin, DefaultRainCountdownMax);
        }

        private float GetRandomCountdownDuration(float minCountdown, float maxCountdown)
        {
            if (maxCountdown < minCountdown)
            {
                float swap = minCountdown;
                minCountdown = maxCountdown;
                maxCountdown = swap;
            }

            if (Math.Abs(maxCountdown - minCountdown) < float.Epsilon)
            {
                return minCountdown;
            }

            return minCountdown + (float)(random.NextDouble() * (maxCountdown - minCountdown));
        }
    }
}
