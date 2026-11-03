using System;
using System.Collections.Generic;
using System.Linq;
using Menu;
using RWCustom;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// The floating-elements layout of the wallpaper settings and quick-travel menu: no panel, only a column of small
    /// cards on the right, one per control, floating over the wallpaper, with TRAVEL and CLOSE at the bottom right where
    /// the pause menu keeps CONTINUE and EXIT. It is built from the game's own menu objects, so the mouse, the arrow keys
    /// and controllers all work, and it runs as a side process next to the wallpaper game, so nothing pauses while it is
    /// open. It does the same things as WallpaperSettingsMenu and differs only in how it looks, how it is laid out and how
    /// it moves: the cards slide in from the right one after the next, bob gently while the menu is idle, and slide out
    /// again when the menu closes itself (see FloatingMotion for the timing).
    /// </summary>
    internal sealed class WallpaperFloatingMenu : Menu.Menu, CheckBox.IOwnCheckBox
    {
        // The ID only labels the menu for the game. It is registered once, here, so the ExtEnum entry stays unique.
        private static readonly ProcessManager.ProcessID FloatingProcessID = new ProcessManager.ProcessID("WallpaperSettingsFloating", register: true);

        // Layout, in the game's 1366x768 menu space. The column is placed from the right edge of the visible screen;
        // screenLeft converts to the page's coordinates, because Menu.Init shifts the page to centre that space on
        // narrower screens.
        private const float ColumnWidth = 420f;
        private const float ColumnRightMargin = 60f;
        private const float CardHeight = 36f;
        private const float CardGap = 8f;
        private const float CardPaddingLeft = 14f;
        private const float CardPaddingRight = 10f;
        private const float LabelNudge = 1f;
        private const float ArrowWidth = 32f;
        private const float ArrowHeight = 26f;
        private const float CyclerWidth = 256f;
        private const float CheckBoxSize = 24f;
        private const float ButtonWidth = 110f;
        private const float ButtonHeight = 30f;
        private const float ButtonGap = 30f;
        private const float TitleDrop = 42f;
        private const float StatusDrop = 32f;
        private const float StatusBand = 14f;
        private const float HeaderBand = 28f;
        private const float HeaderInset = 4f;
        private const float GroupGap = 8f;
        private const float ShadowAlpha = 0.3f;
        private const int ShadowFadeTicks = 2 * FloatingMotion.EnterTicks;

        // Items slide in from this far to the right of where they rest: the column's width plus its margin plus some
        // slack, so an item that is still waiting for its turn is entirely off the screen, and so is one that has left.
        private const float SlideSlack = 12f;

        // Signals sent by the arrow buttons and the two bottom buttons.
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

        private sealed class CyclerRow
        {
            public MenuLabel Name;
            public SymbolButton Prev;
            public SymbolButton Next;
            public MenuLabel Value;
            public float ValueWidth;
        }

        // Something that slides and bobs: a card, a label or a button, with the place it rests at (in page
        // coordinates), the tick its slide-in starts on and the phase of its bobbing.
        private sealed class FloatingItem
        {
            public PositionedMenuObject Obj;
            public Vector2 Rest;
            public int Delay;
            public int BobIndex;
        }

        private readonly WallpaperController controller;
        private readonly Action closeRequested;
        private readonly float screenLeft;

        // Filled while the controls are built. These start empty (field initialisers run before the base
        // constructor), because the base constructor already calls UpdateInfoText.
        private readonly Dictionary<MenuObject, string> descriptions = new Dictionary<MenuObject, string>();
        private readonly List<MenuObject[]> selectableRows = new List<MenuObject[]>();
        private readonly List<FloatingItem> floaters = new List<FloatingItem>();

        // The column, in page coordinates (they include screenLeft), and the cursor the cards are stacked down from.
        private float screenHeight;
        private float columnLeft;
        private float columnRight;
        private float bottomButtonsY;
        private float layoutTop;
        private float slideDistance;
        private int cardCount;

        private DarkGradient shadow;
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

        // The motion clock: ticks since the first update, and ticks since the menu started closing itself.
        private int ticks;
        private int exitTicks;

        public WallpaperFloatingMenu(ProcessManager manager, WallpaperController controller, Action closeRequested)
            : base(manager, FloatingProcessID)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.closeRequested = closeRequested;
            screenLeft = HorizontalMoveToGetCentered(manager);

            try
            {
                pages.Add(new Page(this, null, "main", 0));

                InitializeSelectionLists();
                MeasureColumn();
                BuildShadow();
                BuildControls();
                LinkSelectables();
                PlaceOffScreen();
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

        #region Building the menu

        private void MeasureColumn()
        {
            Vector2 screenSize = manager.rainWorld.options.ScreenSize;
            Vector2 safeOffset = manager.rainWorld.options.SafeScreenOffset;

            screenHeight = screenSize.y;
            columnRight = screenSize.x + screenLeft - ColumnRightMargin - safeOffset.x;
            columnLeft = columnRight - ColumnWidth;
            bottomButtonsY = Mathf.Max(safeOffset.y, 15f);
            slideDistance = ColumnWidth + ColumnRightMargin + safeOffset.x + SlideSlack;
        }

        private void BuildShadow()
        {
            // The only thing behind the column is a very light, soft shadow, so the title and the headers stay readable
            // over a bright scene. It is the game's own DarkGradient (a black FlatLight sprite), made before the cards
            // so that they are drawn over it. It stays put and fades in and out with the rest.
            Vector2 center = new Vector2(columnLeft + ColumnWidth / 2f, screenHeight / 2f);
            shadow = new DarkGradient(this, pages[0], center, ColumnWidth / 2f + 90f, screenHeight / 2f + 20f, 0f);
            pages[0].subObjects.Add(shadow);
        }

        private void BuildControls()
        {
            // The title and the status line come in first, and bob in phase with the first cards.
            float y = screenHeight - TitleDrop;
            AddColumnLabel(Translate("WALLPAPER MODE"), y, bigText: true, bobIndex: -1);
            y -= StatusDrop;
            statusLabel = AddColumnLabel(string.Empty, y, bigText: false, bobIndex: 0);
            statusLabel.label.color = MenuRGB(MenuColors.MediumGrey);
            layoutTop = y - StatusBand;

            AddGroupHeader("TRAVEL");
            campaignRow = AddCyclerCard("Campaign", SignalCampaignPrev, SignalCampaignNext,
                "Choose which slugcat campaign the wallpaper tours. Applied with TRAVEL.");
            regionRow = AddCyclerCard("Region", SignalRegionPrev, SignalRegionNext,
                "Choose the region to travel to. Applied with TRAVEL.");
            roomRow = AddCyclerCard("Room", SignalRoomPrev, SignalRoomNext,
                "Choose a room to jump to in the current region. Applied with TRAVEL.");
            cameraRow = AddCyclerCard("Camera", SignalCameraPrev, SignalCameraNext,
                "Choose how camera positions are picked inside a room. Applied with TRAVEL.");
            rotStateRow = AddCyclerCard("Watcher world", SignalRotStatePrev, SignalRotStateNext,
                "Choose the Watcher world state: natural mix, clean, rotted, karma flowers or both. Applies right away.");
            AddToggleCard("Lock room", IdLockRoom,
                "Stay in this room and hold off the region change until it is unlocked.");

            // The note about when creature changes apply shares the header line, on the right.
            float creaturesY = AddGroupHeader("CREATURES");
            AddHeaderLabel(Translate("Creature changes apply in the next region"), creaturesY, alignRight: true);
            AddToggleCard("Chaos creatures", IdChaos,
                "Spawn extra creatures into the rooms you visit.");
            chaosLevelRow = AddCyclerCard("Chaos level", SignalChaosLevelPrev, SignalChaosLevelNext,
                "How often creatures appear and how many can be alive at once.");
            AddToggleCard("Spawn every creature (experimental)", IdChaosSpawnAll,
                "Experimental: also spawns creature types that are normally left out.");

            AddGroupHeader("DISPLAY");
            alwaysShowHudBox = AddToggleCard("Always show HUD", IdAlwaysShowHud,
                "Keep the HUD on screen instead of fading it out.");
            AddToggleCard("No Rain Wait", IdNoRainWait,
                "Change region at 95% of the rain cycle instead of waiting for the rain.");

            AddBottomButtons();
        }

        private void AddBottomButtons()
        {
            // Where the pause menu keeps CONTINUE and EXIT: the bottom right, the buttons 140 pixels apart. They sit on
            // the column's right edge here, so they line up with the cards.
            float closeX = columnRight - ButtonWidth;
            float travelX = closeX - ButtonGap - ButtonWidth;

            SimpleButton travelButton = new SimpleButton(this, pages[0], Translate("TRAVEL"), SignalTravel,
                new Vector2(travelX, bottomButtonsY), new Vector2(ButtonWidth, ButtonHeight));
            SimpleButton closeButton = new SimpleButton(this, pages[0], Translate("CLOSE"), SignalClose,
                new Vector2(closeX, bottomButtonsY), new Vector2(ButtonWidth, ButtonHeight));
            pages[0].subObjects.Add(travelButton);
            pages[0].subObjects.Add(closeButton);

            // The buttons arrive with the last card and bob in phase with it.
            AddFloater(travelButton, (cardCount - 1) * FloatingMotion.StaggerTicks, cardCount - 1);
            AddFloater(closeButton, (cardCount - 1) * FloatingMotion.StaggerTicks, cardCount - 1);
            descriptions[travelButton] = "Apply the campaign, region, room and camera selection, then close.";
            descriptions[closeButton] = "Close this menu without travelling.";
            selectableRows.Add(new MenuObject[] { travelButton, closeButton });

            // The back input (B, or the UI cancel key) selects the CLOSE button.
            backObject = closeButton;
        }

        // A label centred over the column, which slides in at once.
        private MenuLabel AddColumnLabel(string text, float centerY, bool bigText, int bobIndex)
        {
            MenuLabel label = new MenuLabel(this, pages[0], text, new Vector2(columnLeft, centerY), new Vector2(ColumnWidth, 0f), bigText);
            pages[0].subObjects.Add(label);
            AddFloater(label, 0, bobIndex);
            return label;
        }

        // Registers something that floats. Its resting place is the position it has now, so call this once it is
        // where it belongs.
        private void AddFloater(PositionedMenuObject obj, int delay, int bobIndex)
        {
            floaters.Add(new FloatingItem { Obj = obj, Rest = obj.pos, Delay = delay, BobIndex = bobIndex });
        }

        // Moves the layout cursor down past a small group header and returns the y of the header's text.
        private float AddGroupHeader(string text)
        {
            layoutTop -= GroupGap;
            float centerY = layoutTop - HeaderBand / 2f;
            layoutTop -= HeaderBand;

            AddHeaderLabel(Translate(text), centerY, alignRight: false);
            return centerY;
        }

        private void AddHeaderLabel(string text, float centerY, bool alignRight)
        {
            float x = alignRight ? columnRight - HeaderInset : columnLeft + HeaderInset;
            MenuLabel label = new MenuLabel(this, pages[0], text, new Vector2(x, centerY), Vector2.zero, bigText: false);
            label.label.alignment = alignRight ? FLabelAlignment.Right : FLabelAlignment.Left;
            label.label.color = MenuRGB(MenuColors.DarkGrey);
            pages[0].subObjects.Add(label);

            // A header arrives with the first card of its group, which is the next card made, and bobs in phase with it.
            AddFloater(label, cardCount * FloatingMotion.StaggerTicks, cardCount);
        }

        // The next card down the column. Its parts are positioned relative to it. Cards slide in from the top, one
        // after the next, and each bobs with a phase of its own.
        private FloatingCard AddCard()
        {
            layoutTop -= CardHeight;
            FloatingCard card = new FloatingCard(this, pages[0], new Vector2(columnLeft, layoutTop), new Vector2(ColumnWidth, CardHeight));
            layoutTop -= CardGap;
            pages[0].subObjects.Add(card);

            AddFloater(card, cardCount * FloatingMotion.StaggerTicks, cardCount);
            cardCount++;
            return card;
        }

        private CyclerRow AddCyclerCard(string labelText, string prevSignal, string nextSignal, string description)
        {
            FloatingCard card = AddCard();

            // The arrows and the value share the right side of the card; the name takes the left.
            float groupLeft = ColumnWidth - CardPaddingRight - CyclerWidth;
            float valueLeft = groupLeft + ArrowWidth;
            float valueRight = groupLeft + CyclerWidth - ArrowWidth;
            float arrowY = (CardHeight - ArrowHeight) / 2f;
            float labelY = CardHeight / 2f + LabelNudge;

            CyclerRow row = new CyclerRow
            {
                Name = new MenuLabel(this, card, Translate(labelText), new Vector2(CardPaddingLeft, labelY), Vector2.zero, bigText: false),
                Prev = AddArrowButton(card, "pageleft", prevSignal, new Vector2(groupLeft, arrowY)),
                Next = AddArrowButton(card, "pageright", nextSignal, new Vector2(valueRight, arrowY)),
                ValueWidth = valueRight - valueLeft - 8f
            };

            row.Name.label.alignment = FLabelAlignment.Left;
            row.Name.label.color = MenuRGB(MenuColors.MediumGrey);
            card.subObjects.Add(row.Name);

            row.Value = new MenuLabel(this, card, string.Empty, new Vector2(valueLeft, labelY), new Vector2(valueRight - valueLeft, 0f), bigText: false);
            row.Value.label.color = MenuRGB(MenuColors.White);
            card.subObjects.Add(row.Value);

            descriptions[row.Prev] = description;
            descriptions[row.Next] = description;
            selectableRows.Add(new MenuObject[] { row.Prev, row.Next });
            return row;
        }

        private SymbolButton AddArrowButton(FloatingCard card, string symbolName, string signal, Vector2 pos)
        {
            SymbolButton button = new SymbolButton(this, card, symbolName, signal, pos);

            // Resized the way the game's own page arrows are.
            button.size = new Vector2(ArrowWidth, ArrowHeight);
            button.roundedRect.size = button.size;
            card.subObjects.Add(button);
            card.Controls.Add(button);
            return button;
        }

        private CheckBox AddToggleCard(string labelText, string idString, string description)
        {
            FloatingCard card = AddCard();

            // The checkbox draws its own label to its left, so that label starts at the card's left padding.
            float boxLeft = ColumnWidth - CardPaddingRight - CheckBoxSize;
            CheckBox box = new CheckBox(this, card, this, new Vector2(boxLeft, (CardHeight - CheckBoxSize) / 2f),
                boxLeft - CardPaddingLeft, Translate(labelText), idString);
            card.subObjects.Add(box);
            card.Controls.Add(box);

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

        // The controls sit inside cards, so their own pos is relative; ScreenPos adds the owners' positions.
        private static float CenterX(MenuObject obj)
        {
            RectangularMenuObject rect = obj as RectangularMenuObject;
            return rect != null ? rect.ScreenPos.x + rect.size.x / 2f : 0f;
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

            // The objects stored their previous positions during the base update, so moving them now makes the next
            // draw interpolate from the old position to the new one.
            if (closeQueued)
            {
                exitTicks++;
            }

            UpdateMotion();
            ticks++;

            // Closing is done here, at the end of the update, the way the game's own side-process dialogs do it. When
            // the menu closes itself it first slides out, and closes on the tick after the last step of the slide was
            // reached, which is when that step has been drawn. A process switch freezes the menu's input, so it closes
            // the menu at once. F1 and Escape stop the menu from outside, also at once, without coming through here.
            bool slideOutDone = closeQueued && exitTicks > FloatingMotion.ExitTicks;
            if (!closeRaised && (slideOutDone || manager.upcomingProcess != null))
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

            // Once the menu is on its way out it takes no more orders. Without this a second press during the slide-out
            // would travel twice.
            if (closeQueued)
            {
                return;
            }

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
                    BeginClose();
                    break;

                case SignalClose:
                    BeginClose();
                    break;
            }
        }

        #endregion

        #region Motion

        // Starts the slide-out; the menu closes when it is over. A travel that changes the region has already ended the
        // menu by now (the region reload closes it), and then there is nothing left to slide.
        private void BeginClose()
        {
            if (isShutDown || closeQueued)
            {
                return;
            }

            closeQueued = true;
            exitTicks = 0;
        }

        // Puts every floating item where its slide-in starts, before the first update, so that the first frame that is
        // drawn already has everything off the screen. The links between the controls were made while they rested.
        private void PlaceOffScreen()
        {
            for (int i = 0; i < floaters.Count; i++)
            {
                FloatingItem item = floaters[i];
                Vector2 start = item.Rest + new Vector2(slideDistance, 0f);
                item.Obj.pos = start;
                item.Obj.lastPos = start;
            }
        }

        // Moves every floating item to where it belongs on this tick: sliding in, bobbing, or sliding out.
        private void UpdateMotion()
        {
            float exitProgress = closeQueued ? FloatingMotion.ExitProgress(exitTicks) : 0f;
            float exitTravelled = FloatingMotion.ExitTravelled(exitProgress);

            for (int i = 0; i < floaters.Count; i++)
            {
                FloatingItem item = floaters[i];
                float x = slideDistance * (FloatingMotion.EnterRemaining(ticks, item.Delay) + exitTravelled);
                float y = FloatingMotion.Bob(ticks, item.Delay, item.BobIndex, exitProgress);
                item.Obj.pos = item.Rest + new Vector2(x, y);
            }

            // The shadow does not move; it fades in with the cards and out with them.
            float shadowIn = Custom.SCurve(Mathf.Clamp01(ticks / (float)ShadowFadeTicks), 0.8f);
            shadow.alpha = ShadowAlpha * shadowIn * (1f - exitProgress);
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
