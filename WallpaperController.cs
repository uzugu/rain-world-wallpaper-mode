using System;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Drives the wallpaper experience inside a live RainWorldGame instance
    /// </summary>
    public class WallpaperController
    {
        public RegionManager RegionManager { get; }
        public RainWorldGame Game { get; }
        public EchoMusicManager EchoMusic { get; private set; }
        public ChaosManager ChaosManager { get; private set; }
        public WallpaperHUD Hud => uiController.Hud;

        // Transition settings
        private float transitionDuration = 5f;
        private float stayDuration = 15f;

        // Location tracking
        private string currentRegionCode;

        // Room lock
        private bool isRoomLocked = false;

        private readonly System.Random random = new System.Random();
        private bool axisSkipActive;
        private readonly WallpaperInputState inputState = new WallpaperInputState();

        // Rot state tracking for Watcher campaign
        private WallpaperModOptions.RotState currentRotState = WallpaperModOptions.RotState.Natural;
        private readonly WallpaperRainCycleController rainCycleController;
        private readonly WallpaperRoomTracker roomTracker;
        private readonly WallpaperUiController uiController = new WallpaperUiController();
        private readonly WallpaperSpectatorState spectatorState = new WallpaperSpectatorState();
        private readonly WallpaperSessionState sessionState = new WallpaperSessionState();
        private readonly WallpaperTransitionController transitionController;
        private readonly WatcherWorldStateController watcherWorldState = new WatcherWorldStateController();

        public WallpaperController(RainWorldGame game, string startRegion)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            rainCycleController = new WallpaperRainCycleController(random);
            roomTracker = new WallpaperRoomTracker(random);

            currentRegionCode = startRegion;
            RegionManager = new RegionManager(this, startRegion);
            EchoMusic = new EchoMusicManager(game);
            ChaosManager = new ChaosManager(game, this);
            transitionController = new WallpaperTransitionController(game, roomTracker, RegionManager, EchoMusic);

            // Load settings from config
            if (WallpaperMod.Options != null)
            {
                transitionDuration = WallpaperMod.Options.TransitionDuration.Value;
                stayDuration = WallpaperMod.Options.StayDuration.Value;
                transitionController.SetTransitionDuration(transitionDuration);
                roomTracker.SetCameraMode(WallpaperModOptions.GetCameraMode(WallpaperMod.Options.CameraModeConfig.Value));

                // Load rot state from config
                currentRotState = WallpaperModOptions.GetRotState(WallpaperMod.Options.RotStateConfig.Value);
                WallpaperMod.Log?.LogInfo($"WallpaperController: Rot state set to {currentRotState}");

                // Initialize chaos if enabled
                if (WallpaperMod.Options.EnableChaos.Value)
                {
                    int chaosLevel = WallpaperMod.Options.ChaosLevel.Value;
                    ChaosManager.EnableChaos(chaosLevel);
                }
            }

            WallpaperMod.Log?.LogInfo($"WallpaperController: Initialized (start region: {currentRegionCode}, camera mode: {WallpaperModOptions.GetCameraMode(WallpaperMod.Options?.CameraModeConfig.Value ?? WallpaperModOptions.CameraMode.RandomExploration.ToString())})");
        }

        /// <summary>
        /// Called every RainWorldGame.Update tick
        /// </summary>
        public void Update(float dt)
        {
            HandleCriticalInput();

            if (Game == null || Game.cameras == null || Game.cameras.Length == 0)
            {
                return;
            }

            EnsureSpectatorState();
            if (!CanRunWallpaperSession())
            {
                return;
            }

            bool watcherCampaign = IsWatcherCampaign();
            if (watcherCampaign)
            {
                SyncWatcherWorldState();
                watcherWorldState.Update(Game, currentRotState, dt);
            }
            uiController.EnsureInitialized(GetPrimaryCamera(), this);
            sessionState.TryStartExploration(Game.world, stayDuration);
            HandleInput();
            AdvanceSession(dt);

            if (watcherCampaign && roomTracker.HasNewRoomSinceLastCheck())
            {
                watcherWorldState.ApplyToCurrentRoom(Game, currentRotState);
            }

            EchoMusic?.Update();
            SyncChaosRuntimeState();
            ChaosManager?.Update(dt);
            uiController.Hud?.Update();
        }

        /// <summary>
        /// Called from RoomCamera.Update so we can clamp camera behaviour during transitions
        /// </summary>
        public void OnCameraUpdate(RoomCamera camera)
        {
            if (camera == null)
            {
                return;
            }

            EnsureSpectatorState();

            if (!spectatorState.IsPrepared)
            {
                return;
            }

            uiController.EnsureHudInitialized(camera, this);

            if (sessionState.IsTransitioning && camera.followAbstractCreature == null)
            {
                // Camera position handled during transition update
            }
        }

        public void Shutdown()
        {
            watcherWorldState.ClearSessionState(Game);
            uiController.Shutdown();
            RegionManager?.Cleanup();
            EchoMusic?.Shutdown();
            ChaosManager?.Shutdown();
            axisSkipActive = false;
            inputState.Reset();
            rainCycleController.Clear();

            roomTracker.Reset();
            spectatorState.Reset();
            sessionState.Reset();

            WallpaperMod.Log?.LogInfo("WallpaperController: Shutdown complete");
        }

        private void HandleCriticalInput()
        {
            bool pauseButton = Input.GetKey(KeyCode.Escape);
            if (inputState.WasPressed(WallpaperInputButton.Pause, pauseButton))
            {
                WallpaperMod.Log?.LogInfo("WallpaperController: ESC pressed, returning to main menu");
                if (Game?.manager != null)
                {
                    Game.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                }
            }

            bool toggleOverlayButton = Input.GetKey(KeyCode.F1) || Input.GetKey(KeyCode.Tab);
            if (inputState.WasPressed(WallpaperInputButton.ToggleOverlay, toggleOverlayButton))
            {
                uiController.ToggleSettingsMenu(GetPrimaryCamera(), this);
            }
        }

        private void ResetRainTracking()
        {
            rainCycleController.ResetForRegion(Game);
        }

        private void OnRainRegionChange()
        {
            WallpaperMod.Log?.LogInfo("Rain World Wallpaper Mode] Rain countdown complete, changing region...");

            string nextRegion = RegionManager?.GetRandomUnvisitedRegion();

            if (nextRegion != null)
            {
                // Found an unvisited region in current campaign
                WallpaperMod.Log?.LogInfo($"Rain World Wallpaper Mode] Switching to unvisited region: {nextRegion}");
                RegionManager?.SetCurrentRegion(nextRegion);
            }
            else
            {
                // All regions visited - cycle to next campaign
                WallpaperMod.Log?.LogInfo("Rain World Wallpaper Mode] All regions explored, cycling to next campaign...");
                WallpaperMod.Instance?.AdvanceToNextCampaign();
            }
        }

        private void HandleInput()
        {
            RegisterUserActivityFromAnyInput();
            bool settingsActive = uiController.IsSettingsActive;
            var settingsOverlay = uiController.SettingsOverlay;

            if (settingsActive)
            {
                HandleOverlayInput(settingsOverlay);
            }
            else
            {
                HandleNormalInput();
            }

            bool nextRoomButton = Input.GetKey(KeyCode.N);
            if (inputState.WasPressed(WallpaperInputButton.NextRoom, nextRoomButton))
            {
                uiController.RegisterUserActivity();
                ForceImmediateLocationChange();
            }

            bool regionForwardButton = Input.GetKey(KeyCode.G);
            if (!settingsActive && inputState.WasPressed(WallpaperInputButton.RegionForward, regionForwardButton) && !sessionState.IsPreparingWorldReload)
            {
                uiController.RegisterUserActivity();
                RegionManager?.AdvanceToNextRegion();
            }

            bool regionBackButton = Input.GetKey(KeyCode.B);
            if (inputState.WasPressed(WallpaperInputButton.RegionBack, regionBackButton) && !sessionState.IsPreparingWorldReload)
            {
                uiController.RegisterUserActivity();
                RegionManager?.AdvanceToPreviousRegion();
            }

            bool lockButton = Input.GetKey(KeyCode.L);
            if (inputState.WasPressed(WallpaperInputButton.Lock, lockButton))
            {
                ToggleRoomLock();
            }

            bool camUpButton = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            if (inputState.WasPressed(WallpaperInputButton.Up, camUpButton))
            {
                CycleCameraPosition(1);
            }

            bool camDownButton = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            if (inputState.WasPressed(WallpaperInputButton.Down, camDownButton))
            {
                CycleCameraPosition(-1);
            }

            bool navLeftButton = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            if (inputState.WasPressed(WallpaperInputButton.Left, navLeftButton))
            {
                GoToPreviousRoom();
            }
        }

        private bool CanRunWallpaperSession()
        {
            if (!sessionState.IsPreparingWorldReload && spectatorState.IsPrepared)
            {
                return true;
            }

            if (sessionState.IsPreparingWorldReload)
            {
                WallpaperMod.Log?.LogDebug("WallpaperController: Waiting for region reload");
            }

            return false;
        }

        private void AdvanceSession(float dt)
        {
            sessionState.TickStayTimer(dt, isRoomLocked);

            if (rainCycleController.Update(Game, dt, isRoomLocked))
            {
                OnRainRegionChange();
            }

            if (sessionState.ShouldAutoTransition(stayDuration, isRoomLocked))
            {
                StartTransitionToRandomRoom();
                return;
            }

            if (sessionState.IsTransitioning)
            {
                UpdateTransition(dt);
            }
        }

        private void SyncChaosRuntimeState()
        {
            if (WallpaperMod.Options == null || ChaosManager == null)
            {
                return;
            }

            bool chaosEnabledInSettings = WallpaperMod.Options.EnableChaos.Value;
            bool chaosCurrentlyActive = ChaosManager.IsEnabled;

            if (chaosEnabledInSettings && !chaosCurrentlyActive)
            {
                ChaosManager.EnableChaos(WallpaperMod.Options.ChaosLevel.Value);
                return;
            }

            if (!chaosEnabledInSettings && chaosCurrentlyActive)
            {
                ChaosManager.DisableChaos();
            }
        }

        private void RegisterUserActivityFromAnyInput()
        {
            bool anyKeyPressed = Input.anyKeyDown
                || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(1)
                || Input.GetMouseButtonDown(2);

            if (anyKeyPressed)
            {
                uiController.RegisterUserActivity();
            }
        }

        private void HandleOverlayInput(WallpaperSettingsOverlay settingsOverlay)
        {
            bool overlayDirty = false;

            if (inputState.WasPressed(WallpaperInputButton.Up, Input.GetKey(KeyCode.UpArrow)))
            {
                settingsOverlay?.CycleFocus(-1);
                overlayDirty = true;
            }

            if (inputState.WasPressed(WallpaperInputButton.Down, Input.GetKey(KeyCode.DownArrow)))
            {
                settingsOverlay?.CycleFocus(1);
                overlayDirty = true;
            }

            if (inputState.WasPressed(WallpaperInputButton.Right, Input.GetKey(KeyCode.RightArrow)))
            {
                settingsOverlay?.CycleCurrentSelection(1);
                overlayDirty = true;
            }

            if (inputState.WasPressed(WallpaperInputButton.Left, Input.GetKey(KeyCode.LeftArrow)))
            {
                settingsOverlay?.CycleCurrentSelection(-1);
                overlayDirty = true;
            }

            bool enterButton = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.G);
            if (inputState.WasPressed(WallpaperInputButton.Enter, enterButton))
            {
                settingsOverlay?.ApplyTravel();
                uiController.ToggleSettingsMenu(GetPrimaryCamera(), this);
            }

            if (TryToggleHud(showLogMessage: false))
            {
                overlayDirty = true;
            }

            if (overlayDirty)
            {
                settingsOverlay?.Refresh();
            }
        }

        private void HandleNormalInput()
        {
            TryToggleHud(showLogMessage: true);

            bool nextRoomRequested = inputState.WasPressed(
                WallpaperInputButton.Right,
                Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D));

            float horizontalAxis = 0f;
            try
            {
                horizontalAxis = Input.GetAxisRaw("Horizontal");
            }
            catch
            {
            }

            if (horizontalAxis > 0.6f)
            {
                if (!axisSkipActive)
                {
                    nextRoomRequested = true;
                    axisSkipActive = true;
                }
            }
            else if (horizontalAxis < 0.2f && horizontalAxis > -0.2f)
            {
                axisSkipActive = false;
            }

            if (nextRoomRequested)
            {
                uiController.RegisterUserActivity();
                ForceImmediateLocationChange();
            }
        }

        private bool TryToggleHud(bool showLogMessage)
        {
            bool hudToggleButton = Input.GetKey(KeyCode.H);
            if (!inputState.WasPressed(WallpaperInputButton.HudToggle, hudToggleButton) || Hud == null)
            {
                return false;
            }

            bool newValue = !Hud.AlwaysShowHUD;
            Hud.SetAlwaysShowHUD(newValue);
            uiController.RegisterUserActivity();
            uiController.RefreshOverlay();

            if (showLogMessage)
            {
                WallpaperMod.Log?.LogInfo($"WallpaperController: Always show HUD toggled {(newValue ? "ON" : "OFF")}");
            }

            return true;
        }

        private void EnsureSpectatorState()
        {
            if (!spectatorState.EnsurePrepared(Game, roomTracker))
            {
                return;
            }

            currentRegionCode = Game?.world?.name ?? currentRegionCode;
            ResetRainTracking();
            sessionState.MarkWorldReady(stayDuration);
        }

        private void StartTransitionToRandomRoom()
        {
            if (sessionState.IsPreparingWorldReload || !spectatorState.IsPrepared)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Game.world?.name))
            {
                currentRegionCode = Game.world.name;
            }

            transitionController.StartTransitionToRandomRoom(sessionState);
        }

        private void StartTransitionToSpecificRoom(AbstractRoom targetRoom)
        {
            if (sessionState.IsPreparingWorldReload || !spectatorState.IsPrepared)
            {
                return;
            }

            transitionController.StartTransitionToSpecificRoom(targetRoom, sessionState);
        }

        private void UpdateTransition(float dt)
        {
            transitionController.UpdateTransition(dt, sessionState);
        }

        private void SyncWatcherWorldState()
        {
            if (WallpaperMod.Options == null)
            {
                return;
            }

            var configuredState = WallpaperModOptions.GetRotState(WallpaperMod.Options.RotStateConfig.Value);
            if (configuredState == currentRotState)
            {
                return;
            }

            currentRotState = configuredState;
            watcherWorldState.ApplyToCurrentRoom(Game, currentRotState);
            WallpaperMod.Log?.LogInfo($"WallpaperController: Watcher world state changed to {currentRotState}");
        }

        internal void BeforeRoomLoaded(Room room)
        {
            if (IsWatcherCampaign())
            {
                watcherWorldState.ApplyBeforeRoomLoaded(Game, room, currentRotState);
            }
        }

        internal void AfterRoomLoaded(Room room)
        {
            if (IsWatcherCampaign())
            {
                watcherWorldState.ApplyAfterRoomLoaded(Game, room, currentRotState);
            }
        }

        private void CompleteTransition(RoomCamera camera)
        {
            bool isNewRoom = transitionController.CompleteTransition(camera, sessionState);
            if (isNewRoom)
            {
                roomTracker.MarkNewRoom();
            }
        }

        internal void OnRegionChanged(string regionCode)
        {
            WallpaperMod.Log?.LogInfo($"WallpaperController: Region changed request {regionCode}");

            if (Game?.manager == null)
            {
                return;
            }

            currentRegionCode = regionCode;

            if (!spectatorState.IsPrepared || Game.world == null)
            {
                return;
            }

            roomTracker.Reset();

            if (!string.Equals(Game.world.name, regionCode, StringComparison.OrdinalIgnoreCase))
            {
                PrepareForWorldReload();
                WallpaperMod.Instance.QueueRegionReload(Game.manager, regionCode);
            }
            else
            {
                sessionState.MarkWorldReady(stayDuration);
                // Reset rain tracking for new region
                ResetRainTracking();
                // Notify chaos manager of region change (even if staying in same region)
                ChaosManager?.OnRegionChanged();
            }
        }

        internal void PrepareForWorldReload()
        {
            watcherWorldState.ClearSessionState(Game);
            sessionState.PrepareForReload();
            roomTracker.Reset();
            uiController.ResetForReload();
            axisSkipActive = false;
            inputState.Reset();
            rainCycleController.Clear();
            spectatorState.Reset();

            // Cleanup chaos manager for region reload
            ChaosManager?.Shutdown();
        }

        private void ForceImmediateLocationChange()
        {
            if (sessionState.IsPreparingWorldReload || !spectatorState.IsPrepared || Game?.cameras == null || Game.cameras.Length == 0)
            {
                return;
            }

            transitionController.ForceImmediateLocationChange(sessionState);
        }
        public string CurrentRegionCode => currentRegionCode ?? string.Empty;

        public string CurrentRoomName => roomTracker.CurrentRoomName;

        public string PreviousRoomName => roomTracker.PreviousRoomName;

        public string NextRoomName => roomTracker.NextRoomName;

        public int RoomsExploredInRegion => RegionManager?.GetRoomsExplored() ?? 0;

        public int RegionsExplored => RegionManager?.GetRegionsExplored() ?? 0;

        public int TotalRegions => RegionManager?.GetTotalRegions() ?? 0;

        public string NextRegionCode => RegionManager?.GetNextRegion() ?? string.Empty;

        public string PreviousRegionCode => RegionManager?.GetPreviousRegion() ?? string.Empty;

        /// <summary>
        /// Rain countdown timer - shows remaining seconds until region change (0 if not active)
        /// </summary>
        public float RainCountdownRemaining => rainCycleController.RainCountdownRemaining;

        /// <summary>
        /// Whether rain countdown is currently active
        /// </summary>
        public bool IsRainCountdownActive => rainCycleController.IsRainCountdownActive;

        /// <summary>
        /// Whether No Rain Wait mode is enabled
        /// </summary>
        public bool IsNoRainWaitMode => rainCycleController.IsNoRainWaitMode;

        /// <summary>
        /// Current rain cycle progress (0.0 to 1.0)
        /// </summary>
        public float CycleProgress => rainCycleController.GetCycleProgress(Game);

        public bool IsTransitioning => sessionState.IsTransitioning;

        public RegionManager RegionMgr => RegionManager;

        /// <summary>
        /// Request a region change from the overlay
        /// </summary>
        public void RequestRegionChange(string regionCode)
        {
            if (string.IsNullOrEmpty(regionCode))
            {
                return;
            }

            WallpaperMod.Log?.LogInfo($"WallpaperController: Region change requested to {regionCode}");

            // Update region manager to point to this region. ForceRegion calls OnRegionChanged.
            RegionManager?.ForceRegion(regionCode);
        }

        /// <summary>
        /// Set the camera mode from the overlay
        /// </summary>
        public void SetCameraMode(WallpaperModOptions.CameraMode mode)
        {
            roomTracker.SetCameraMode(mode);
            WallpaperMod.Log?.LogInfo($"WallpaperController: Camera mode set to {mode}");
        }

        /// <summary>
        /// Toggle room lock (prevents automatic room transitions)
        /// </summary>
        public void ToggleRoomLock()
        {
            isRoomLocked = !isRoomLocked;
            WallpaperMod.Log?.LogInfo($"WallpaperController: Room lock {(isRoomLocked ? "ON" : "OFF")}");
            uiController.RegisterUserActivity();
            uiController.RefreshOverlay();
        }

        /// <summary>
        /// Request a jump to a specific room
        /// </summary>
        public void RequestRoomChange(string roomName)
        {
            if (string.IsNullOrEmpty(roomName) || roomName == "Random")
            {
                return;
            }

            if (Game?.world?.abstractRooms == null)
            {
                WallpaperMod.Log?.LogWarning($"WallpaperController: Cannot change to room {roomName}, world not ready");
                return;
            }

            // Find the room
            AbstractRoom targetRoom = null;
            foreach (var room in Game.world.abstractRooms)
            {
                if (room != null && string.Equals(room.name, roomName, StringComparison.OrdinalIgnoreCase))
                {
                    targetRoom = room;
                    break;
                }
            }

            if (targetRoom == null)
            {
                WallpaperMod.Log?.LogWarning($"WallpaperController: Room {roomName} not found in current region");
                return;
            }

            WallpaperMod.Log?.LogInfo($"WallpaperController: Jumping to room {roomName}");

            // Force immediate transition to this room
            if (sessionState.IsTransitioning)
            {
                // Complete current transition first
                sessionState.ForceCompleteTransition();
                CompleteTransition(Game.cameras[0]);
            }

            StartTransitionToSpecificRoom(targetRoom);
        }

        public bool IsRoomLocked => isRoomLocked;

        /// <summary>
        /// Go to the previous room in history
        /// </summary>
        public void GoToPreviousRoom()
        {
            if (!roomTracker.TryPopPreviousRoom(out string previousRoomName))
            {
                WallpaperMod.Log?.LogInfo("WallpaperController: No previous room in history");
                return;
            }
            
            WallpaperMod.Log?.LogInfo($"WallpaperController: Going back to {previousRoomName}");
            RequestRoomChange(previousRoomName);
        }

        /// <summary>
        /// Cycle through camera positions in the current room
        /// </summary>
        public void CycleCameraPosition(int direction)
        {
            if (Game?.cameras == null || Game.cameras.Length == 0 || Game.cameras[0].room == null)
            {
                return;
            }

            var camera = Game.cameras[0];
            var room = camera.room;
            int totalPositions = room.cameraPositions.Length;

            if (totalPositions <= 1)
            {
                return;
            }

            if (!roomTracker.TryCycleCameraPosition(totalPositions, direction, out int cameraIndex))
            {
                return;
            }

            WallpaperMod.Log?.LogInfo($"WallpaperController: Cycling camera to position {cameraIndex + 1}/{totalPositions}");
            
            camera.MoveCamera(room, cameraIndex);
            
            // Update target position so we don't drift back if transitioning
            // And update start position to avoid jumps
            transitionController.SyncManualCameraPosition(camera.pos);
            
            uiController.RegisterUserActivity();
        }

        private RoomCamera GetPrimaryCamera()
        {
            return Game?.cameras != null && Game.cameras.Length > 0 ? Game.cameras[0] : null;
        }

        private bool IsWatcherCampaign()
        {
            return string.Equals(WallpaperMod.Options?.SelectedCampaign.Value, "Watcher", StringComparison.OrdinalIgnoreCase);
        }
    }
}
