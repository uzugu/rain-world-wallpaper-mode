using System;
using System.Collections.Generic;
using System.Linq;
using Menu;
using RWCustom;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// The minimalistic layout of the wallpaper settings and quick-travel menu (--wallpaper-menu-style minimal). It
    /// holds the same settings and works the same way as the panel, but it is only text: one column at the left of the
    /// screen, a status line above it, labels in dark grey with the value of each setting beside them, and no panel,
    /// borders or section headers. The column stands on a soft darkening that fades out towards the middle of the screen.
    /// The selected row brightens and gets a thin marker.
    ///
    /// Every row is a MinimalRow, a ButtonTemplate of its own, so the mouse, the arrow keys and controllers navigate it
    /// the way they navigate any other Rain World menu. It runs as a side process next to the wallpaper game, so nothing
    /// pauses while it is open.
    /// </summary>
    internal sealed class WallpaperMinimalMenu : Menu.Menu
    {
        // The ID only labels the menu for the game. It is registered once, here, so the ExtEnum entry stays unique.
        private static readonly ProcessManager.ProcessID MinimalProcessID = new ProcessManager.ProcessID("WallpaperSettingsMinimal", register: true);

        // Layout, in the game's 1366x768 menu space with x measured from the left edge of the visible screen
        // (screenLeft converts it, because Menu.Init shifts the page to centre that space on narrower screens).
        private const float ColumnLeft = 90f;       // where the labels start
        private const float ValueGap = 28f;         // least space between the longest label and the values
        private const float GroupGap = 18f;         // extra space between the groups of rows
        private const float StatusHeight = 22f;     // the status line above the rows
        private const float StatusGap = 14f;        // extra space under the status line
        private const float NoteHeight = 20f;       // the small note under the creature settings

        // Coming in: everything fades in over OpenTicks game ticks and the column slides in from the left.
        private const int OpenTicks = 10;
        private const float OpenCurve = 0.65f;
        private const float SlideDistance = 16f;

        // The darkening behind the column. How strong it needs to be can only be judged on screen, so it is easy to
        // tune: ShadeAlpha is the strength of each blob and ShadeBlobCount how many are stacked along the column.
        // The blobs are centred ShadeCenterX from the left edge of the screen and reach the middle of the screen, or
        // ShadeTail past the widest text when that is further.
        private const int ShadeBlobCount = 6;
        private const float ShadeAlpha = 0.65f;
        private const float ShadeCenterX = 40f;
        private const float ShadeTail = 90f;

        // Signals sent by the rows.
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

        // A toggle row sends this followed by its ID.
        private const string SignalTogglePrefix = "TOGGLE_";

        // IDs of the toggles.
        private const string IdLockRoom = "LOCKROOM";
        private const string IdChaos = "CHAOS";
        private const string IdChaosSpawnAll = "CHAOSSPAWNALL";
        private const string IdAlwaysShowHud = "ALWAYSSHOWHUD";
        private const string IdNoRainWait = "NORAINWAIT";

        // The rain part of the status line.
        private const int RainNoWait = 0;
        private const int RainCountdown = 1;
        private const int RainWaiting = 2;

        // Where a row or line goes in the column. Everything is first stacked from the top and centred on the screen
        // once the whole column is known.
        private readonly struct LayoutEntry
        {
            public LayoutEntry(PositionedMenuObject item, float top, float height)
            {
                Item = item;
                Top = top;
                Height = height;
            }

            public PositionedMenuObject Item { get; }
            public float Top { get; }
            public float Height { get; }
        }

        private readonly WallpaperController controller;
        private readonly Action closeRequested;
        private readonly float screenLeft;

        // Filled while the controls are built. These start empty (field initialisers run before the base
        // constructor), because the base constructor already calls UpdateInfoText.
        private readonly Dictionary<MenuObject, string> descriptions = new Dictionary<MenuObject, string>();
        private readonly List<MinimalRow> rows = new List<MinimalRow>();
        private readonly Dictionary<string, MinimalRow> toggleRows = new Dictionary<string, MinimalRow>();
        private readonly List<LayoutEntry> layout = new List<LayoutEntry>();

        private MinimalShade shade;
        private MinimalColumn column;
        private MenuLabel statusLabel;
        private MinimalRow campaignRow;
        private MinimalRow regionRow;
        private MinimalRow roomRow;
        private MinimalRow cameraRow;
        private MinimalRow rotStateRow;
        private MinimalRow chaosLevelRow;
        private MinimalRow alwaysShowHudRow;

        private string onText = "On";
        private string offText = "Off";
        private string roomUnavailableText = string.Empty;
        private string rotStateUnavailableText = string.Empty;
        private float layoutCursor;
        private float columnTop;
        private float columnBottom;

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

        // What the status line shows now, so its text is only rebuilt when something in it changed.
        private bool statusBuilt;
        private string shownCampaign;
        private string shownRegionCode;
        private int shownRainKind;
        private int shownRainValue;
        private int shownExplored;
        private int shownTotal;
        private string shownRegionName = string.Empty;

        private float open;
        private float lastOpen;
        private bool roomRowEnabled;
        private bool rotStateRowEnabled;
        private bool closeQueued;
        private bool closeRaised;
        private bool isShutDown;
        private bool faulted;

        public WallpaperMinimalMenu(ProcessManager manager, WallpaperController controller, Action closeRequested)
            : base(manager, MinimalProcessID)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.closeRequested = closeRequested;
            screenLeft = HorizontalMoveToGetCentered(manager);

            try
            {
                pages.Add(new Page(this, null, "main", 0));

                InitializeSelectionLists();

                // Sprites are drawn in the order they are made, so the darkening is made before any text.
                BuildShade();
                BuildColumn();
                LayoutColumn();
                LinkSelectables();
                RefreshValueLabels();
                RefreshStatus();
                LayoutShade();
            }
            catch
            {
                // Nothing else owns a half-built menu, so take its containers off the stage before the error moves on.
                ShutDownProcess();
                throw;
            }
        }

        #region Building the menu

        private void BuildShade()
        {
            FShader flatLight = null;
            if (manager.rainWorld.Shaders != null)
            {
                manager.rainWorld.Shaders.TryGetValue("FlatLight", out flatLight);
            }

            // Without the shader the blobs would be solid squares, so the menu simply goes without a darkening.
            if (flatLight != null)
            {
                shade = new MinimalShade(pages[0].Container, flatLight, ShadeBlobCount, ShadeAlpha);
            }
        }

        private void BuildColumn()
        {
            // The column starts SlideDistance to the left of where it ends up and slides in while the menu fades in.
            column = new MinimalColumn(this, pages[0], new Vector2(ColumnLeft + screenLeft - SlideDistance, 0f));
            pages[0].subObjects.Add(column);

            onText = Tr("On");
            offText = Tr("Off");
            roomUnavailableText = Tr("Current region only");
            rotStateUnavailableText = Tr("Watcher campaign only");

            statusLabel = AddTextLine(string.Empty, StatusHeight, MenuColors.MediumGrey);
            layoutCursor += StatusGap;

            // Travel
            campaignRow = AddCyclerRow("Campaign", SignalCampaignPrev, SignalCampaignNext,
                "Choose which slugcat campaign the wallpaper tours. Applied with Travel.");
            regionRow = AddCyclerRow("Region", SignalRegionPrev, SignalRegionNext,
                "Choose the region to travel to. Applied with Travel.");
            roomRow = AddCyclerRow("Room", SignalRoomPrev, SignalRoomNext,
                "Choose a room to jump to in the current region. Applied with Travel.");
            cameraRow = AddCyclerRow("Camera", SignalCameraPrev, SignalCameraNext,
                "Choose how camera positions are picked inside a room. Applied with Travel.");
            rotStateRow = AddCyclerRow("Watcher world", SignalRotStatePrev, SignalRotStateNext,
                "Choose the Watcher world state: natural mix, clean, rotted, karma flowers or both. Applies right away.");
            AddToggleRow("Lock room", IdLockRoom,
                "Stay in this room and hold off the region change until it is unlocked.");

            // Creatures
            layoutCursor += GroupGap;
            AddToggleRow("Chaos creatures", IdChaos,
                "Spawn extra creatures into the rooms you visit.");
            chaosLevelRow = AddCyclerRow("Chaos level", SignalChaosLevelPrev, SignalChaosLevelNext,
                "How often creatures appear and how many can be alive at once.");
            AddToggleRow("Spawn every creature (experimental)", IdChaosSpawnAll,
                "Experimental: also spawns creature types that are normally left out.");
            AddTextLine(Tr("Creature changes apply in the next region"), NoteHeight, MenuColors.DarkGrey);

            // Display
            layoutCursor += GroupGap;
            alwaysShowHudRow = AddToggleRow("Always show HUD", IdAlwaysShowHud,
                "Keep the HUD on screen instead of fading it out.");
            AddToggleRow("No Rain Wait", IdNoRainWait,
                "Change region at 95% of the rain cycle instead of waiting for the rain.");

            // The text buttons
            layoutCursor += GroupGap;
            AddRow(MinimalRowKind.Action, "Travel", SignalTravel, null,
                "Apply the campaign, region, room and camera selection, then close.");
            MinimalRow closeRow = AddRow(MinimalRowKind.Action, "Close", SignalClose, null,
                "Close this menu without travelling.");

            // The back input (B, or the UI cancel key) selects the Close row.
            backObject = closeRow;
        }

        private MenuLabel AddTextLine(string text, float height, MenuColors color)
        {
            MenuLabel line = new MenuLabel(this, column, text, Vector2.zero, new Vector2(0f, height), bigText: false);
            line.label.alignment = FLabelAlignment.Left;
            line.label.color = MenuRGB(color);
            column.subObjects.Add(line);
            Place(line, height);
            return line;
        }

        private MinimalRow AddRow(MinimalRowKind kind, string labelText, string signal, string backSignal, string description)
        {
            MinimalRow row = new MinimalRow(this, column, kind, Tr(labelText), signal, backSignal);
            column.subObjects.Add(row);
            Place(row, MinimalRow.RowHeight);

            rows.Add(row);
            descriptions[row] = description;
            return row;
        }

        private MinimalRow AddCyclerRow(string labelText, string prevSignal, string nextSignal, string description)
        {
            return AddRow(MinimalRowKind.Cycler, labelText, nextSignal, prevSignal, description);
        }

        private MinimalRow AddToggleRow(string labelText, string id, string description)
        {
            MinimalRow row = AddRow(MinimalRowKind.Toggle, labelText, SignalTogglePrefix + id, null, description);
            toggleRows[id] = row;
            return row;
        }

        private void Place(PositionedMenuObject item, float height)
        {
            layout.Add(new LayoutEntry(item, layoutCursor, height));
            layoutCursor += height;
        }

        // Fixes where everything goes once the whole column exists: where the values start, and the column's place on
        // the screen, vertically centred.
        private void LayoutColumn()
        {
            // The values start a fixed distance from the labels, further out when a long label would leave no gap.
            float widestLabel = 0f;
            foreach (MinimalRow row in rows)
            {
                widestLabel = Mathf.Max(widestLabel, row.LabelWidth);
            }

            float valueOffset = Mathf.Max(MinimalRow.DefaultValueOffset, Mathf.Ceil(widestLabel + ValueGap));
            foreach (MinimalRow row in rows)
            {
                row.SetValueColumn(valueOffset);
            }

            float screenHeight = manager.rainWorld.options.ScreenSize.y;
            columnTop = Mathf.Round((screenHeight + layoutCursor) / 2f);
            columnBottom = columnTop - layoutCursor;
            foreach (LayoutEntry entry in layout)
            {
                float y = columnTop - entry.Top - entry.Height;
                entry.Item.pos = new Vector2(entry.Item.pos.x, y);
                entry.Item.lastPos = entry.Item.pos;
            }
        }

        // Sized last, because it has to reach past the widest text. Up to the middle of the screen it fades out, and
        // further if the text is wider than that.
        private void LayoutShade()
        {
            if (shade == null)
            {
                return;
            }

            float contentRight = ColumnLeft + statusLabel.label.textRect.width;
            foreach (MinimalRow row in rows)
            {
                contentRight = Mathf.Max(contentRight, ColumnLeft + row.pos.x + row.size.x);
            }

            float reach = Mathf.Max(manager.rainWorld.options.ScreenSize.x * 0.5f, contentRight + ShadeTail);
            shade.Layout(ShadeCenterX, reach - ShadeCenterX, columnBottom, columnTop);
        }

        // Up and down move between the rows and wrap around. Left and right lead back to the same row: the menu then
        // leaves the selection where it is, and a cycler uses the input itself.
        private void LinkSelectables()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                MinimalRow row = rows[i];
                row.nextSelectable[0] = row;
                row.nextSelectable[1] = rows[(i + rows.Count - 1) % rows.Count];
                row.nextSelectable[2] = row;
                row.nextSelectable[3] = rows[(i + 1) % rows.Count];
            }

            // Start on the first row. Menu.Init would otherwise select the back button.
            selectedObject = rows[0];
            infolabelDirty = true;
        }

        private string Tr(string text)
        {
            return WallpaperHudText.Translate(manager.rainWorld.inGameTranslator, text);
        }

        #endregion

        #region Menu overrides

        public override void Update()
        {
            if (isShutDown || faulted)
            {
                return;
            }

            try
            {
                base.Update();

                // A signal handled above can end the menu (a region change tears the UI down).
                if (isShutDown)
                {
                    return;
                }

                // Menu.Init has run by now, so this is where the menu starts to come in.
                lastOpen = open;
                open = Mathf.Min(1f, open + 1f / OpenTicks);
                float restX = ColumnLeft + screenLeft;
                column.pos = new Vector2(restX - SlideDistance * (1f - Custom.SCurve(open, OpenCurve)), column.pos.y);

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
            catch (Exception ex)
            {
                Fault(ex);
            }
        }

        public override void GrafUpdate(float timeStacker)
        {
            if (isShutDown || faulted)
            {
                return;
            }

            try
            {
                // Menu.Init moves the page on the first Update, so stay hidden until it has run.
                float shown = Custom.SCurve(Mathf.Lerp(lastOpen, open, timeStacker), OpenCurve);
                container.isVisible = init && shown > 0f;
                container.alpha = shown;
                base.GrafUpdate(timeStacker);

                // The tooltip line belongs to the stage, not to the container, so it comes in with the menu by hand.
                if (infoLabel != null)
                {
                    infoLabel.alpha *= shown;
                }
            }
            catch (Exception ex)
            {
                Fault(ex);
            }
        }

        // The menu is cosmetic: never let it take the wallpaper down, and do not repeat the error every tick or frame.
        // It is logged once and the menu closes.
        private void Fault(Exception ex)
        {
            faulted = true;
            WallpaperMod.Log?.LogError($"WallpaperMinimalMenu: closing the menu after an error - {ex}");

            if (!closeRaised)
            {
                closeRaised = true;
                closeRequested?.Invoke();
            }
        }

        public override void ShutDownProcess()
        {
            isShutDown = true;
            shade?.Remove();
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

            if (message.StartsWith(SignalTogglePrefix, StringComparison.Ordinal))
            {
                ToggleSetting(message.Substring(SignalTogglePrefix.Length));
                return;
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

        #region Toggles

        private bool GetToggle(string id)
        {
            switch (id)
            {
                case IdLockRoom: return controller.IsRoomLocked;
                case IdChaos: return WallpaperMod.Options != null && WallpaperMod.Options.EnableChaos.Value;
                case IdChaosSpawnAll: return WallpaperMod.Options != null && WallpaperMod.Options.ChaosSpawnAll.Value;
                case IdAlwaysShowHud: return controller.Hud != null && controller.Hud.AlwaysShowHUD;
                case IdNoRainWait: return WallpaperMod.Options != null && WallpaperMod.Options.NoRainTransition.Value;
                default: return false;
            }
        }

        private void SetToggle(string id, bool c)
        {
            switch (id)
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
            }
        }

        // Flips a toggle the way the game's check boxes do: the sound follows the state the setting ended up in.
        private void ToggleSetting(string id)
        {
            SetToggle(id, !GetToggle(id));
            PlaySound(GetToggle(id) ? SoundID.MENU_Checkbox_Check : SoundID.MENU_Checkbox_Uncheck);
            RefreshToggleRows();
        }

        #endregion

        #region Refreshing what the menu shows

        private void RefreshStatus()
        {
            string campaign = WallpaperMod.CurrentCampaign ?? string.Empty;
            string regionCode = controller.CurrentRegionCode;

            int rainKind;
            int rainValue;
            if (controller.IsNoRainWaitMode)
            {
                rainKind = RainNoWait;
                rainValue = ToPercent(controller.CycleProgress);
            }
            else if (controller.IsRainCountdownActive)
            {
                rainKind = RainCountdown;
                rainValue = (int)controller.RainCountdownRemaining;
            }
            else
            {
                rainKind = RainWaiting;
                rainValue = ToPercent(controller.CycleProgress);
            }

            int explored = controller.RegionsExplored;
            int total = controller.TotalRegions;

            bool regionChanged = !statusBuilt || !string.Equals(regionCode, shownRegionCode, StringComparison.Ordinal);
            if (!regionChanged
                && string.Equals(campaign, shownCampaign, StringComparison.Ordinal)
                && rainKind == shownRainKind
                && rainValue == shownRainValue
                && explored == shownExplored
                && total == shownTotal)
            {
                return;
            }

            if (regionChanged)
            {
                shownRegionName = WallpaperHudText.ResolveRegionName(regionCode, manager.rainWorld.inGameTranslator);
            }

            statusBuilt = true;
            shownCampaign = campaign;
            shownRegionCode = regionCode;
            shownRainKind = rainKind;
            shownRainValue = rainValue;
            shownExplored = explored;
            shownTotal = total;
            statusLabel.text = BuildStatusText();
        }

        private static int ToPercent(float progress)
        {
            return (int)Math.Round(progress * 100f, MidpointRounding.AwayFromZero);
        }

        // The campaign, the region, the rain and how far through the regions the wallpaper is, with a gap and a bar between them.
        private string BuildStatusText()
        {
            string rain;
            switch (shownRainKind)
            {
                case RainNoWait:
                    rain = $"No Rain Wait: {shownRainValue}% / 95%";
                    break;
                case RainCountdown:
                    rain = $"Rain in {shownRainValue / 60}m {shownRainValue % 60}s";
                    break;
                default:
                    rain = $"Waiting for the rain ({shownRainValue}%)";
                    break;
            }

            List<string> parts = new List<string>(4);
            if (!string.IsNullOrEmpty(shownCampaign))
            {
                parts.Add(GetCampaignDisplayName(shownCampaign));
            }

            if (!string.IsNullOrEmpty(shownRegionName))
            {
                parts.Add(shownRegionName);
            }

            parts.Add(rain);
            parts.Add($"Region {shownExplored} of {shownTotal}");
            return string.Join("  |  ", parts);
        }

        // Called every update, so it only compares and sets flags; the value labels are rebuilt when something changed.
        private void RefreshRowStates()
        {
            if (IsSelectedRegionCurrent() != roomRowEnabled || IsSelectedCampaignWatcher() != rotStateRowEnabled)
            {
                RefreshValueLabels();
            }

            RefreshToggleRows();
            alwaysShowHudRow.Enabled = controller.Hud != null;
        }

        // The toggles show the setting itself, so a change made anywhere else, such as the room lock key, shows up here.
        private void RefreshToggleRows()
        {
            foreach (KeyValuePair<string, MinimalRow> toggle in toggleRows)
            {
                toggle.Value.SetValue(GetToggle(toggle.Key) ? onText : offText);
            }
        }

        private void RefreshValueLabels()
        {
            // The room can only be chosen while the selected region is the one being shown, and the Watcher world
            // state only exists for the Watcher campaign. Those rows are greyed out otherwise and say why.
            roomRowEnabled = IsSelectedRegionCurrent();
            rotStateRowEnabled = IsSelectedCampaignWatcher();

            SetRowValue(campaignRow, GetCampaignDisplayName(availableCampaigns[selectedCampaignIndex]), true);
            SetRowValue(regionRow, GetRegionDisplayName(availableRegions[selectedRegionIndex]), true);
            SetRowValue(roomRow,
                roomRowEnabled ? availableRooms[selectedRoomIndex] : roomUnavailableText,
                roomRowEnabled);
            SetRowValue(cameraRow, GetCameraModeDisplayName(availableCameraModes[selectedCameraModeIndex]), true);
            SetRowValue(rotStateRow,
                rotStateRowEnabled ? GetRotStateDisplayName(availableRotStates[selectedRotStateIndex]) : rotStateUnavailableText,
                rotStateRowEnabled);

            int chaosLevel = WallpaperMod.Options?.ChaosLevel.Value ?? WallpaperModOptions.ChaosLevelRangeMin;
            SetRowValue(chaosLevelRow, chaosLevel.ToString(), true);
        }

        private static void SetRowValue(MinimalRow row, string text, bool enabled)
        {
            row.SetValue(text);
            row.Enabled = enabled;
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

            // Only the Watcher campaign has world states, so the row is greyed out for the others.
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
