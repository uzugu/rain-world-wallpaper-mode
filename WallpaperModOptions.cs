using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Remix configuration interface for Wallpaper Mode settings
    /// </summary>
    public class WallpaperModOptions : OptionInterface
    {
        // Enums for dropdowns
        public enum CampaignChoice
        {
            White,      // Survivor
            Yellow,     // Monk
            Red,        // Hunter
            Gourmand,   // Downpour
            Artificer,  // Downpour
            Rivulet,    // Downpour
            Spearmaster,// Downpour
            Saint,      // Downpour
            Watcher     // The Watcher campaign
        }

        public enum RegionChoice
        {
            SU,  // Outskirts
            HI,  // Industrial Complex
            CC,  // Chimney Canopy
            GW,  // Garbage Wastes
            SH,  // Shaded Citadel
            DS,  // Drainage System
            SL,  // Shoreline
            SI,  // Sky Islands
            LF,  // Farm Arrays
            UW,  // The Exterior
            SS,  // Five Pebbles
            SB,  // Subterranean
            UG,  // Undergrowth (Downpour)
            LM,  // Waterfront Facility (Downpour)
            RM,  // The Rot (Downpour)
            DM,  // Looks to the Moon (Downpour)
            LC,  // Metropolis (Downpour)
            MS,  // Submerged Superstructure (Downpour)
            VS,  // Pipeyard (Downpour)
            CL,  // Silent Construct (Downpour)
            OE,  // Outer Expanse (Downpour)
            HR,  // Rubicon (Downpour)
            WVWA,
            WVWB,
            WRRA,
            WPGA,
            WARA,
            WARB,
            WARC,
            WARD,
            WARE,
            WARF,
            WARG,
            WMPA,
            WAUA,
            WBLA,
            WPTA,
            WRFA,
            WRFB,
            WRSA,
            WSKA,
            WSKB,
            WSKC,
            WSKD,
            WTDA,
            WTDB,
            WORA,
            WDSR,
            WGWR,
            WHIR,
            WSSR,
            WSUR
        }

        public enum CameraMode
        {
            RandomExploration,  // Random start, random jumps, random order (default)
            Random,             // Pick one random camera position
            Sequential,         // Cycle through all camera positions
            FirstOnly           // Always use first camera position
        }

        public enum RotState
        {
            Natural,    // Session-generated Watcher-style mixed rot progression
            Normal,     // Clean world, no rot effects (like WMPA)
            Rotted,     // Sentient Rot Spores active, incremental progression
            Karma,      // Karma flowers everywhere, FlowerWind active (like WORA)
            RottedKarma // Sentient Rot and karma flowers together
        }

        // Allowed ranges for the numeric settings (seconds unless noted). Remix enforces them through the
        // configurables' acceptable ranges, and WallpaperSettingsOverlay reuses them for its runtime changes
        // so the two cannot drift apart.
        public const float TransitionDurationRangeMin = 0.5f;
        public const float TransitionDurationRangeMax = 60f;
        public const float StayDurationRangeMin = 1f;
        public const float StayDurationRangeMax = 600f;
        public const float HudFadeDelayRangeMin = 0f;
        public const float HudFadeDelayRangeMax = 60f;
        public const int ChaosLevelRangeMin = 1;
        public const int ChaosLevelRangeMax = 10;
        public const int RainCountdownRangeMin = 0;
        public const int RainCountdownRangeMax = 600;

        // Configuration values
        public readonly Configurable<float> TransitionDuration;
        public readonly Configurable<float> StayDuration;
        public readonly Configurable<float> HudFadeDelay;
        public readonly Configurable<bool> AlwaysShowHud;
        public readonly Configurable<string> StartRegion;
        public readonly Configurable<string> SelectedCampaign;
        public readonly Configurable<string> CameraModeConfig;
        public readonly Configurable<bool> EnableChaos;
        public readonly Configurable<int> ChaosLevel;
        public readonly Configurable<bool> ChaosSpawnAll;
        public readonly Configurable<bool> NoRainTransition;
        public readonly Configurable<int> RainCountdownMin;
        public readonly Configurable<int> RainCountdownMax;
        public readonly Configurable<string> RotStateConfig;

        public WallpaperModOptions()
        {
            // Bind configuration values with defaults (using strings for enums to work with OpResourceSelector)
            // Numeric values get an acceptable range, so Remix clamps anything outside it, including values
            // loaded from an existing config file
            TransitionDuration = config.Bind("transitionDuration", 5f, new ConfigAcceptableRange<float>(TransitionDurationRangeMin, TransitionDurationRangeMax));
            StayDuration = config.Bind("stayDuration", 15f, new ConfigAcceptableRange<float>(StayDurationRangeMin, StayDurationRangeMax));
            HudFadeDelay = config.Bind("hudFadeDelay", 3f, new ConfigAcceptableRange<float>(HudFadeDelayRangeMin, HudFadeDelayRangeMax));
            AlwaysShowHud = config.Bind("alwaysShowHud", false);
            StartRegion = config.Bind("startRegion", RegionChoice.SU.ToString());
            SelectedCampaign = config.Bind("selectedCampaign", CampaignChoice.White.ToString());
            CameraModeConfig = config.Bind("cameraMode", CameraMode.RandomExploration.ToString());
            EnableChaos = config.Bind("enableChaos", false);
            ChaosLevel = config.Bind("chaosLevel", 1, new ConfigAcceptableRange<int>(ChaosLevelRangeMin, ChaosLevelRangeMax));
            ChaosSpawnAll = config.Bind("chaosSpawnAll", false);
            NoRainTransition = config.Bind("noRainTransition", false);
            RainCountdownMin = config.Bind("rainCountdownMin", 60, new ConfigAcceptableRange<int>(RainCountdownRangeMin, RainCountdownRangeMax));
            RainCountdownMax = config.Bind("rainCountdownMax", 180, new ConfigAcceptableRange<int>(RainCountdownRangeMin, RainCountdownRangeMax));
            RotStateConfig = config.Bind("rotState", RotState.Natural.ToString());
        }

        public override void Initialize()
        {
            base.Initialize();

            try
            {
                var opTab = new OpTab(this, "Settings");
                Tabs = new[] { opTab };

                // Two-column layout with increased spacing
                float leftColumnLabel = 50f;
                float leftColumnControl = 150f;
                float rightColumnLabel = 350f;
                float rightColumnControl = 450f;
                float yPos = 550f;
                float lineHeight = 60f;

                // Create all UI elements first, add dropdowns last for proper z-ordering
                var uiElements = new List<UIelement>();

                // Title (full width)
                uiElements.Add(new OpLabel(leftColumnLabel, yPos, "Wallpaper Mode Settings", bigText: true));
                yPos -= lineHeight * 1.2f;

                // === LEFT COLUMN ===
                float leftYPos = yPos;

                // Campaign Selection label (dropdown added later)
                float campaignYPos = leftYPos;
                uiElements.Add(new OpLabel(leftColumnLabel, campaignYPos, "Campaign:"));
                leftYPos -= lineHeight;

                // Room Stay Duration
                // Numeric settings use OpUpdown: it takes its bounds from the configurable's acceptable range,
                // while OpTextBox accepts any number and only gets clamped when the config is saved
                OpUpdown stayDurationBox = new OpUpdown(StayDuration, new Vector2(leftColumnControl, leftYPos - 5f), 80f);
                stayDurationBox.description = $"Duration in seconds to stay in each room before transitioning ({StayDurationRangeMin}-{StayDurationRangeMax})";
                uiElements.Add(new OpLabel(leftColumnLabel, leftYPos, "Room (sec):"));
                uiElements.Add(stayDurationBox);
                leftYPos -= lineHeight;

                // Always Show HUD
                OpCheckBox alwaysShowHudBox = new OpCheckBox(AlwaysShowHud, new Vector2(leftColumnControl, leftYPos));
                alwaysShowHudBox.description = "If enabled, the HUD will always be visible and won't fade out";
                uiElements.Add(new OpLabel(leftColumnLabel, leftYPos + 2f, "Always HUD:"));
                uiElements.Add(alwaysShowHudBox);
                leftYPos -= lineHeight;

                // Camera Mode label (dropdown added later)
                float cameraModeYPos = leftYPos;
                uiElements.Add(new OpLabel(leftColumnLabel, cameraModeYPos, "Camera Mode:"));
                leftYPos -= lineHeight;

                // Rain Countdown Min
                OpUpdown rainCountdownMinBox = new OpUpdown(RainCountdownMin, new Vector2(leftColumnControl, leftYPos - 5f), 80f);
                rainCountdownMinBox.description = $"Minimum seconds for rain countdown before region change ({RainCountdownRangeMin}-{RainCountdownRangeMax})";
                uiElements.Add(new OpLabel(leftColumnLabel, leftYPos, "Rain Min (sec):"));
                uiElements.Add(rainCountdownMinBox);
                leftYPos -= lineHeight;

                // Rain Countdown Max
                OpUpdown rainCountdownMaxBox = new OpUpdown(RainCountdownMax, new Vector2(leftColumnControl, leftYPos - 5f), 80f);
                rainCountdownMaxBox.description = $"Maximum seconds for rain countdown before region change ({RainCountdownRangeMin}-{RainCountdownRangeMax})";
                uiElements.Add(new OpLabel(leftColumnLabel, leftYPos, "Rain Max (sec):"));
                uiElements.Add(rainCountdownMaxBox);

                // === RIGHT COLUMN ===
                float rightYPos = yPos;

                // Start Region Selection label (dropdown added later)
                float regionYPos = rightYPos;
                uiElements.Add(new OpLabel(rightColumnLabel, regionYPos, "Start Region:"));
                rightYPos -= lineHeight;

                // Transition Duration
                OpUpdown transitionDurationBox = new OpUpdown(TransitionDuration, new Vector2(rightColumnControl, rightYPos - 5f), 80f);
                transitionDurationBox.description = $"Duration in seconds for camera transitions between rooms ({TransitionDurationRangeMin}-{TransitionDurationRangeMax})";
                uiElements.Add(new OpLabel(rightColumnLabel, rightYPos, "Transition (sec):"));
                uiElements.Add(transitionDurationBox);
                rightYPos -= lineHeight;

                // HUD Fade Delay
                OpUpdown hudFadeDelayBox = new OpUpdown(HudFadeDelay, new Vector2(rightColumnControl, rightYPos - 5f), 80f);
                hudFadeDelayBox.description = $"Delay in seconds before HUD fades out ({HudFadeDelayRangeMin}-{HudFadeDelayRangeMax})";
                uiElements.Add(new OpLabel(rightColumnLabel, rightYPos, "HUD Fade (sec):"));
                uiElements.Add(hudFadeDelayBox);
                rightYPos -= lineHeight;

                // Watcher State label (dropdown added later)
                float rotStateYPos = rightYPos;
                uiElements.Add(new OpLabel(rightColumnLabel, rotStateYPos, "Watcher State:"));
                rightYPos -= lineHeight;

                // Enable Chaos
                OpCheckBox enableChaosBox = new OpCheckBox(EnableChaos, new Vector2(rightColumnControl, rightYPos));
                enableChaosBox.description = "If enabled, spawns creatures to create a more lively environment (may cause instability)";
                uiElements.Add(new OpLabel(rightColumnLabel, rightYPos + 2f, "Chaos Mode:"));
                uiElements.Add(enableChaosBox);
                rightYPos -= lineHeight;

                // Chaos Level
                OpUpdown chaosLevelBox = new OpUpdown(ChaosLevel, new Vector2(rightColumnControl, rightYPos - 5f), 80f);
                chaosLevelBox.description = "Chaos intensity: 1 = Controlled (50 max), 10 = ABSOLUTE CHAOS (250 max)";
                uiElements.Add(new OpLabel(rightColumnLabel, rightYPos, "Chaos Level:"));
                uiElements.Add(chaosLevelBox);
                rightYPos -= lineHeight;

                // Chaos Spawn All
                OpCheckBox chaosSpawnAllBox = new OpCheckBox(ChaosSpawnAll, new Vector2(rightColumnControl, rightYPos));
                chaosSpawnAllBox.description = "⚠️ EXPERIMENTAL: Spawn ALL creature types, ignoring blacklist (may crash!)";
                uiElements.Add(new OpLabel(rightColumnLabel, rightYPos + 2f, "Spawn All:"));
                uiElements.Add(chaosSpawnAllBox);
                rightYPos -= lineHeight;

                // No Rain Transition
                OpCheckBox noRainTransitionBox = new OpCheckBox(NoRainTransition, new Vector2(rightColumnControl, rightYPos));
                noRainTransitionBox.description = "Skip the rain countdown and transition to next region immediately at 95% cycle completion";
                uiElements.Add(new OpLabel(rightColumnLabel, rightYPos + 2f, "No Rain Wait:"));
                uiElements.Add(noRainTransitionBox);

                // === BOTTOM SECTION (full width) ===
                float bottomYPos = Mathf.Min(leftYPos, rightYPos) - lineHeight * 0.5f;

                // Control hints
                uiElements.Add(new OpLabel(leftColumnLabel, bottomYPos, "In-game controls: F1/Tab opens the live overlay.", bigText: false)
                {
                    color = new Color(0.7f, 0.85f, 1f, 0.85f)
                });

                // Add all non-dropdown elements first
                opTab.AddItems(uiElements.ToArray());

                // Create and add dropdowns LAST for proper z-ordering (render on top)
                var campaignDropdown = new OpComboBox(
                    SelectedCampaign,
                    new Vector2(leftColumnControl, campaignYPos - 5f),
                    140f,
                    OpResourceSelector.GetEnumNames(null, typeof(CampaignChoice)).Select(li =>
                    {
                        li.displayName = GetCampaignDisplayName(li.name);
                        return li;
                    }).ToList()
                ) { colorEdge = Menu.MenuColorEffect.rgbWhite };
                campaignDropdown.description = "Choose which slugcat campaign to explore";

                var regionDropdown = new OpComboBox(
                    StartRegion,
                    new Vector2(rightColumnControl, regionYPos - 5f),
                    140f,
                    WallpaperRegionCatalog.GetAllSelectableRegions()
                        .Select((regionCode, index) => new ListItem(regionCode, index)
                        {
                            displayName = GetRegionDisplayName(regionCode)
                        })
                        .ToList()
                ) { colorEdge = Menu.MenuColorEffect.rgbWhite };
                regionDropdown.description = "Choose a starting region available to at least one installed campaign";

                var cameraModeDropdown = new OpComboBox(
                    CameraModeConfig,
                    new Vector2(leftColumnControl, cameraModeYPos - 5f),
                    140f,
                    OpResourceSelector.GetEnumNames(null, typeof(CameraMode)).Select(li =>
                    {
                        li.displayName = GetCameraModeDisplayName(li.name);
                        return li;
                    }).ToList()
                ) { colorEdge = Menu.MenuColorEffect.rgbWhite };
                cameraModeDropdown.description = "How to select camera positions in large rooms";

                var rotStateDropdown = new OpComboBox(
                    RotStateConfig,
                    new Vector2(rightColumnControl, rotStateYPos - 5f),
                    140f,
                    OpResourceSelector.GetEnumNames(null, typeof(RotState)).Select(li =>
                    {
                        li.displayName = GetRotStateDisplayName(li.name);
                        return li;
                    }).ToList()
                ) { colorEdge = Menu.MenuColorEffect.rgbWhite };
                rotStateDropdown.description = "Watcher environmental state: Natural Mix, Clean, Rotted, Karma Flowers, or Rot + Karma";

                // Add dropdowns last to ensure they render on top
                opTab.AddItems(new UIelement[] { campaignDropdown, regionDropdown, cameraModeDropdown, rotStateDropdown });

                WallpaperMod.Log?.LogInfo("WallpaperModOptions: UI initialized successfully");
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"WallpaperModOptions: Failed to initialize UI - {ex}");
            }
        }

        private string GetCampaignDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "White": return "Survivor";
                case "Yellow": return "Monk";
                case "Red": return "Hunter";
                case "Gourmand": return "Gourmand (DP)";
                case "Artificer": return "Artificer (DP)";
                case "Rivulet": return "Rivulet (DP)";
                case "Spearmaster": return "Spearmaster (DP)";
                case "Saint": return "Saint (DP)";
                case "Watcher": return "Watcher";
                default: return enumName;
            }
        }

        private string GetRegionDisplayName(string enumName)
        {
            string displayName = WallpaperRegionCatalog.GetDisplayName(enumName, SelectedCampaign.Value);
            if (WallpaperRegionCatalog.WatcherRegions.Contains(enumName, StringComparer.OrdinalIgnoreCase))
            {
                return displayName + " (Watcher)";
            }

            if (WallpaperRegionCatalog.DownpourRegions.Contains(enumName, StringComparer.OrdinalIgnoreCase))
            {
                return displayName + " (DP)";
            }

            return displayName;
        }

        public override void Update()
        {
            base.Update();

            // Each numeric value is already clamped to its own range by the configurable (see the constructor).
            // The rain countdown bounds also depend on each other, which a per-value range cannot express.

            // Ensure min <= max
            if (RainCountdownMin.Value > RainCountdownMax.Value)
                RainCountdownMin.Value = RainCountdownMax.Value;

            string inferredCampaign = WallpaperRegionCatalog.InferCampaignForRegion(StartRegion.Value, SelectedCampaign.Value);
            if (!string.Equals(SelectedCampaign.Value, inferredCampaign, StringComparison.OrdinalIgnoreCase))
                SelectedCampaign.Value = inferredCampaign;

            if (!string.Equals(SelectedCampaign.Value, CampaignChoice.Watcher.ToString(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(RotStateConfig.Value, RotState.Natural.ToString(), StringComparison.OrdinalIgnoreCase))
                RotStateConfig.Value = RotState.Natural.ToString();

            string normalizedRegion = WallpaperRegionCatalog.NormalizeRegionForCampaign(StartRegion.Value, SelectedCampaign.Value);
            if (!string.Equals(StartRegion.Value, normalizedRegion, StringComparison.OrdinalIgnoreCase))
                StartRegion.Value = normalizedRegion;
        }

        // Helper method to convert campaign string to SlugcatStats.Name
        public static SlugcatStats.Name GetSlugcatName(string campaignStr)
        {
            if (!Enum.TryParse<CampaignChoice>(campaignStr, out var campaign))
            {
                campaign = CampaignChoice.White;
            }

            switch (campaign)
            {
                case CampaignChoice.White: return SlugcatStats.Name.White;
                case CampaignChoice.Yellow: return SlugcatStats.Name.Yellow;
                case CampaignChoice.Red: return SlugcatStats.Name.Red;
                // Downpour and Watcher slugcats are guarded so Workshop installs without those DLCs still load.
                case CampaignChoice.Gourmand: return ModCompatibility.IsDownpourEnabled ? new SlugcatStats.Name("Gourmand", false) : SlugcatStats.Name.White;
                case CampaignChoice.Artificer: return ModCompatibility.IsDownpourEnabled ? new SlugcatStats.Name("Artificer", false) : SlugcatStats.Name.White;
                case CampaignChoice.Rivulet: return ModCompatibility.IsDownpourEnabled ? new SlugcatStats.Name("Rivulet", false) : SlugcatStats.Name.White;
                case CampaignChoice.Spearmaster: return ModCompatibility.IsDownpourEnabled ? new SlugcatStats.Name("Spearmaster", false) : SlugcatStats.Name.White;
                case CampaignChoice.Saint: return ModCompatibility.IsDownpourEnabled ? new SlugcatStats.Name("Saint", false) : SlugcatStats.Name.White;
                case CampaignChoice.Watcher: return ModCompatibility.IsWatcherEnabled ? new SlugcatStats.Name("Watcher", false) : SlugcatStats.Name.White;
                default: return SlugcatStats.Name.White;
            }
        }

        // Helper method to get region code string
        public static string GetRegionCode(string regionStr)
        {
            if (string.IsNullOrWhiteSpace(regionStr))
            {
                return "SU";
            }

            string normalized = regionStr.Trim().ToUpperInvariant();
            return WallpaperRegionCatalog.IsKnownRegion(normalized) ? normalized : "SU";
        }

        // Helper method to convert camera mode string to enum
        public static CameraMode GetCameraMode(string modeStr)
        {
            if (Enum.TryParse<CameraMode>(modeStr, out var mode))
            {
                return mode;
            }
            return CameraMode.RandomExploration;
        }

        private string GetCameraModeDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "RandomExploration": return "Random Exploration";
                case "Random": return "Single Random";
                case "Sequential": return "All Positions";
                case "FirstOnly": return "First Only";
                default: return enumName;
            }
        }

        // Helper method to convert rot state string to enum
        public static WallpaperModOptions.RotState GetRotState(string stateStr)
        {
            if (Enum.TryParse<WallpaperModOptions.RotState>(stateStr, out var state))
            {
                return state;
            }
            return WallpaperModOptions.RotState.Natural;
        }

        private string GetRotStateDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "Natural": return "Natural Mix";
                case "Normal": return "Clean";
                case "Rotted": return "Rotted (Rot Spread)";
                case "Karma": return "Karma Flowers";
                case "RottedKarma": return "Rot + Karma Flowers";
                default: return enumName;
            }
        }
    }
}
