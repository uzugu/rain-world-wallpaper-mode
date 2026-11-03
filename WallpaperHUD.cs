using System;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Heads-up display for Wallpaper Mode, drawn with the game's own fonts, colours and fades so that it reads like
    /// part of Rain World's HUD. It has three blocks, each of which fades on its own:
    ///   location (bottom left)    region and sub-region name, the room and the one the camera is heading for, next region
    ///   rain (bottom right)       rain countdown or cycle progress, region count, a thin cycle progress line
    ///   controls (bottom centre)  key or controller hints
    /// Input, or the always-show setting, brings up all three blocks and they fade out again after the configured
    /// delay. The location block also announces itself for a few seconds whenever a region or sub-region begins, even
    /// while the rest of the HUD is hidden.
    /// </summary>
    public class WallpaperHUD
    {
        // Layout, in the game's 768 pixel tall screen space (origin bottom left)
        private const float Margin = 40f;
        private const float ControlsBottom = 12f;
        private const float HintClearance = 2f;
        private const float ProgressBarHeight = 2f;
        private const float ProgressBarGap = 6f;
        private const float MinProgressBarWidth = 150f;

        // Timing, in game ticks (40 per second)
        private const int TitleHoldTicks = 160;
        private const int SubregionSettleTicks = 80;
        private const float DefaultFadeDelay = 3f;

        // Strength of the soft shadow behind each block
        private const float LocationShadowAlpha = 0.32f;
        private const float RainShadowAlpha = 0.3f;
        private const float ControlsShadowAlpha = 0.22f;

        private readonly RoomCamera camera;
        private readonly WallpaperController controller;
        private readonly RainWorld rainWorld;
        private readonly InGameTranslator translator;

        private FContainer root;
        private WallpaperHudBlock locationBlock;
        private WallpaperHudBlock rainBlock;
        private WallpaperHudBlock controlsBlock;
        private WallpaperHudLine regionLine;
        private WallpaperHudLine subregionLine;
        private WallpaperHudLine roomLine;
        private WallpaperHudLine nextRegionLine;
        private WallpaperHudLine rainLine;
        private WallpaperHudLine regionCountLine;
        private WallpaperHudLine controlsLine;
        private FSprite barTrack;
        private FSprite barFill;

        // Visibility
        private bool alwaysShowHUD;
        private float idleSeconds;
        private int titleHoldTicks;
        private bool destroyed;
        private bool faulted;

        // What the location lines currently show, so that text is only rebuilt when it changes
        private string shownRegionCode;
        private string shownRegionName;
        private string shownNextRegionCode;
        private string shownRoom;
        private string shownNextRoom;
        private Region shownSubregionRegion;
        private int shownSubregionIndex;

        // Sub-region announcements, ported from HUD.SubregionTracker
        private int lastSubregion;
        private int announcedSubregion;
        private int subregionSettleTicks;

        // What the rain lines currently show
        private int shownRainState = -1;
        private int shownRainValue = -1;
        private int shownExplored = -1;
        private int shownTotal = -1;
        private int shownBarPixels = -1;
        private float barWidth;

        private WallpaperInputDevice? shownDevice;
        private Vector2 layoutScreenSize;

        public bool IsReady { get; private set; }
        public bool AlwaysShowHUD => alwaysShowHUD;

        public WallpaperHUD(RoomCamera camera, WallpaperController controller)
        {
            this.camera = camera;
            this.controller = controller;
            rainWorld = camera?.game?.rainWorld ?? RWCustom.Custom.rainWorld;
            translator = rainWorld?.inGameTranslator;

            // Load settings from config
            if (WallpaperMod.Options != null)
            {
                alwaysShowHUD = WallpaperMod.Options.AlwaysShowHud.Value;
            }

            InitializeHUD();
        }

        private void InitializeHUD()
        {
            try
            {
                if (controller == null)
                {
                    throw new InvalidOperationException("the HUD needs a wallpaper controller");
                }

                // Built off stage and attached at the end, so a failure part way through leaves nothing behind.
                root = new FContainer();

                FShader flatLight = null;
                if (rainWorld != null && rainWorld.Shaders != null)
                {
                    rainWorld.Shaders.TryGetValue("FlatLight", out flatLight);
                }

                string font = RWCustom.Custom.GetFont();
                string displayFont = RWCustom.Custom.GetDisplayFont();
                Color white = global::Menu.Menu.MenuRGB(global::Menu.Menu.MenuColors.White);
                Color mediumGrey = global::Menu.Menu.MenuRGB(global::Menu.Menu.MenuColors.MediumGrey);
                Color darkGrey = global::Menu.Menu.MenuRGB(global::Menu.Menu.MenuColors.DarkGrey);

                // Blocks are drawn in the order they are created. The controls block is the widest, so it goes first
                // and its shadow lies behind the text of the other two instead of over it.

                // Controls, bottom centre
                controlsBlock = new WallpaperHudBlock(root, flatLight, ControlsShadowAlpha);
                controlsLine = controlsBlock.AddLine(font, darkGrey, FLabelAlignment.Center, 0f);

                // Rain and progress, bottom right
                rainBlock = new WallpaperHudBlock(root, flatLight, RainShadowAlpha);
                rainLine = rainBlock.AddLine(font, mediumGrey, FLabelAlignment.Right, 0f);
                regionCountLine = rainBlock.AddLine(font, darkGrey, FLabelAlignment.Right, 0f);
                barTrack = CreateBarSprite(darkGrey, 0.55f);
                barFill = CreateBarSprite(mediumGrey, 1f);
                rainBlock.Container.AddChild(barTrack);
                rainBlock.Container.AddChild(barFill);

                // Where the camera is, bottom left. Lines are listed from the top of the block down.
                locationBlock = new WallpaperHudBlock(root, flatLight, LocationShadowAlpha);
                regionLine = locationBlock.AddLine(displayFont, white, FLabelAlignment.Left, 4f);
                subregionLine = locationBlock.AddLine(font, mediumGrey, FLabelAlignment.Left, 4f);
                roomLine = locationBlock.AddLine(font, darkGrey, FLabelAlignment.Left, 0f);
                nextRegionLine = locationBlock.AddLine(font, darkGrey, FLabelAlignment.Left, 0f);

                GetParentContainer().AddChild(root);
                layoutScreenSize = GetScreenSize();

                WallpaperMod.Log?.LogInfo("WallpaperHUD: Initialized successfully");
                IsReady = true;
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"WallpaperHUD: Failed to initialize - {ex}");
                RemoveParts();
                IsReady = false;
            }
        }

        private static FSprite CreateBarSprite(Color color, float alpha)
        {
            // "pixel" is one pixel square, so its scale is its size in pixels.
            return new FSprite("pixel") { anchorX = 0f, color = color, alpha = alpha, scaleY = ProgressBarHeight };
        }

        private FContainer GetParentContainer()
        {
            if (camera != null)
            {
                try
                {
                    FContainer layer = camera.ReturnFContainer("HUD2");
                    if (layer != null)
                    {
                        return layer;
                    }
                }
                catch (Exception ex)
                {
                    WallpaperMod.Log?.LogWarning($"WallpaperHUD: HUD2 layer unavailable, drawing on the Futile stage instead - {ex.Message}");
                }
            }

            return Futile.stage;
        }

        private Vector2 GetScreenSize()
        {
            if (rainWorld != null && rainWorld.options != null)
            {
                return rainWorld.options.ScreenSize;
            }

            return new Vector2(Futile.screen.pixelWidth, Futile.screen.pixelHeight);
        }

        private Vector2 GetSafeOffset()
        {
            return rainWorld != null && rainWorld.options != null ? rainWorld.options.SafeScreenOffset : Vector2.zero;
        }

        private static float GetFadeDelay()
        {
            return WallpaperMod.Options != null ? Mathf.Max(0.1f, WallpaperMod.Options.HudFadeDelay.Value) : DefaultFadeDelay;
        }

        /// <summary>
        /// Called once per game tick; <paramref name="dt"/> is the length of a tick in seconds.
        /// </summary>
        public void Update(float dt)
        {
            if (!IsReady || faulted)
            {
                return;
            }

            try
            {
                float cycleProgress = controller.CycleProgress;

                if (!alwaysShowHUD && idleSeconds < GetFadeDelay())
                {
                    idleSeconds += dt;
                }

                RefreshLocation();
                RefreshRain(cycleProgress);
                RefreshControls();

                // The location block is also held up by a region or sub-region announcement; the other two only show
                // with the full HUD. The announcement's hold only runs down once the block has finished fading in.
                bool fullHud = alwaysShowHUD || idleSeconds < GetFadeDelay();
                if (titleHoldTicks > 0 && locationBlock.IsFullyShown)
                {
                    titleHoldTicks--;
                }

                locationBlock.Step(fullHud || titleHoldTicks > 0);
                rainBlock.Step(fullHud);
                controlsBlock.Step(fullHud);

                ApplyLayout();
                UpdateProgressBar(cycleProgress);
            }
            catch (Exception ex)
            {
                OnFault(ex);
            }
        }

        private void OnFault(Exception ex)
        {
            // The HUD is cosmetic: never let it take the wallpaper down, and do not repeat the error every tick.
            faulted = true;
            WallpaperMod.Log?.LogError($"WallpaperHUD: Update failed, the HUD stays off until the next region load - {ex}");

            try
            {
                HideAllBlocks();
            }
            catch (Exception hideException)
            {
                WallpaperMod.Log?.LogError($"WallpaperHUD: Could not hide the HUD after the failure - {hideException.Message}");
            }
        }

        // Location block --------------------------------------------------------------------------------------

        private void RefreshLocation()
        {
            bool regionChanged = RefreshRegionTitle();
            int subregion = RefreshSubregionLine();

            if (regionChanged)
            {
                // The sub-region line is part of this announcement, so it must not be announced again 80 ticks later.
                AnnounceLocation(subregion);
            }
            else
            {
                TrackSubregion(subregion);
            }

            lastSubregion = subregion;
            RefreshRoomLine();
            RefreshNextRegionLine();
        }

        // Returns true when the region is new to this HUD, either because the HUD just appeared or the region changed.
        private bool RefreshRegionTitle()
        {
            string code = controller.CurrentRegionCode;
            if (string.IsNullOrEmpty(code) || string.Equals(code, shownRegionCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            shownRegionCode = code;
            shownRegionName = WallpaperHudText.ResolveRegionName(code, translator);
            SetLineText(locationBlock, regionLine, shownRegionName);

            // The sub-region line leaves out a name that repeats the region's, so check it again against the new one.
            shownSubregionIndex = -1;
            return true;
        }

        // Shows the sub-region of the room the camera is in and returns its index (0 when there is none).
        private int RefreshSubregionLine()
        {
            Room room = camera != null ? camera.room : null;
            int index = WallpaperHudText.FindSubregionIndex(room, out Region region);
            if (index != shownSubregionIndex || !ReferenceEquals(region, shownSubregionRegion))
            {
                shownSubregionIndex = index;
                shownSubregionRegion = region;
                string name = index > 0 ? WallpaperHudText.GetSubregionName(region, index, translator) : string.Empty;

                // Some regions name a sub-region after themselves (SU's "Outskirts"); do not show the name twice.
                if (string.Equals(name, shownRegionName, StringComparison.OrdinalIgnoreCase))
                {
                    name = string.Empty;
                }

                SetLineText(locationBlock, subregionLine, name);
            }

            return index;
        }

        // The vanilla rule: a sub-region is announced once the camera has stayed in it for a while, and not again until
        // a different sub-region has been announced. Passing through a room without a sub-region does not count.
        private void TrackSubregion(int subregion)
        {
            if (subregion != 0 && subregion != announcedSubregion && subregion == lastSubregion)
            {
                subregionSettleTicks++;
                if (subregionSettleTicks >= SubregionSettleTicks)
                {
                    AnnounceLocation(subregion);
                }
            }
            else
            {
                subregionSettleTicks = 0;
            }
        }

        // Fades the location block in by itself and holds it for a few seconds, whatever the rest of the HUD is doing.
        private void AnnounceLocation(int subregion)
        {
            titleHoldTicks = TitleHoldTicks;
            announcedSubregion = subregion;
            subregionSettleTicks = 0;
        }

        private void RefreshRoomLine()
        {
            string current = controller.CurrentRoomName;
            if (string.IsNullOrEmpty(current) && camera != null && camera.room != null && camera.room.abstractRoom != null)
            {
                // Until the first transition has finished the controller does not know the room yet.
                current = camera.room.abstractRoom.name;
            }

            // The next room is only known while a transition runs, and a camera that stays in its room names that room.
            string next = controller.NextRoomName;
            if (string.IsNullOrEmpty(next) || string.Equals(next, current, StringComparison.OrdinalIgnoreCase))
            {
                next = null;
            }

            if (!string.Equals(current, shownRoom, StringComparison.Ordinal) || !string.Equals(next, shownNextRoom, StringComparison.Ordinal))
            {
                shownRoom = current;
                shownNextRoom = next;
                SetLineText(locationBlock, roomLine, WallpaperHudText.BuildRoomLine(current, next));
            }
        }

        private void RefreshNextRegionLine()
        {
            string nextCode = controller.NextRegionCode;
            if (string.IsNullOrEmpty(nextCode) || string.Equals(nextCode, controller.CurrentRegionCode, StringComparison.OrdinalIgnoreCase))
            {
                nextCode = null;
            }

            if (!string.Equals(nextCode, shownNextRegionCode, StringComparison.OrdinalIgnoreCase))
            {
                shownNextRegionCode = nextCode;
                string text = nextCode == null
                    ? string.Empty
                    : WallpaperHudText.BuildNextRegionLine(WallpaperHudText.ResolveRegionName(nextCode, translator));
                SetLineText(locationBlock, nextRegionLine, text);
            }
        }

        // Rain block ------------------------------------------------------------------------------------------

        private void RefreshRain(float cycleProgress)
        {
            WallpaperHudRainState state;
            int value;
            if (controller.IsNoRainWaitMode)
            {
                state = WallpaperHudRainState.NoRainWait;
                value = ToPercent(cycleProgress);
            }
            else if (controller.IsRainCountdownActive)
            {
                state = WallpaperHudRainState.Countdown;
                value = Mathf.CeilToInt(controller.RainCountdownRemaining);
            }
            else
            {
                state = WallpaperHudRainState.Waiting;
                value = ToPercent(cycleProgress);
            }

            if ((int)state != shownRainState || value != shownRainValue)
            {
                shownRainState = (int)state;
                shownRainValue = value;
                SetLineText(rainBlock, rainLine, WallpaperHudText.BuildRainLine(state, value));
            }

            int explored = controller.RegionsExplored;
            int total = controller.TotalRegions;
            if (explored != shownExplored || total != shownTotal)
            {
                shownExplored = explored;
                shownTotal = total;
                SetLineText(rainBlock, regionCountLine, total > 0 ? WallpaperHudText.BuildRegionCountLine(explored, total) : string.Empty);
            }
        }

        private static int ToPercent(float progress)
        {
            return Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
        }

        private void UpdateProgressBar(float cycleProgress)
        {
            int pixels = Mathf.RoundToInt(barWidth * Mathf.Clamp01(cycleProgress));
            if (pixels != shownBarPixels)
            {
                shownBarPixels = pixels;
                barFill.scaleX = pixels;
            }
        }

        private void PlaceProgressBar(Rect area)
        {
            barWidth = Mathf.Round(area.width);
            float left = Mathf.Round(area.x);
            float centerY = Mathf.Round(area.y) + ProgressBarHeight * 0.5f;

            barTrack.x = left;
            barTrack.y = centerY;
            barTrack.scaleX = barWidth;
            barFill.x = left;
            barFill.y = centerY;
            shownBarPixels = -1;
        }

        // Controls block --------------------------------------------------------------------------------------

        private void RefreshControls()
        {
            WallpaperInputDevice device = controller.ActiveInputDevice;
            if (shownDevice != device)
            {
                shownDevice = device;
                string hint = device == WallpaperInputDevice.Controller ? WallpaperHudText.ControllerHint : WallpaperHudText.KeyboardHint;
                SetLineText(controlsBlock, controlsLine, hint);
            }
        }

        // Layout ----------------------------------------------------------------------------------------------

        private static void SetLineText(WallpaperHudBlock block, WallpaperHudLine line, string text)
        {
            if (line.SetText(text))
            {
                block.LayoutDirty = true;
            }
        }

        private void ApplyLayout()
        {
            // Everything is positioned from the screen size, which is 1024 to 1366 pixels wide depending on the player's
            // resolution. A change of resolution moves it all.
            Vector2 screen = GetScreenSize();
            if (screen != layoutScreenSize)
            {
                layoutScreenSize = screen;
                locationBlock.LayoutDirty = true;
                rainBlock.LayoutDirty = true;
                controlsBlock.LayoutDirty = true;
            }

            Vector2 safe = GetSafeOffset();

            if (locationBlock.LayoutDirty)
            {
                locationBlock.Layout(Margin + safe.x, FLabelAlignment.Left, Margin + safe.y, 0f, 0f);
            }

            if (rainBlock.LayoutDirty)
            {
                Rect area = rainBlock.Layout(
                    screen.x - Margin - safe.x,
                    FLabelAlignment.Right,
                    Margin + safe.y,
                    ProgressBarGap + ProgressBarHeight,
                    MinProgressBarWidth);
                PlaceProgressBar(area);
            }

            if (controlsBlock.LayoutDirty)
            {
                // The hint sits on the bottom edge, below the rows of the two side blocks, so a long hint cannot run
                // into them on a narrow screen.
                // The fonts of some languages have tall lines that would reach up into the side blocks' rows, so the top
                // of the hint is kept under the margin.
                float hintBottom = Mathf.Clamp(Margin - HintClearance - controlsLine.Height, 0f, ControlsBottom);
                controlsBlock.Layout(screen.x * 0.5f, FLabelAlignment.Center, hintBottom + safe.y, 0f, 0f);
            }
        }

        // Public controls -------------------------------------------------------------------------------------

        /// <summary>
        /// Input happened: show the whole HUD and start the fade delay again.
        /// </summary>
        public void RegisterUserActivity()
        {
            idleSeconds = 0f;
        }

        public void SetAlwaysShowHUD(bool value, bool hideImmediately = false)
        {
            alwaysShowHUD = value;

            // Save to config
            if (WallpaperMod.Options != null)
            {
                WallpaperMod.Options.AlwaysShowHud.Value = value;
            }

            if (alwaysShowHUD)
            {
                idleSeconds = 0f;
            }
            else if (hideImmediately)
            {
                // Hide everything at once, including an announcement that is still showing.
                idleSeconds = float.MaxValue;
                titleHoldTicks = 0;
                HideAllBlocks();
            }
            else
            {
                // Keep the HUD up for the fade delay, then let it fade out.
                idleSeconds = 0f;
            }
        }

        private void HideAllBlocks()
        {
            locationBlock?.HideNow();
            rainBlock?.HideNow();
            controlsBlock?.HideNow();
        }

        public void Destroy()
        {
            if (destroyed)
            {
                return;
            }

            destroyed = true;
            IsReady = false;
            RemoveParts();
            WallpaperMod.Log?.LogInfo("WallpaperHUD: Destroyed");
        }

        private void RemoveParts()
        {
            try
            {
                locationBlock?.Remove();
                rainBlock?.Remove();
                controlsBlock?.Remove();
                root?.RemoveAllChildren();
                root?.RemoveFromContainer();
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"WallpaperHUD: Failed to remove the HUD sprites - {ex}");
            }

            locationBlock = null;
            rainBlock = null;
            controlsBlock = null;
            root = null;
        }
    }
}
