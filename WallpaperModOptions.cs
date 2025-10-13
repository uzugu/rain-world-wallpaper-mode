using Menu.Remix.MixedUI;
using System;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Remix configuration interface for Wallpaper Mode settings
    /// </summary>
    public class WallpaperModOptions : OptionInterface
    {
        // Configuration values
        public readonly Configurable<float> RegionDuration;
        public readonly Configurable<float> TransitionDuration;
        public readonly Configurable<float> StayDuration;
        public readonly Configurable<float> HudFadeDelay;
        public readonly Configurable<bool> AlwaysShowHud;
        public readonly Configurable<string> StartRegion;

        public WallpaperModOptions()
        {
            // Bind configuration values with defaults
            RegionDuration = config.Bind("regionDuration", 300f);
            TransitionDuration = config.Bind("transitionDuration", 5f);
            StayDuration = config.Bind("stayDuration", 15f);
            HudFadeDelay = config.Bind("hudFadeDelay", 3f);
            AlwaysShowHud = config.Bind("alwaysShowHud", true);
            StartRegion = config.Bind("startRegion", "SU");
        }

        public override void Initialize()
        {
            base.Initialize();

            try
            {
                var opTab = new OpTab(this, "Settings");
                Tabs = new[] { opTab };

                float leftColumn = 100f;
                float rightColumn = 350f;
                float yPos = 550f;
                float lineHeight = 60f;

                // Title
                opTab.AddItems(new UIelement[]
                {
                    new OpLabel(leftColumn, yPos, "Wallpaper Mode Settings", bigText: true)
                });
                yPos -= lineHeight * 1.2f;

                // Region Duration
                OpTextBox regionDurationBox = new OpTextBox(RegionDuration, new Vector2(rightColumn, yPos - 5f), 80f);
                regionDurationBox.description = "Duration in minutes to spend in each region (1-30)";
                opTab.AddItems(new UIelement[]
                {
                    new OpLabel(leftColumn, yPos, "Region Duration (min):"),
                    regionDurationBox
                });
                yPos -= lineHeight;

                // Transition Duration
                OpTextBox transitionDurationBox = new OpTextBox(TransitionDuration, new Vector2(rightColumn, yPos - 5f), 80f);
                transitionDurationBox.description = "Duration in seconds for camera transitions between rooms (1-15)";
                opTab.AddItems(new UIelement[]
                {
                    new OpLabel(leftColumn, yPos, "Transition Duration (sec):"),
                    transitionDurationBox
                });
                yPos -= lineHeight;

                // Stay Duration
                OpTextBox stayDurationBox = new OpTextBox(StayDuration, new Vector2(rightColumn, yPos - 5f), 80f);
                stayDurationBox.description = "Duration in seconds to stay in each room before transitioning (5-60)";
                opTab.AddItems(new UIelement[]
                {
                    new OpLabel(leftColumn, yPos, "Room Stay Duration (sec):"),
                    stayDurationBox
                });
                yPos -= lineHeight;

                // HUD Fade Delay
                OpTextBox hudFadeDelayBox = new OpTextBox(HudFadeDelay, new Vector2(rightColumn, yPos - 5f), 80f);
                hudFadeDelayBox.description = "Delay in seconds before HUD fades out (1-10)";
                opTab.AddItems(new UIelement[]
                {
                    new OpLabel(leftColumn, yPos, "HUD Fade Delay (sec):"),
                    hudFadeDelayBox
                });
                yPos -= lineHeight;

                // Always Show HUD
                OpCheckBox alwaysShowHudBox = new OpCheckBox(AlwaysShowHud, new Vector2(rightColumn, yPos));
                alwaysShowHudBox.description = "If enabled, the HUD will always be visible and won't fade out";
                opTab.AddItems(new UIelement[]
                {
                    new OpLabel(leftColumn, yPos + 2f, "Always Show HUD:"),
                    alwaysShowHudBox
                });
                yPos -= lineHeight;

                // Start Region
                OpTextBox startRegionBox = new OpTextBox(StartRegion, new Vector2(rightColumn, yPos - 5f), 80f);
                startRegionBox.description = "Region code to start in (e.g., SU, HI, CC)";
                opTab.AddItems(new UIelement[]
                {
                    new OpLabel(leftColumn, yPos, "Start Region Code:"),
                    startRegionBox
                });
                yPos -= lineHeight * 1.2f;

                // Control hints
                opTab.AddItems(new UIelement[]
                {
                    new OpLabel(leftColumn, yPos, "In-Game Controls:", bigText: false),
                });
                yPos -= 30f;

                opTab.AddItems(new UIelement[]
                {
                    new OpLabelLong(new Vector2(leftColumn, yPos - 100f), new Vector2(500f, 100f),
                        "N - Next Room | G - Next Region | B - Previous Region\n" +
                        "+/- or PgUp/PgDn - Adjust Region Duration\n" +
                        "H - Toggle HUD Always Visible\n" +
                        "F1/Tab - Settings Overlay | Escape - Return to Menu")
                    {
                        verticalAlignment = OpLabel.LabelVAlignment.Top,
                        color = new Color(0.7f, 0.85f, 1f, 0.85f)
                    }
                });

                WallpaperMod.Log?.LogInfo("WallpaperModOptions: UI initialized successfully");
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"WallpaperModOptions: Failed to initialize UI - {ex}");
            }
        }

        public override void Update()
        {
            base.Update();

            // Clamp values to valid ranges
            float regionDuration = RegionDuration.Value;
            if (regionDuration < 60f)
                RegionDuration.Value = 60f;
            else if (regionDuration > 1800f)
                RegionDuration.Value = 1800f;

            float transitionDuration = TransitionDuration.Value;
            if (transitionDuration < 1f)
                TransitionDuration.Value = 1f;
            else if (transitionDuration > 15f)
                TransitionDuration.Value = 15f;

            float stayDuration = StayDuration.Value;
            if (stayDuration < 5f)
                StayDuration.Value = 5f;
            else if (stayDuration > 60f)
                StayDuration.Value = 60f;

            float hudFadeDelay = HudFadeDelay.Value;
            if (hudFadeDelay < 1f)
                HudFadeDelay.Value = 1f;
            else if (hudFadeDelay > 10f)
                HudFadeDelay.Value = 10f;
        }
    }
}
