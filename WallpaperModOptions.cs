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

        public enum MenuStyle
        {
            Panel,      // WallpaperSettingsMenu: a framed panel with arrow buttons and checkboxes (default)
            Minimal     // WallpaperMinimalMenu: a text-only column over a soft shade
        }

        // Allowed ranges for the numeric settings (seconds unless noted). Remix enforces them through the
        // configurables' acceptable ranges, and WallpaperSettingsMenu reuses them for its runtime changes
        // so the two cannot drift apart.
        public const float TransitionDurationRangeMin = 1f;
        public const float TransitionDurationRangeMax = 15f;
        public const float StayDurationRangeMin = 5f;
        public const float StayDurationRangeMax = 60f;
        public const float HudFadeDelayRangeMin = 1f;
        public const float HudFadeDelayRangeMax = 10f;
        public const int ChaosLevelRangeMin = 1;
        public const int ChaosLevelRangeMax = 10;
        public const int RainCountdownMinRangeMin = 10;
        public const int RainCountdownMinRangeMax = 300;
        public const int RainCountdownMaxRangeMin = 30;
        public const int RainCountdownMaxRangeMax = 600;

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
        public readonly Configurable<string> MenuStyleConfig;
        public readonly Configurable<bool> GameSound;

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
            RainCountdownMin = config.Bind("rainCountdownMin", 60, new ConfigAcceptableRange<int>(RainCountdownMinRangeMin, RainCountdownMinRangeMax));
            RainCountdownMax = config.Bind("rainCountdownMax", 180, new ConfigAcceptableRange<int>(RainCountdownMaxRangeMin, RainCountdownMaxRangeMax));
            RotStateConfig = config.Bind("rotState", RotState.Natural.ToString());
            MenuStyleConfig = config.Bind("menuStyle", MenuStyle.Panel.ToString());
            GameSound = config.Bind("gameSound", true);
        }

        // Layout of the Remix page. Remix gives every tab a 600 x 600 canvas with (0, 0) at its bottom left corner.
        private const float CanvasSize = 600f;
        private const float PageMargin = 10f;
        private const float ColumnGap = 10f;
        private const float CardGap = 8f;
        private const float CardWidth = (CanvasSize - 2f * PageMargin - ColumnGap) / 2f;
        private const float CardsTop = 520f;

        public override void Initialize()
        {
            base.Initialize();

            try
            {
                var opTab = new OpTab(this, "Settings");
                Tabs = new[] { opTab };

                var page = new RemixPage();
                float leftColumn = PageMargin;
                float rightColumn = PageMargin + CardWidth + ColumnGap;

                page.AddCenteredText("WALLPAPER MODE", 560f, 30f, bigText: true, RemixPage.White);
                OpLabel hint = page.AddCenteredText("Hover over a setting to see what it does.", 532f, 20f, bigText: false, RemixPage.DarkGrey);

                // Top right, clear of the centred title: one click adds this wallpaper to Wallpaper Engine's library. The
                // hint line under the title reports how it went.
                page.AddButton("WALLPAPER ENGINE", new Vector2(CanvasSize - PageMargin - 150f, 558f), new Vector2(150f, 26f),
                    "Add \"Rain World Wallpaper Mode\" to Wallpaper Engine, so you can pick it there as your desktop wallpaper. " +
                    "Wallpaper Engine then starts the game with these settings. Restart Wallpaper Engine if it does not show up.",
                    () =>
                    {
                        bool added = WallpaperEngineSetup.TryAddProject(out string message);
                        hint.text = message.Length > 88 ? message.Substring(0, 85) + "..." : message;
                        hint.color = added ? RemixPage.White : RemixPage.ErrorRed;
                    });

                // The cards follow the sections of the in-game settings menu; TIMING holds the durations that only Remix
                // sets, and Skip the rain sits with the travel choices because it decides when the tour moves on. Both
                // columns hold eight rows so they end at the same height, and the controls card spans the page below.
                float top = CardsTop;
                page.BeginCard("TRAVEL", leftColumn, top, CardWidth, RemixPage.CardHeight(5));
                page.AddDropdown("Campaign", SelectedCampaign, GetEnumItems(typeof(CampaignChoice), GetCampaignDisplayName),
                    "Which slugcat campaign the wallpaper tours. It decides which regions are visited and what they are called.");
                page.AddDropdown("Start region", StartRegion, GetRegionItems(),
                    "The region the wallpaper starts in. A Watcher region switches to the Watcher campaign; a region the campaign cannot reach falls back to its usual start.");
                page.AddDropdown("Camera", CameraModeConfig, GetEnumItems(typeof(CameraMode), GetCameraModeDisplayName),
                    "How the camera picks views in rooms with several screens: a random tour, one random view, every view in order, or the first view only.");
                page.AddDropdown("Watcher world", RotStateConfig, GetEnumItems(typeof(RotState), GetRotStateDisplayName),
                    "Watcher campaign only: the state of the world, from a natural mix to clean, rotted, karma flowers, or rot and flowers together.");
                page.AddCheckBox("Skip the rain", NoRainTransition,
                    "Change region at 95% of the cycle instead of waiting for the rain.");

                top -= RemixPage.CardHeight(5) + CardGap;
                page.BeginCard("CREATURES", leftColumn, top, CardWidth, RemixPage.CardHeight(3));
                page.AddCheckBox("Chaos creatures", EnableChaos,
                    "Spawn extra creatures into the rooms the camera visits.");
                page.AddNumber("Chaos level", ChaosLevel,
                    $"How many creatures appear and how quickly ({ChaosLevelRangeMin}-{ChaosLevelRangeMax}): level 1 keeps up to 5 at once, level 10 up to 150. Above 7 the game may lag or crash.");
                page.AddCheckBox("Every creature (experimental)", ChaosSpawnAll,
                    "Experimental: also spawn creature types that are normally left out. May crash the game.");

                top = CardsTop;
                page.BeginCard("TIMING", rightColumn, top, CardWidth, RemixPage.CardHeight(4));
                page.AddNumber("Room stay (sec)", StayDuration,
                    $"How long the camera stays in each room before moving on ({StayDurationRangeMin}-{StayDurationRangeMax} seconds).");
                page.AddNumber("Transition (sec)", TransitionDuration,
                    $"How long the move from one room to the next takes ({TransitionDurationRangeMin}-{TransitionDurationRangeMax} seconds).");
                page.AddNumber("Rain time min (sec)", RainCountdownMin,
                    $"When a cycle ends, the rain plays for a random time between the two rain times, then the region changes. Shortest time ({RainCountdownMinRangeMin}-{RainCountdownMinRangeMax} seconds).");
                page.AddNumber("Rain time max (sec)", RainCountdownMax,
                    $"Longest time the rain plays before the region changes ({RainCountdownMaxRangeMin}-{RainCountdownMaxRangeMax} seconds).");

                top -= RemixPage.CardHeight(4) + CardGap;
                page.BeginCard("DISPLAY", rightColumn, top, CardWidth, RemixPage.CardHeight(4));
                page.AddCheckBox("Always show HUD", AlwaysShowHud,
                    "Keep the HUD on screen instead of fading it out. H switches this in-game.");
                page.AddNumber("HUD fade (sec)", HudFadeDelay,
                    $"How long the HUD stays after the last input before it fades out ({HudFadeDelayRangeMin}-{HudFadeDelayRangeMax} seconds).");
                page.AddDropdown("Menu style", MenuStyleConfig, GetEnumItems(typeof(MenuStyle), GetMenuStyleDisplayName),
                    "Look of the in-game settings menu (F1 / Tab): Panel, a framed panel with arrows and checkboxes, or Minimal, a plain text list.");
                page.AddCheckBox("Game sound", GameSound,
                    "Play the game's music and sounds in wallpaper mode. Off keeps the wallpaper silent; the game's own audio settings are not changed.");

                // The keys that work while the wallpaper runs, as two tables side by side.
                top -= RemixPage.CardHeight(4) + CardGap;
                float controlsWidth = CanvasSize - 2f * PageMargin;
                page.BeginCard("CONTROLS", leftColumn, top, controlsWidth, top - PageMargin, RemixPage.KeyRowPitch);
                float tableWidth = (controlsWidth - 2f * RemixPage.CardPadding) / 2f;
                page.AddKeyTable(leftColumn + RemixPage.CardPadding, new[]
                {
                    new[] { "", "Keyboard", "Controller" },
                    new[] { "Settings menu", "F1 / Tab", "Start" },
                    new[] { "Change room", "Left / Right", "Stick" },
                    new[] { "Camera position", "Up / Down", "Stick" },
                    new[] { "Next room", "N", "A" },
                });
                page.AddKeyTable(leftColumn + RemixPage.CardPadding + tableWidth, new[]
                {
                    new[] { "", "Keyboard", "Controller" },
                    new[] { "Change region", "B / G", "LB / RB" },
                    new[] { "Lock the room", "L", "X" },
                    new[] { "HUD always on", "H", "Y" },
                    new[] { "Main menu", "Esc", "Back" },
                });

                page.AddTo(opTab);
                WallpaperMod.Log?.LogInfo("WallpaperModOptions: UI initialized successfully");
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"WallpaperModOptions: Failed to initialize UI - {ex}");
            }
        }

        private static List<ListItem> GetEnumItems(Type enumType, Func<string, string> displayName)
        {
            return OpResourceSelector.GetEnumNames(null, enumType).Select(item =>
            {
                item.displayName = displayName(item.name);
                return item;
            }).ToList();
        }

        private List<ListItem> GetRegionItems()
        {
            return WallpaperRegionCatalog.GetAllSelectableRegions()
                .Select((regionCode, index) => new ListItem(regionCode, index)
                {
                    displayName = GetRegionDisplayName(regionCode)
                })
                .ToList();
        }

        /// <summary>
        /// Lays out the Remix page as cards: a header, then one setting per row, with the label on the left and the
        /// control against the right edge of the card. Elements are collected in three layers and added to the tab in
        /// that order, because Remix draws later items on top: card backgrounds, then labels and simple controls, then
        /// dropdowns.
        /// </summary>
        private sealed class RemixPage
        {
            public const float CardPadding = 12f;
            public const float KeyRowPitch = 26f;
            private const float RowPitch = 32f;
            private const float CardHeaderHeight = 30f;
            private const float CardBottomPadding = 6f;
            private const float LabelGap = 8f;
            private const float DropdownWidth = 150f;
            private const float NumberWidth = 80f;
            private const float CheckBoxSize = 24f;
            private const float KeyActionWidth = 108f;
            private const float KeyColumnWidth = 92f;
            private const float KeyButtonWidth = 70f;

            private readonly List<UIelement> backgrounds = new List<UIelement>();
            private readonly List<UIelement> items = new List<UIelement>();
            private readonly List<UIelement> dropdowns = new List<UIelement>();

            private float contentLeft;
            private float contentRight;
            private float rowPitch;
            private float firstRowCenter;
            private float nextRowCenter;

            public static Color White => global::Menu.MenuColorEffect.rgbWhite;
            public static Color MediumGrey => global::Menu.MenuColorEffect.rgbMediumGrey;
            public static Color DarkGrey => global::Menu.MenuColorEffect.rgbDarkGrey;
            public static Color ErrorRed => new Color(0.95f, 0.45f, 0.4f);

            /// <summary>
            /// Height of a card that holds the given number of setting rows.
            /// </summary>
            public static float CardHeight(int rows)
            {
                return CardHeaderHeight + rows * RowPitch + CardBottomPadding;
            }

            public OpLabel AddCenteredText(string text, float bottom, float height, bool bigText, Color color)
            {
                var label = new OpLabel(new Vector2(PageMargin, bottom), new Vector2(CanvasSize - 2f * PageMargin, height), text, FLabelAlignment.Center, bigText)
                {
                    color = color
                };
                items.Add(label);
                return label;
            }

            public void AddButton(string text, Vector2 pos, Vector2 size, string description, Action onClick)
            {
                var button = new OpSimpleButton(pos, size, text)
                {
                    description = description
                };
                button.OnClick += _ => onClick();
                items.Add(button);
            }

            /// <summary>
            /// Starts a card whose top left corner is at (left, top). The rows added next fill it from under its header.
            /// </summary>
            public void BeginCard(string title, float left, float top, float width, float height, float pitch = RowPitch)
            {
                backgrounds.Add(new OpRect(new Vector2(left, top - height), new Vector2(width, height)));

                contentLeft = left + CardPadding;
                contentRight = left + width - CardPadding;
                rowPitch = pitch;
                firstRowCenter = top - CardHeaderHeight - pitch / 2f;
                nextRowCenter = firstRowCenter;

                AddText(title, contentLeft, top - CardHeaderHeight / 2f, contentRight - contentLeft, White);
            }

            public void AddDropdown(string label, Configurable<string> config, List<ListItem> choices, string description)
            {
                float centerY = TakeRow(label, DropdownWidth);
                var dropdown = new OpComboBox(config, new Vector2(contentRight - DropdownWidth, centerY - 12f), DropdownWidth, choices)
                {
                    description = description
                };

                // Remix draws an open list inside the dropdown's own container, under anything added after it, so the
                // dropdown moves itself to the front whenever its list opens over the rows below.
                dropdown.OnListOpen += _ => dropdown.MoveToFront();
                dropdowns.Add(dropdown);
            }

            // Numbers use OpUpdown: it takes its bounds from the configurable's acceptable range, while OpTextBox accepts
            // any number and only gets clamped when the config is saved.
            public void AddNumber(string label, Configurable<float> config, string description)
            {
                float centerY = TakeRow(label, NumberWidth);
                items.Add(new OpUpdown(config, new Vector2(contentRight - NumberWidth, centerY - 15f), NumberWidth)
                {
                    description = description
                });
            }

            public void AddNumber(string label, Configurable<int> config, string description)
            {
                float centerY = TakeRow(label, NumberWidth);
                items.Add(new OpUpdown(config, new Vector2(contentRight - NumberWidth, centerY - 15f), NumberWidth)
                {
                    description = description
                });
            }

            public void AddCheckBox(string label, Configurable<bool> config, string description)
            {
                float centerY = TakeRow(label, CheckBoxSize);
                items.Add(new OpCheckBox(config, new Vector2(contentRight - CheckBoxSize, centerY - CheckBoxSize / 2f))
                {
                    description = description
                });
            }

            /// <summary>
            /// Writes a table of controls from the card's first row down. Each row is { what it does, keyboard keys,
            /// controller buttons }, and the first row holds the column headings.
            /// </summary>
            public void AddKeyTable(float left, string[][] rows)
            {
                float centerY = firstRowCenter;
                for (int i = 0; i < rows.Length; i++)
                {
                    bool heading = i == 0;
                    AddText(rows[i][0], left, centerY, KeyActionWidth, heading ? DarkGrey : MediumGrey);
                    AddText(rows[i][1], left + KeyActionWidth, centerY, KeyColumnWidth, heading ? DarkGrey : White);
                    AddText(rows[i][2], left + KeyActionWidth + KeyColumnWidth, centerY, KeyButtonWidth, heading ? DarkGrey : White);
                    centerY -= rowPitch;
                }
            }

            public void AddTo(OpTab tab)
            {
                tab.AddItems(backgrounds.ToArray());
                tab.AddItems(items.ToArray());
                tab.AddItems(dropdowns.ToArray());
            }

            // Takes the next row of the card and writes its label, leaving room for a control of the given width at the
            // right edge. Returns the row's vertical centre.
            private float TakeRow(string label, float controlWidth)
            {
                float centerY = TakeRow();
                AddText(label, contentLeft, centerY, contentRight - controlWidth - LabelGap - contentLeft, MediumGrey);
                return centerY;
            }

            private float TakeRow()
            {
                float centerY = nextRowCenter;
                nextRowCenter -= rowPitch;
                return centerY;
            }

            private void AddText(string text, float left, float centerY, float width, Color color)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                items.Add(new OpLabel(new Vector2(left, centerY - 10f), new Vector2(width, 20f), text, FLabelAlignment.Left)
                {
                    color = color
                });
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

        /// <summary>
        /// Reads a menu style by name ("Panel" or "Minimal", in any case). Unlike Enum.TryParse it refuses numbers,
        /// which would otherwise turn into styles that do not exist.
        /// </summary>
        public static bool TryParseMenuStyle(string styleStr, out MenuStyle style)
        {
            style = MenuStyle.Panel;
            if (string.IsNullOrWhiteSpace(styleStr))
            {
                return false;
            }

            string trimmed = styleStr.Trim();
            foreach (MenuStyle candidate in (MenuStyle[])Enum.GetValues(typeof(MenuStyle)))
            {
                if (string.Equals(candidate.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    style = candidate;
                    return true;
                }
            }

            return false;
        }

        // Helper method to convert menu style string to enum; anything unreadable means the panel
        public static MenuStyle GetMenuStyle(string styleStr)
        {
            return TryParseMenuStyle(styleStr, out MenuStyle style) ? style : MenuStyle.Panel;
        }

        private string GetMenuStyleDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "Panel": return "Panel";
                case "Minimal": return "Minimal";
                default: return enumName;
            }
        }
    }
}
