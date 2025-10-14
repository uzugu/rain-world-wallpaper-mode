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
            Saint       // Downpour
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
            LM,  // Looks to the Moon (Downpour)
            RM,  // Waterfront Facility (Downpour)
            DM,  // Metropolis (Downpour)
            LC,  // Outer Expanse (Downpour)
            MS,  // Submerged Superstructure (Downpour)
            VS,  // Pipeyard (Downpour)
            CL,  // The Rot (Downpour)
            OE   // Rubicon (Downpour)
        }

        // Configuration values
        public readonly Configurable<float> RegionDuration;
        public readonly Configurable<float> TransitionDuration;
        public readonly Configurable<float> StayDuration;
        public readonly Configurable<float> HudFadeDelay;
        public readonly Configurable<bool> AlwaysShowHud;
        public readonly Configurable<string> StartRegion;
        public readonly Configurable<string> SelectedCampaign;

        public WallpaperModOptions()
        {
            // Bind configuration values with defaults (using strings for enums to work with OpResourceSelector)
            RegionDuration = config.Bind("regionDuration", 300f);
            TransitionDuration = config.Bind("transitionDuration", 5f);
            StayDuration = config.Bind("stayDuration", 15f);
            HudFadeDelay = config.Bind("hudFadeDelay", 3f);
            AlwaysShowHud = config.Bind("alwaysShowHud", true);
            StartRegion = config.Bind("startRegion", RegionChoice.SU.ToString());
            SelectedCampaign = config.Bind("selectedCampaign", CampaignChoice.White.ToString());
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

                // Region Duration
                OpTextBox regionDurationBox = new OpTextBox(RegionDuration, new Vector2(leftColumnControl, leftYPos - 5f), 80f);
                regionDurationBox.description = "Duration in seconds to spend in each region (60-1800)";
                uiElements.Add(new OpLabel(leftColumnLabel, leftYPos, "Region (sec):"));
                uiElements.Add(regionDurationBox);
                leftYPos -= lineHeight;

                // Room Stay Duration
                OpTextBox stayDurationBox = new OpTextBox(StayDuration, new Vector2(leftColumnControl, leftYPos - 5f), 80f);
                stayDurationBox.description = "Duration in seconds to stay in each room before transitioning (5-60)";
                uiElements.Add(new OpLabel(leftColumnLabel, leftYPos, "Room (sec):"));
                uiElements.Add(stayDurationBox);
                leftYPos -= lineHeight;

                // Always Show HUD
                OpCheckBox alwaysShowHudBox = new OpCheckBox(AlwaysShowHud, new Vector2(leftColumnControl, leftYPos));
                alwaysShowHudBox.description = "If enabled, the HUD will always be visible and won't fade out";
                uiElements.Add(new OpLabel(leftColumnLabel, leftYPos + 2f, "Show HUD:"));
                uiElements.Add(alwaysShowHudBox);

                // === RIGHT COLUMN ===
                float rightYPos = yPos;

                // Start Region Selection label (dropdown added later)
                float regionYPos = rightYPos;
                uiElements.Add(new OpLabel(rightColumnLabel, regionYPos, "Start Region:"));
                rightYPos -= lineHeight;

                // Transition Duration
                OpTextBox transitionDurationBox = new OpTextBox(TransitionDuration, new Vector2(rightColumnControl, rightYPos - 5f), 80f);
                transitionDurationBox.description = "Duration in seconds for camera transitions between rooms (1-15)";
                uiElements.Add(new OpLabel(rightColumnLabel, rightYPos, "Transition (sec):"));
                uiElements.Add(transitionDurationBox);
                rightYPos -= lineHeight;

                // HUD Fade Delay
                OpTextBox hudFadeDelayBox = new OpTextBox(HudFadeDelay, new Vector2(rightColumnControl, rightYPos - 5f), 80f);
                hudFadeDelayBox.description = "Delay in seconds before HUD fades out (1-10)";
                uiElements.Add(new OpLabel(rightColumnLabel, rightYPos, "HUD Fade (sec):"));
                uiElements.Add(hudFadeDelayBox);

                // === BOTTOM SECTION (full width) ===
                float bottomYPos = Mathf.Min(leftYPos, rightYPos) - lineHeight * 0.5f;

                // Control hints
                uiElements.Add(new OpLabel(leftColumnLabel, bottomYPos, "In-Game Controls:", bigText: false));
                bottomYPos -= 30f;

                uiElements.Add(new OpLabelLong(new Vector2(leftColumnLabel, bottomYPos - 100f), new Vector2(500f, 100f),
                    "N - Next Room | G - Next Region | B - Previous Region\n" +
                    "+/- or PgUp/PgDn - Adjust Region Duration\n" +
                    "H - Toggle HUD Always Visible\n" +
                    "F1/Tab - Settings Overlay | Escape - Return to Menu")
                {
                    verticalAlignment = OpLabel.LabelVAlignment.Top,
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
                    OpResourceSelector.GetEnumNames(null, typeof(RegionChoice)).Select(li =>
                    {
                        li.displayName = GetRegionDisplayName(li.name);
                        return li;
                    }).ToList()
                ) { colorEdge = Menu.MenuColorEffect.rgbWhite };
                regionDropdown.description = "Choose starting region";

                // Add dropdowns last to ensure they render on top
                opTab.AddItems(new UIelement[] { campaignDropdown, regionDropdown });

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
                default: return enumName;
            }
        }

        private string GetRegionDisplayName(string enumName)
        {
            switch (enumName)
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
                case "LM": return "Looks to the Moon (DP)";
                case "RM": return "Waterfront Facility (DP)";
                case "DM": return "Metropolis (DP)";
                case "LC": return "Outer Expanse (DP)";
                case "MS": return "Submerged Superstructure (DP)";
                case "VS": return "Pipeyard (DP)";
                case "CL": return "The Rot (DP)";
                case "OE": return "Rubicon (DP)";
                default: return enumName;
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
                // Downpour slugcats - these require ModManager.MSC check
                case CampaignChoice.Gourmand: return new SlugcatStats.Name("Gourmand", false);
                case CampaignChoice.Artificer: return new SlugcatStats.Name("Artificer", false);
                case CampaignChoice.Rivulet: return new SlugcatStats.Name("Rivulet", false);
                case CampaignChoice.Spearmaster: return new SlugcatStats.Name("Spearmaster", false);
                case CampaignChoice.Saint: return new SlugcatStats.Name("Saint", false);
                default: return SlugcatStats.Name.White;
            }
        }

        // Helper method to get region code string
        public static string GetRegionCode(string regionStr)
        {
            // Already a string, just validate and return
            if (Enum.TryParse<RegionChoice>(regionStr, out var region))
            {
                return region.ToString();
            }
            return "SU"; // Default to Outskirts
        }
    }
}
