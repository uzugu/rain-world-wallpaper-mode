using System;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Auto-hiding HUD for Wallpaper Mode
    /// Shows current location, next location, and region progress
    /// Fades out after inactivity, reappears on mouse movement
    /// </summary>
    public class WallpaperHUD
    {
        private RoomCamera camera;
        private WallpaperController controller;

        // HUD Elements
        private FContainer hudContainer;
        private FLabel currentRegionLabel;
        private FLabel currentRoomLabel;
        private FLabel nextRoomLabel;
        private FLabel nextRegionLabel;
        private FLabel regionTimeLabel;
        private FLabel controlHintLabel;
        private float idleTimer = 0f;
        private float fadeDelay = 3f; // Fade after 3 seconds

        // Fade state
        private float currentAlpha = 1f;
        private float targetAlpha = 1f;
        private bool isVisible = true;

        // Configuration (will be loaded from config)
        private bool alwaysShowHUD = true;

        public bool IsReady { get; private set; }
        public bool AlwaysShowHUD => alwaysShowHUD;

        public WallpaperHUD(RoomCamera camera, WallpaperController controller)
        {
            this.camera = camera;
            this.controller = controller;

            // Load settings from config
            if (WallpaperMod.Options != null)
            {
                fadeDelay = WallpaperMod.Options.HudFadeDelay.Value;
                alwaysShowHUD = WallpaperMod.Options.AlwaysShowHud.Value;
            }

            InitializeHUD();
        }

        private void InitializeHUD()
        {
            try
            {
                // Create main container and add directly to Futile's stage
                // This works even without a camera HUD, like Rain Meadow does
                hudContainer = new FContainer();
                Futile.stage.AddChild(hudContainer);

                // Create labels
                currentRegionLabel = CreateLabel(100f, 720f, "");
                currentRoomLabel = CreateLabel(100f, 690f, "");
                nextRoomLabel = CreateLabel(100f, 660f, "");
                nextRegionLabel = CreateLabel(100f, 630f, "");
                regionTimeLabel = CreateLabel(100f, 600f, "");
                controlHintLabel = CreateLabel(100f, 560f, "");

                regionTimeLabel.color = new Color(0f, 0.7f, 1f, 1f);
                controlHintLabel.color = new Color(0.7f, 0.85f, 1f, 0.8f);
                controlHintLabel.scale = 0.9f;

                hudContainer.AddChild(currentRegionLabel);
                hudContainer.AddChild(currentRoomLabel);
                hudContainer.AddChild(nextRoomLabel);
                hudContainer.AddChild(nextRegionLabel);
                hudContainer.AddChild(regionTimeLabel);
                hudContainer.AddChild(controlHintLabel);

                WallpaperMod.Log?.LogInfo("WallpaperHUD: Initialized successfully");
                IsReady = true;
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"WallpaperHUD: Failed to initialize - {ex}");
                IsReady = false;
            }
        }

        private FLabel CreateLabel(float x, float y, string text)
        {
            // Try to use the display font, fallback to font if not available
            string fontName = "DisplayFont";
            try
            {
                if (Futile.atlasManager.GetAtlasWithName("font") != null)
                {
                    fontName = "font";
                }
            }
            catch
            {
                // If font check fails, use DisplayFont
            }

            return new FLabel(fontName, text)
            {
                x = x,
                y = y,
                alignment = FLabelAlignment.Left,
                color = new Color(0f, 0.8f, 1f, 1f) // Bright cyan
            };
        }

        public void Update()
        {
            if (currentRegionLabel == null)
            {
                return;
            }

            if (alwaysShowHUD)
            {
                targetAlpha = 1f;
                isVisible = true;
                if (!Mathf.Approximately(currentAlpha, 1f))
                {
                    currentAlpha = 1f;
                    UpdateAlpha();
                }
            }
            else if (isVisible)
            {
                idleTimer += Time.deltaTime;

                if (idleTimer >= fadeDelay)
                {
                    targetAlpha = 0f;
                    isVisible = false;
                }
            }

            if (!Mathf.Approximately(currentAlpha, targetAlpha))
            {
                float fadeSpeed = 2f; // Fade over 0.5 seconds
                currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
                UpdateAlpha();
            }
            else if (alwaysShowHUD && !Mathf.Approximately(currentAlpha, 1f))
            {
                currentAlpha = 1f;
                targetAlpha = 1f;
                UpdateAlpha();
            }

            UpdateLabels();
        }

        public void RegisterUserActivity()
        {
            idleTimer = 0f;

            if (alwaysShowHUD)
            {
                return;
            }

            targetAlpha = 1f;
            isVisible = true;
            if (currentAlpha < 1f)
            {
                currentAlpha = 1f;
                UpdateAlpha();
            }
        }

        public void SetAlwaysShowHUD(bool value)
        {
            alwaysShowHUD = value;

            // Save to config
            if (WallpaperMod.Options != null)
            {
                WallpaperMod.Options.AlwaysShowHud.Value = value;
            }

            if (alwaysShowHUD)
            {
                targetAlpha = 1f;
                currentAlpha = 1f;
                isVisible = true;
                idleTimer = 0f;
                UpdateAlpha();
            }
            else
            {
                idleTimer = 0f;
                targetAlpha = 1f;
                isVisible = true;
            }
        }

        private void UpdateAlpha()
        {
            if (hudContainer != null)
            {
                hudContainer.alpha = currentAlpha;
            }
        }

        private void UpdateLabels()
        {
            if (controller.RegionManager == null || currentRegionLabel == null)
            {
                return;
            }

            string currentRegionCode = controller.CurrentRegionCode;
            string nextRegionCode = controller.NextRegionCode;
            string currentRoom = controller.CurrentRoomName;
            string nextRoom = controller.NextRoomName;

            currentRegionLabel.text = $"Region: {GetRegionName(currentRegionCode)} ({currentRegionCode})";
            currentRoomLabel.text = string.IsNullOrEmpty(currentRoom)
                ? "Area: [Loading...]"
                : $"Area: {currentRoom}";

            nextRoomLabel.text = string.IsNullOrEmpty(nextRoom)
                ? "Next Room: [Calculating...]"
                : $"Next Room: {nextRoom}";

            if (!string.IsNullOrEmpty(nextRegionCode) && !string.Equals(nextRegionCode, currentRegionCode, StringComparison.OrdinalIgnoreCase))
            {
                nextRegionLabel.text = $"Next Region: {GetRegionName(nextRegionCode)} ({nextRegionCode})";
            }
            else
            {
                nextRegionLabel.text = "Next Region: [Pending]";
            }

            float elapsed = Mathf.Clamp(controller.RegionTimerSeconds, 0f, Mathf.Max(controller.RegionDurationSeconds, 0.01f));
            float total = Mathf.Max(controller.RegionDurationSeconds, 0.01f);
            regionTimeLabel.text = $"Region Time: {FormatTime(elapsed)} / {FormatTime(total)} | Regions {controller.RegionsExplored}/{controller.TotalRegions}";

            controlHintLabel.text = "Controls: Right Arrow/Dpad -> Next | N Next Room | G Next Region | B Prev Region | plus/minus or PgUp/PgDn Duration | F1/Tab Settings";
        }

        private string GetRegionName(string regionCode)
        {
            // Map region codes to full names
            switch (regionCode)
            {
                case "SU": return "Outskirts";
                case "HI": return "Industrial Complex";
                case "CC": return "Chimney Canopy";
                case "GW": return "Garbage Wastes";
                case "SH": return "Shaded Citadel";
                case "DS": return "Drainage System";
                case "SL": return "Shoreline";
                case "SI": return "Sky Islands";
                case "LF": return "Farm Arrays";
                case "UW": return "The Exterior";
                case "SS": return "Five Pebbles";
                case "SB": return "Subterranean";
                case "LM": return "Looks to the Moon";
                case "RM": return "Pipeyard";
                case "DM": return "Metropolis";
                case "LC": return "Outer Expanse";
                case "MS": return "Waterfront Facility";
                case "VS": return "Undergrowth";
                case "CL": return "Silent Construct";
                case "OE": return "Rubicon";
                default: return regionCode;
            }
        }

        private string FormatTime(float seconds)
        {
            float clamped = Mathf.Clamp(seconds, 0f, 359999f);
            var span = TimeSpan.FromSeconds(clamped);
            return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
        }

        public void Destroy()
        {
            if (hudContainer != null)
            {
                hudContainer.RemoveFromContainer();
            }

            WallpaperMod.Log?.LogInfo("WallpaperHUD: Destroyed");
            IsReady = false;
        }
    }
}
