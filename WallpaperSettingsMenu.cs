using System;
using System.Collections.Generic;
using System.Linq;
using Menu;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// The wallpaper settings and quick-travel panel. It is built from the game's own menu objects, so it looks and
    /// behaves like the rest of Rain World's UI: the mouse, the arrow keys and controllers all work. It runs as a side
    /// process next to the wallpaper game, so nothing pauses while it is open.
    /// </summary>
    internal sealed class WallpaperSettingsMenu : Menu.Menu, CheckBox.IOwnCheckBox
    {
        // The ID only labels the menu for the game. It is registered once, here, so the ExtEnum entry stays unique.
        private static readonly ProcessManager.ProcessID SettingsProcessID = new ProcessManager.ProcessID("WallpaperSettings", register: true);

        // Layout, in the game's 1366x768 menu space with x measured from the left edge of the visible screen
        // (screenLeft converts it, because Menu.Init shifts the page to centre that space on narrower screens).
        private const float PanelLeft = 60f;
        private const float PanelBottom = 36f;
        private const float PanelWidth = 500f;
        private const float PanelHeight = 702f;
        private const float PanelPadding = 24f;
        private const float PanelTop = PanelBottom + PanelHeight;
        private const float ContentLeft = PanelLeft + PanelPadding;
        private const float ContentRight = PanelLeft + PanelWidth - PanelPadding;
        private const float ControlColumnLeft = 236f;
        private const float RowPitch = 34f;
        private const float ArrowWidth = 34f;
        private const float ArrowHeight = 26f;
        private const float CheckBoxSize = 24f;
        private const float ButtonWidth = 110f;
        private const float ButtonHeight = 30f;
        private const float PanelFillAlpha = 0.85f;
        private const float BackdropAlpha = 0.3f;

        // Signals sent by the arrow buttons and the two panel buttons.
        private const string SignalCampaignPrev = "CAMPAIGN_PREV";
        private const string SignalCampaignNext = "CAMPAIGN_NEXT";
        private const string SignalRegionPrev = "REGION_PREV";
        private const string SignalRegionNext = "REGION_NEXT";
        private const string SignalRoomPrev = "ROOM_PREV";
        private const string SignalRoomNext = "ROOM_NEXT";
        private const string SignalCameraPrev = "CAMERA_PREV";
        private const string SignalCameraNext = "CAMERA_NEXT";
        private const string SignalRotStatePrev = "ROTSTATE_PREV";
        private const string SignalRotStateNext = "ROTSTATE_NEXT";
        private const string SignalChaosLevelPrev = "CHAOSLEVEL_PREV";
        private const string SignalChaosLevelNext = "CHAOSLEVEL_NEXT";
        private const string SignalTravel = "TRAVEL";
        private const string SignalClose = "CLOSE";

        // IDStrings of the checkboxes.
        private const string IdLockRoom = "LOCKROOM";
        private const string IdChaos = "CHAOS";
        private const string IdChaosSpawnAll = "CHAOSSPAWNALL";
        private const string IdAlwaysShowHud = "ALWAYSSHOWHUD";
        private const string IdNoRainWait = "NORAINWAIT";
        private const string IdGameSound = "GAMESOUND";

        private sealed class CyclerRow
        {
            public MenuLabel Name;
            public SymbolButton Prev;
            public SymbolButton Next;
            public MenuLabel Value;
            public float ValueWidth;
        }

        // A thin line under a section header. Raw sprites have to follow the page, which Menu.Init shifts, so the
        // line is a menu object that positions its sprite every frame (the way the game's settings interfaces do).
        private sealed class PanelDivider : PositionedMenuObject
        {
            private readonly FSprite line;

            public PanelDivider(Menu.Menu menu, MenuObject owner, Vector2 pos, float width)
                : base(menu, owner, pos)
            {
                line = new FSprite("pixel")
                {
                    anchorX = 0f,
                    scaleX = width,
                    scaleY = 2f,
                    color = MenuRGB(MenuColors.VeryDarkGrey)
                };
                Container.AddChild(line);
            }

            public override void GrafUpdate(float timeStacker)
            {
                base.GrafUpdate(timeStacker);
                line.x = DrawX(timeStacker);
                line.y = DrawY(timeStacker);
            }

            public override void RemoveSprites()
            {
                line.RemoveFromContainer();
                base.RemoveSprites();
            }
        }

        private readonly WallpaperController controller;
        private readonly Action closeRequested;
        private readonly float screenLeft;

        // Filled while the controls are built. These start empty (field initialisers run before the base
        // constructor), because the base constructor already calls UpdateInfoText.
        private readonly Dictionary<MenuObject, string> descriptions = new Dictionary<MenuObject, string>();
        private readonly List<MenuObject[]> selectableRows = new List<MenuObject[]>();

        private FSprite backdrop;
        private MenuLabel statusLabel;
        private CyclerRow campaignRow;
        private CyclerRow regionRow;
        private CyclerRow roomRow;
        private CyclerRow cameraRow;
        private CyclerRow rotStateRow;
        private CyclerRow chaosLevelRow;
        private CheckBox alwaysShowHudBox;

        // Quick travel selection: lists with the index the player is currently looking at.
        private List<string> availableCampaigns;
        private List<string> availableRegions;
        private List<string> availableCameraModes;
        private List<string> availableRotStates;
        private List<string> availableRooms;
        private int selectedCampaignIndex;
        private int selectedRegionIndex;
        private int selectedCameraModeIndex;
        private int selectedRotStateIndex;
        private int selectedRoomIndex;

        private bool roomRowEnabled;
        private bool rotStateRowEnabled;
        private bool closeQueued;
        private bool closeRaised;
        private bool isShutDown;

        public WallpaperSettingsMenu(ProcessManager manager, WallpaperController controller, Action closeRequested)
            : base(manager, SettingsProcessID)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.closeRequested = closeRequested;
            screenLeft = HorizontalMoveToGetCentered(manager);

            try
            {
                pages.Add(new Page(this, null, "main", 0));

                InitializeSelectionLists();
                BuildBackdrop();
                BuildPanel();
                BuildControls();
                LinkSelectables();
                RefreshValueLabels();
                RefreshStatus();
            }
            catch
            {
                // Nothing else owns a half-built menu, so take its containers off the stage before the error moves on.
                ShutDownProcess();
                throw;
            }
        }

        #region Building the panel

        private void BuildBackdrop()
        {
            // A raw sprite is not moved by Menu.Init, so it only has to be larger than the screen.
            backdrop = new FSprite("pixel")
            {
                color = MenuRGB(MenuColors.Black),
                anchorX = 0f,
                anchorY = 0f,
                x = -1f,
                y = -1f,
                scaleX = manager.rainWorld.screenSize.x + 2f,
                scaleY = manager.rainWorld.screenSize.y + 2f,
                alpha = BackdropAlpha
            };
            pages[0].Container.AddChild(backdrop);
        }

        private void BuildPanel()
        {
            RoundedRect panel = new RoundedRect(this, pages[0], new Vector2(PanelLeft + screenLeft, PanelBottom), new Vector2(PanelWidth, PanelHeight), filled: true);
            panel.fillAlpha = PanelFillAlpha;
            panel.borderColor = MenuColor(MenuColors.MediumGrey);
            pages[0].subObjects.Add(panel);
        }

        private void BuildControls()
        {
            AddCenteredLabel(Translate("WALLPAPER MODE"), PanelTop - 32f, bigText: true);
            statusLabel = AddCenteredLabel(string.Empty, PanelTop - 64f, bigText: false);
            statusLabel.label.color = MenuRGB(MenuColors.MediumGrey);

            float y = PanelTop - 100f;
            AddSectionHeader("TRAVEL", y);
            y -= 36f;
            campaignRow = AddCyclerRow("Campaign", SignalCampaignPrev, SignalCampaignNext, y,
                "Choose which slugcat campaign the wallpaper tours. Applied with TRAVEL.");
            y -= RowPitch;
            regionRow = AddCyclerRow("Region", SignalRegionPrev, SignalRegionNext, y,
                "Choose the region to travel to. Applied with TRAVEL.");
            y -= RowPitch;
            roomRow = AddCyclerRow("Room", SignalRoomPrev, SignalRoomNext, y,
                "Choose a room to jump to in the current region. Applied with TRAVEL.");
            y -= RowPitch;
            cameraRow = AddCyclerRow("Camera", SignalCameraPrev, SignalCameraNext, y,
                "Choose how camera positions are picked inside a room. Applied with TRAVEL.");
            y -= RowPitch;
            rotStateRow = AddCyclerRow("Watcher world", SignalRotStatePrev, SignalRotStateNext, y,
                "Choose the Watcher world state: natural mix, clean, rotted, karma flowers or both. Applies right away.");
            y -= RowPitch;
            AddCheckBox("Lock room", IdLockRoom, y,
                "Stay in this room and hold off the region change until it is unlocked.");

            y -= 40f;
            AddSectionHeader("CREATURES", y);
            y -= 36f;
            AddCheckBox("Chaos creatures", IdChaos, y,
                "Spawn extra creatures into the rooms you visit.");
            y -= RowPitch;
            chaosLevelRow = AddCyclerRow("Chaos level", SignalChaosLevelPrev, SignalChaosLevelNext, y,
                "How often creatures appear and how many can be alive at once.");
            y -= RowPitch;
            AddCheckBox("Spawn every creature (experimental)", IdChaosSpawnAll, y,
                "Experimental: also spawns creature types that are normally left out.");
            y -= 28f;
            MenuLabel note = AddLeftLabel(Translate("Creature changes apply in the next region"), y);
            note.label.color = MenuRGB(MenuColors.DarkGrey);

            y -= 36f;
            AddSectionHeader("DISPLAY", y);
            y -= 36f;
            alwaysShowHudBox = AddCheckBox("Always show HUD", IdAlwaysShowHud, y,
                "Keep the HUD on screen instead of fading it out.");
            y -= RowPitch;
            AddCheckBox("No Rain Wait", IdNoRainWait, y,
                "Change region at 95% of the rain cycle instead of waiting for the rain.");
            y -= RowPitch;
            AddCheckBox("Game sound", IdGameSound, y,
                "Play the game's music and sounds. Off keeps the wallpaper silent; your audio settings are not changed.");

            AddPanelButtons();
        }

        private void AddPanelButtons()
        {
            float buttonY = PanelBottom + 21f;
            SimpleButton closeButton = new SimpleButton(this, pages[0], Translate("CLOSE"), SignalClose,
                new Vector2(ContentRight - ButtonWidth + screenLeft, buttonY), new Vector2(ButtonWidth, ButtonHeight));
            SimpleButton travelButton = new SimpleButton(this, pages[0], Translate("TRAVEL"), SignalTravel,
                new Vector2(ContentRight - 2f * ButtonWidth - 14f + screenLeft, buttonY), new Vector2(ButtonWidth, ButtonHeight));
            pages[0].subObjects.Add(travelButton);
            pages[0].subObjects.Add(closeButton);
            descriptions[travelButton] = "Apply the campaign, region, room and camera selection, then close.";
            descriptions[closeButton] = "Close this panel without travelling.";
            selectableRows.Add(new MenuObject[] { travelButton, closeButton });

            // The back input (B, or the UI cancel key) selects the CLOSE button.
            backObject = closeButton;
        }

        private MenuLabel AddCenteredLabel(string text, float y, bool bigText)
        {
            MenuLabel label = new MenuLabel(this, pages[0], text, new Vector2(PanelLeft + screenLeft, y), new Vector2(PanelWidth, 0f), bigText);
            pages[0].subObjects.Add(label);
            return label;
        }

        private MenuLabel AddLeftLabel(string text, float y)
        {
            MenuLabel label = new MenuLabel(this, pages[0], text, new Vector2(ContentLeft + screenLeft, y), Vector2.zero, bigText: false);
            label.label.alignment = FLabelAlignment.Left;
            label.label.color = MenuRGB(MenuColors.MediumGrey);
            pages[0].subObjects.Add(label);
            return label;
        }

        private void AddSectionHeader(string text, float y)
        {
            MenuLabel header = AddLeftLabel(Translate(text), y);
            header.label.color = MenuRGB(MenuColors.DarkGrey);
            pages[0].subObjects.Add(new PanelDivider(this, pages[0], new Vector2(ContentLeft + screenLeft, y - 12f), ContentRight - ContentLeft));
        }

        private CyclerRow AddCyclerRow(string labelText, string prevSignal, string nextSignal, float y, string description)
        {
            float valueLeft = ControlColumnLeft + ArrowWidth;
            float valueRight = ContentRight - ArrowWidth;

            CyclerRow row = new CyclerRow
            {
                Name = AddLeftLabel(Translate(labelText), y),
                Prev = AddArrowButton("pageleft", prevSignal, ControlColumnLeft, y),
                Next = AddArrowButton("pageright", nextSignal, valueRight, y),
                ValueWidth = valueRight - valueLeft - 8f
            };

            row.Value = new MenuLabel(this, pages[0], string.Empty, new Vector2(valueLeft + screenLeft, y), new Vector2(valueRight - valueLeft, 0f), bigText: false);
            row.Value.label.color = MenuRGB(MenuColors.White);
            pages[0].subObjects.Add(row.Value);

            descriptions[row.Prev] = description;
            descriptions[row.Next] = description;
            selectableRows.Add(new MenuObject[] { row.Prev, row.Next });
            return row;
        }

        private SymbolButton AddArrowButton(string symbolName, string signal, float x, float centerY)
        {
            SymbolButton button = new SymbolButton(this, pages[0], symbolName, signal, new Vector2(x + screenLeft, centerY - ArrowHeight / 2f));

            // Resized the way the game's own page arrows are.
            button.size = new Vector2(ArrowWidth, ArrowHeight);
            button.roundedRect.size = button.size;
            pages[0].subObjects.Add(button);
            return button;
        }

        private CheckBox AddCheckBox(string labelText, string idString, float centerY, string description)
        {
            // The label sits to the left of the box (the checkbox default), starting at the content's left edge.
            float boxLeft = ContentRight - CheckBoxSize;
            CheckBox box = new CheckBox(this, pages[0], this, new Vector2(boxLeft + screenLeft, centerY - CheckBoxSize / 2f),
                boxLeft - ContentLeft, Translate(labelText), idString);
            pages[0].subObjects.Add(box);

            descriptions[box] = description;
            selectableRows.Add(new MenuObject[] { box });
            return box;
        }

        // Each entry of selectableRows is one visual row (an arrow pair, a checkbox or the two buttons). Up and down
        // move between rows to the closest control in the same column, left and right move inside a row, and the
        // list wraps around.
        private void LinkSelectables()
        {
            for (int i = 0; i < selectableRows.Count; i++)
            {
                MenuObject[] row = selectableRows[i];
                MenuObject[] above = selectableRows[(i + selectableRows.Count - 1) % selectableRows.Count];
                MenuObject[] below = selectableRows[(i + 1) % selectableRows.Count];

                for (int c = 0; c < row.Length; c++)
                {
                    float x = CenterX(row[c]);
                    row[c].nextSelectable[0] = row[Math.Max(c - 1, 0)];
                    row[c].nextSelectable[1] = ClosestInRow(above, x);
                    row[c].nextSelectable[2] = row[Math.Min(c + 1, row.Length - 1)];
                    row[c].nextSelectable[3] = ClosestInRow(below, x);
                }
            }

            // Start on the first control. Menu.Init would otherwise select the back button.
            selectedObject = selectableRows[0][0];
            infolabelDirty = true;
        }

        private static float CenterX(MenuObject obj)
        {
            RectangularMenuObject rect = obj as RectangularMenuObject;
            return rect != null ? rect.pos.x + rect.size.x / 2f : 0f;
        }

        private static MenuObject ClosestInRow(MenuObject[] row, float x)
        {
            MenuObject closest = row[0];
            for (int i = 1; i < row.Length; i++)
            {
                if (Math.Abs(CenterX(row[i]) - x) < Math.Abs(CenterX(closest) - x))
                {
                    closest = row[i];
                }
            }

            return closest;
        }

        #endregion

        #region Menu overrides

        public override void Update()
        {
            if (isShutDown)
            {
                return;
            }

            base.Update();

            // A signal handled above can end the menu (a region change tears the UI down).
            if (isShutDown)
            {
                return;
            }

            RefreshStatus();
            RefreshRowStates();

            // Closing is done here, at the end of the update, the way the game's own side-process dialogs do it.
            // A process switch freezes the menu's input, so it also closes the menu.
            if (!closeRaised && (closeQueued || manager.upcomingProcess != null))
            {
                closeRaised = true;
                closeRequested?.Invoke();
            }
        }

        public override void GrafUpdate(float timeStacker)
        {
            if (isShutDown)
            {
                return;
            }

            // Menu.Init moves the page on the first Update, so stay hidden until it has run.
            container.isVisible = init;
            base.GrafUpdate(timeStacker);
        }

        public override void ShutDownProcess()
        {
            isShutDown = true;
            backdrop?.RemoveFromContainer();
            base.ShutDownProcess();
        }

        public override string UpdateInfoText()
        {
            // The base constructor calls this before any field of this class is assigned. Nothing is selected yet
            // at that point, and descriptions already exists (it has an initialiser), so this stays null-safe.
            if (selectedObject != null && descriptions != null && descriptions.TryGetValue(selectedObject, out string description))
            {
                return description;
            }

            return string.Empty;
        }

        public override void Singal(MenuObject sender, string message)
        {
            base.Singal(sender, message);

            if (message.EndsWith("_PREV", StringComparison.Ordinal) || message.EndsWith("_NEXT", StringComparison.Ordinal))
            {
                PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
            }

            switch (message)
            {
                case SignalCampaignPrev: CycleCampaign(-1); break;
                case SignalCampaignNext: CycleCampaign(1); break;
                case SignalRegionPrev: CycleRegion(-1); break;
                case SignalRegionNext: CycleRegion(1); break;
                case SignalRoomPrev: CycleRoom(-1); break;
                case SignalRoomNext: CycleRoom(1); break;
                case SignalCameraPrev: CycleCameraMode(-1); break;
                case SignalCameraNext: CycleCameraMode(1); break;
                case SignalRotStatePrev: CycleRotState(-1); break;
                case SignalRotStateNext: CycleRotState(1); break;
                case SignalChaosLevelPrev: CycleChaosLevel(-1); break;
                case SignalChaosLevelNext: CycleChaosLevel(1); break;

                case SignalTravel:
                    PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                    ApplyTravel();
                    closeQueued = true;
                    break;

                case SignalClose:
                    closeQueued = true;
                    break;
            }
        }

        #endregion

        #region Checkboxes

        public bool GetChecked(CheckBox box)
        {
            switch (box.IDString)
            {
                case IdLockRoom: return controller.IsRoomLocked;
                case IdChaos: return WallpaperMod.Options != null && WallpaperMod.Options.EnableChaos.Value;
                case IdChaosSpawnAll: return WallpaperMod.Options != null && WallpaperMod.Options.ChaosSpawnAll.Value;
                case IdAlwaysShowHud: return controller.Hud != null && controller.Hud.AlwaysShowHUD;
                case IdNoRainWait: return WallpaperMod.Options != null && WallpaperMod.Options.NoRainTransition.Value;
                case IdGameSound: return WallpaperMod.Options == null || WallpaperMod.Options.GameSound.Value;
                default: return false;
            }
        }

        public void SetChecked(CheckBox box, bool c)
        {
            switch (box.IDString)
            {
                case IdLockRoom:
                    if (controller.IsRoomLocked != c)
                    {
                        controller.ToggleRoomLock();
                    }
                    break;

                case IdChaos:
                    if (WallpaperMod.Options != null)
                    {
                        WallpaperMod.Options.EnableChaos.Value = c;
                    }
                    break;

                case IdChaosSpawnAll:
                    if (WallpaperMod.Options != null)
                    {
                        WallpaperMod.Options.ChaosSpawnAll.Value = c;
                    }
                    break;

                case IdAlwaysShowHud:
                    controller.Hud?.SetAlwaysShowHUD(c, hideImmediately: !c);
                    break;

                case IdNoRainWait:
                    if (WallpaperMod.Options != null)
                    {
                        WallpaperMod.Options.NoRainTransition.Value = c;
                    }
                    break;

                case IdGameSound:
                    if (WallpaperMod.Options != null)
                    {
                        WallpaperMod.Options.GameSound.Value = c;
                    }

                    WallpaperSound.SetSoundOn(c);
                    break;
            }
        }

        #endregion

        #region Refreshing what the panel shows

        private void RefreshStatus()
        {
            statusLabel.text = BuildStatusText();
        }

        private string BuildStatusText()
        {
            string rain;
            if (controller.IsNoRainWaitMode)
            {
                rain = $"No Rain Wait: {controller.CycleProgress * 100f:F0}% / 95%";
            }
            else if (controller.IsRainCountdownActive)
            {
                float remainingSeconds = controller.RainCountdownRemaining;
                int minutes = (int)(remainingSeconds / 60f);
                int seconds = (int)(remainingSeconds % 60f);
                rain = $"Rain in {minutes}m {seconds}s";
            }
            else
            {
                rain = $"Waiting for the rain ({controller.CycleProgress * 100f:F0}%)";
            }

            return $"{rain}  |  Region {controller.RegionsExplored} of {controller.TotalRegions}";
        }

        // Called every update, so it only compares and sets flags; the value labels are rebuilt when something changed.
        private void RefreshRowStates()
        {
            if (IsSelectedRegionCurrent() != roomRowEnabled || IsSelectedCampaignWatcher() != rotStateRowEnabled)
            {
                RefreshValueLabels();
            }

            alwaysShowHudBox.buttonBehav.greyedOut = controller.Hud == null;
        }

        private void RefreshValueLabels()
        {
            // The room can only be chosen while the selected region is the one being shown, and the Watcher world
            // state only exists for the Watcher campaign. Their arrows are greyed out otherwise.
            roomRowEnabled = IsSelectedRegionCurrent();
            rotStateRowEnabled = IsSelectedCampaignWatcher();

            SetRowValue(campaignRow, GetCampaignDisplayName(availableCampaigns[selectedCampaignIndex]), true);
            SetRowValue(regionRow, GetRegionDisplayName(availableRegions[selectedRegionIndex]), true);
            SetRowValue(roomRow,
                roomRowEnabled ? availableRooms[selectedRoomIndex] : "Random (travel to this region first)",
                roomRowEnabled);
            SetRowValue(cameraRow, GetCameraModeDisplayName(availableCameraModes[selectedCameraModeIndex]), true);
            SetRowValue(rotStateRow,
                rotStateRowEnabled ? GetRotStateDisplayName(availableRotStates[selectedRotStateIndex]) : "Watcher campaign only",
                rotStateRowEnabled);

            int chaosLevel = WallpaperMod.Options?.ChaosLevel.Value ?? WallpaperModOptions.ChaosLevelRangeMin;
            SetRowValue(chaosLevelRow, chaosLevel.ToString(), true);
        }

        private static void SetRowValue(CyclerRow row, string text, bool enabled)
        {
            row.Value.text = text;
            row.Value.label.color = MenuRGB(enabled ? MenuColors.White : MenuColors.DarkGrey);
            row.Name.label.color = MenuRGB(enabled ? MenuColors.MediumGrey : MenuColors.DarkGrey);
            row.Prev.buttonBehav.greyedOut = !enabled;
            row.Next.buttonBehav.greyedOut = !enabled;

            // A long name (custom regions, translations) is scaled down to stay between the arrows.
            float width = row.Value.label.textRect.width;
            row.Value.label.scale = width > row.ValueWidth && width > 0f ? Mathf.Max(0.6f, row.ValueWidth / width) : 1f;
        }

        #endregion

        #region Quick travel selection

        private void InitializeSelectionLists()
        {
            // Get all campaigns from enum
            availableCampaigns = Enum.GetNames(typeof(WallpaperModOptions.CampaignChoice)).ToList();
            selectedCampaignIndex = 0;

            // Try to match current campaign
            if (WallpaperMod.Options != null)
            {
                string currentCampaign = WallpaperMod.CurrentCampaign;
                int index = availableCampaigns.FindIndex(c => c == currentCampaign);
                if (index >= 0)
                {
                    selectedCampaignIndex = index;
                }
            }

            RefreshRegionList(preferredRegion: controller.RegionMgr?.GetCurrentRegion());

            // Get all camera modes from enum
            availableCameraModes = Enum.GetNames(typeof(WallpaperModOptions.CameraMode)).ToList();
            selectedCameraModeIndex = 0;

            // Try to match current camera mode
            if (WallpaperMod.Options != null)
            {
                string currentMode = WallpaperMod.Options.CameraModeConfig.Value;
                int index = availableCameraModes.FindIndex(m => m == currentMode);
                if (index >= 0)
                {
                    selectedCameraModeIndex = index;
                }
            }

            availableRotStates = Enum.GetNames(typeof(WallpaperModOptions.RotState)).ToList();
            selectedRotStateIndex = 0;

            if (WallpaperMod.Options != null)
            {
                string currentRotState = WallpaperMod.Options.RotStateConfig.Value;
                int index = availableRotStates.FindIndex(s => s == currentRotState);
                if (index >= 0)
                {
                    selectedRotStateIndex = index;
                }
            }

            // Get all rooms in current region
            RefreshRoomList();
        }

        private void RefreshRoomList()
        {
            availableRooms = new List<string> { "Random" };
            selectedRoomIndex = 0;

            if (controller.Game?.world?.abstractRooms != null)
            {
                foreach (var room in controller.Game.world.abstractRooms)
                {
                    if (WallpaperRoomTracker.IsSafeWallpaperRoom(room))
                    {
                        availableRooms.Add(room.name);
                    }
                }

                // Try to match current room
                string currentRoom = controller.CurrentRoomName;
                if (!string.IsNullOrEmpty(currentRoom))
                {
                    int index = availableRooms.FindIndex(r => string.Equals(r, currentRoom, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        selectedRoomIndex = index;
                    }
                }
            }
        }

        private void RefreshRegionList(string preferredRegion = null)
        {
            string selectedCampaign = availableCampaigns != null && availableCampaigns.Count > 0
                ? availableCampaigns[selectedCampaignIndex]
                : WallpaperMod.CurrentCampaign;

            availableRegions = WallpaperRegionCatalog.GetRegionsForCampaign(selectedCampaign).ToList();
            selectedRegionIndex = 0;

            string regionToSelect = !string.IsNullOrWhiteSpace(preferredRegion)
                ? preferredRegion
                : WallpaperRegionCatalog.GetDefaultStartRegionForCampaign(selectedCampaign);

            int index = availableRegions.FindIndex(r => string.Equals(r, regionToSelect, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                selectedRegionIndex = index;
            }
        }

        private bool IsSelectedRegionCurrent()
        {
            if (availableRegions == null || availableRegions.Count == 0) return false;
            if (controller.RegionMgr == null) return false;

            string selectedRegion = availableRegions[selectedRegionIndex];
            string currentRegion = controller.RegionMgr.GetCurrentRegion();

            return string.Equals(selectedRegion, currentRegion, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSelectedCampaignWatcher()
        {
            return availableCampaigns != null &&
                availableCampaigns.Count > 0 &&
                string.Equals(availableCampaigns[selectedCampaignIndex], "Watcher", StringComparison.OrdinalIgnoreCase);
        }

        private void CycleCampaign(int direction)
        {
            if (availableCampaigns == null || availableCampaigns.Count == 0) return;

            selectedCampaignIndex = (selectedCampaignIndex + direction + availableCampaigns.Count) % availableCampaigns.Count;
            RefreshRegionList();
            selectedRoomIndex = 0;
            RefreshValueLabels();
        }

        private void CycleRegion(int direction)
        {
            if (availableRegions == null || availableRegions.Count == 0) return;

            selectedRegionIndex = (selectedRegionIndex + direction + availableRegions.Count) % availableRegions.Count;

            // When region changes, reset room selection to Random
            selectedRoomIndex = 0; // "Random" is always index 0

            RefreshValueLabels();
        }

        private void CycleCameraMode(int direction)
        {
            if (availableCameraModes == null || availableCameraModes.Count == 0) return;

            selectedCameraModeIndex = (selectedCameraModeIndex + direction + availableCameraModes.Count) % availableCameraModes.Count;
            RefreshValueLabels();
        }

        private void CycleRotState(int direction)
        {
            if (availableRotStates == null || availableRotStates.Count == 0) return;

            // Only the Watcher campaign has world states, so the arrows are greyed out for the others.
            if (!IsSelectedCampaignWatcher()) return;

            selectedRotStateIndex = (selectedRotStateIndex + direction + availableRotStates.Count) % availableRotStates.Count;
            if (WallpaperMod.Options != null)
            {
                WallpaperMod.Options.RotStateConfig.Value = availableRotStates[selectedRotStateIndex];
            }
            RefreshValueLabels();
        }

        private void CycleRoom(int direction)
        {
            if (availableRooms == null || availableRooms.Count == 0) return;

            // Only allow room cycling if selected region matches current region. Otherwise room selection is disabled.
            if (!IsSelectedRegionCurrent()) return;

            selectedRoomIndex = (selectedRoomIndex + direction + availableRooms.Count) % availableRooms.Count;
            RefreshValueLabels();
        }

        private void CycleChaosLevel(int direction)
        {
            // Chaos level cycling within the allowed range
            if (WallpaperMod.Options == null) return;

            int currentLevel = WallpaperMod.Options.ChaosLevel.Value;
            int newLevel = currentLevel + direction;

            // Wrap around between the lowest and highest allowed level
            if (newLevel < WallpaperModOptions.ChaosLevelRangeMin) newLevel = WallpaperModOptions.ChaosLevelRangeMax;
            if (newLevel > WallpaperModOptions.ChaosLevelRangeMax) newLevel = WallpaperModOptions.ChaosLevelRangeMin;

            WallpaperMod.Options.ChaosLevel.Value = newLevel;
            RefreshValueLabels();
        }

        private void ApplyTravel()
        {
            if (availableCampaigns == null || availableRegions == null || availableCameraModes == null || availableRotStates == null || availableRooms == null) return;

            string selectedCampaign = availableCampaigns[selectedCampaignIndex];
            string selectedRegion = availableRegions[selectedRegionIndex];
            string selectedCameraMode = availableCameraModes[selectedCameraModeIndex];
            string selectedRotState = IsSelectedCampaignWatcher() ? availableRotStates[selectedRotStateIndex] : WallpaperModOptions.RotState.Natural.ToString();
            string selectedRoom = availableRooms[selectedRoomIndex];

            bool regionChanging = !IsSelectedRegionCurrent();
            string currentCampaign = WallpaperMod.CurrentCampaign;
            bool campaignChanging = !string.Equals(currentCampaign, selectedCampaign, StringComparison.OrdinalIgnoreCase);

            WallpaperMod.Log?.LogInfo($"Quick Travel: Campaign={selectedCampaign}, Region={selectedRegion}, CameraMode={selectedCameraMode}, RotState={selectedRotState}, Room={selectedRoom}");

            // Update the config
            if (WallpaperMod.Options != null)
            {
                WallpaperMod.Options.SelectedCampaign.Value = selectedCampaign;
                WallpaperMod.Options.StartRegion.Value = selectedRegion;
                WallpaperMod.Options.CameraModeConfig.Value = selectedCameraMode;
                WallpaperMod.Options.RotStateConfig.Value = selectedRotState;
            }

            // Update camera mode in controller
            controller.SetCameraMode(WallpaperModOptions.GetCameraMode(selectedCameraMode));
            if (campaignChanging)
            {
                // The selected campaign was just written to the config; end any command-line override so it applies
                WallpaperMod.Instance?.ClearCampaignOverride();
                controller.RegionMgr?.OnCampaignChange(selectedRegion);
                regionChanging = true;
            }

            // If changing regions, do that first (room change will happen after region loads)
            if (regionChanging)
            {
                controller.RequestRegionChange(selectedRegion);
                // The menu is created again with the new region's room list the next time it is opened
            }
            else
            {
                // If staying in same region and a specific room is selected, jump to it
                if (selectedRoom != "Random")
                {
                    controller.RequestRoomChange(selectedRoom);
                }
            }
        }

        private string GetCampaignDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "White": return "Survivor";
                case "Yellow": return "Monk";
                case "Red": return "Hunter";
                case "Gourmand": return "Gourmand";
                case "Artificer": return "Artificer";
                case "Rivulet": return "Rivulet";
                case "Spearmaster": return "Spearmaster";
                case "Saint": return "Saint";
                case "Watcher": return "Watcher";
                default: return enumName;
            }
        }

        private string GetRegionDisplayName(string enumName)
        {
            string selectedCampaign = availableCampaigns != null && availableCampaigns.Count > 0
                ? availableCampaigns[selectedCampaignIndex]
                : WallpaperMod.CurrentCampaign;

            return WallpaperRegionCatalog.GetDisplayName(enumName, selectedCampaign);
        }

        private string GetCameraModeDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "RandomExploration": return "Random Explore";
                case "Random": return "Single Random";
                case "Sequential": return "All Angles";
                case "FirstOnly": return "First Only";
                default: return enumName;
            }
        }

        private string GetRotStateDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "Natural": return "Natural Mix";
                case "Normal": return "Clean";
                case "Rotted": return "Rotted";
                case "Karma": return "Karma Flowers";
                case "RottedKarma": return "Rot + Karma";
                default: return enumName;
            }
        }

        #endregion
    }
}
