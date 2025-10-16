using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Drives the wallpaper experience inside a live RainWorldGame instance
    /// </summary>
    public class WallpaperController
    {
        public RegionManager RegionManager { get; }
        public WallpaperHUD Hud { get; private set; }
        public RainWorldGame Game { get; }
        public EchoMusicManager EchoMusic { get; private set; }

        // Transition settings
        private float transitionDuration = 5f;
        private float stayDuration = 15f;
        private float currentTimer = 0f;
        private bool isTransitioning = false;

        // Region timing
        private float regionDurationSeconds = 300f;
        private float regionTimerSeconds = 0f;
        private const float REGION_DURATION_MIN = 60f;
        private const float REGION_DURATION_MAX = 1800f;
        private const float REGION_DURATION_STEP = 60f;

        // Camera interpolation
        private Vector2 startPosition;
        private Vector2 targetPosition;
        private float transitionProgress = 0f;

        // Room tracking
        private AbstractRoom currentTargetRoom;
        private AbstractRoom previousRoom;
        private readonly List<string> roomHistory = new List<string>();
        private const int MAX_HISTORY = 10;

        // Location tracking
        private string currentRegionCode;
        private string currentRoomName = string.Empty;
        private string nextRoomName = string.Empty;
        private string previousRoomName = string.Empty;

        // Camera mode
        private WallpaperModOptions.CameraMode cameraMode = WallpaperModOptions.CameraMode.RandomExploration;
        private int currentCameraPositionIndex = 0;

        // RandomExploration mode tracking
        private List<int> unvisitedPositions = new List<int>();
        private int remainingJumps = 0;

        // Room lock
        private bool isRoomLocked = false;

        private bool hasInitializedHud = false;
        private readonly System.Random random = new System.Random();
        private bool spectatorPrepared;
        private bool hasStartedExploration;
        private bool preparingWorldReload;
        private bool settingsMenuVisible;
        private bool hasInitializedSettingsOverlay;
        private WallpaperSettingsOverlay settingsOverlay;
        private bool axisSkipActive;

        // Player cleanup tracking - only check for a few frames after initialization
        private int framesWithoutPlayers = 0;
        private const int PLAYER_CHECK_FRAMES = 30; // Check for ~0.5 seconds at 60fps

        // Button state tracking for reliable input
        private bool lastPauseButton;
        private bool lastToggleOverlayButton;
        private bool lastNextRoomButton;
        private bool lastRegionForwardButton;
        private bool lastRegionBackButton;
        private bool lastHudToggleButton;
        private bool lastUpArrowButton;
        private bool lastDownArrowButton;
        private bool lastLeftArrowButton;
        private bool lastRightArrowButton;
        private bool lastEnterButton;
        private bool lastPlusButton;
        private bool lastMinusButton;
        private bool lastLockButton;

        public WallpaperController(RainWorldGame game, string startRegion)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));

            currentRegionCode = startRegion;
            RegionManager = new RegionManager(this, startRegion);
            EchoMusic = new EchoMusicManager(game);

            // Load settings from config
            if (WallpaperMod.Options != null)
            {
                transitionDuration = WallpaperMod.Options.TransitionDuration.Value;
                stayDuration = WallpaperMod.Options.StayDuration.Value;
                regionDurationSeconds = WallpaperMod.Options.RegionDuration.Value;
                cameraMode = WallpaperModOptions.GetCameraMode(WallpaperMod.Options.CameraModeConfig.Value);
            }

            WallpaperMod.Log?.LogInfo($"WallpaperController: Initialized (start region: {currentRegionCode}, camera mode: {cameraMode})");
        }

        /// <summary>
        /// Called every RainWorldGame.Update tick
        /// </summary>
        public void Update(float dt)
        {
            // Handle critical inputs FIRST, before any early returns
            // This ensures F1/Tab and Escape always work
            HandleCriticalInput();

            if (Game == null || Game.cameras == null || Game.cameras.Length == 0)
            {
                return;
            }

            EnsureSpectatorState();

            // Ghost marking disabled - worldGhost is per-echo, not global
            // Echoes work differently than expected, leaving at karma 10 for now

            if (preparingWorldReload || !spectatorPrepared)
            {
                if (preparingWorldReload)
                {
                    WallpaperMod.Log?.LogDebug("WallpaperController: Waiting for region reload");
                }
                return;
            }

            if (!hasInitializedHud)
            {
                TryInitializeHud(Game.cameras[0]);
            }
            if (!hasInitializedSettingsOverlay)
            {
                TryInitializeSettingsOverlay(Game.cameras[0]);
            }

            if (!hasStartedExploration && Game.world != null && Game.world.abstractRooms != null && Game.world.abstractRooms.Length > 0)
            {
                hasStartedExploration = true;
                currentTimer = stayDuration;
            }

            HandleInput();

            currentTimer += dt;
            regionTimerSeconds += dt;

            if (regionTimerSeconds >= regionDurationSeconds)
            {
                regionTimerSeconds = 0f;
                RegionManager?.AdvanceToNextRegion();
            }

            // Only auto-transition if not locked
            if (!isTransitioning && currentTimer >= stayDuration && !isRoomLocked)
            {
                StartTransitionToRandomRoom();
            }
            else if (isTransitioning)
            {
                UpdateTransition(dt);
            }

            // Update echo music volume
            EchoMusic?.Update();

            Hud?.Update();
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

            if (!spectatorPrepared)
            {
                return;
            }

            if (!hasInitializedHud)
            {
                TryInitializeHud(camera);
            }

            if (isTransitioning && camera.followAbstractCreature == null)
            {
                // Camera position handled during transition update
            }
        }

        public void Shutdown()
        {
            Hud?.Destroy();
            RegionManager?.Cleanup();
            EchoMusic?.Shutdown();
            settingsOverlay?.Destroy();
            settingsOverlay = null;
            hasInitializedSettingsOverlay = false;
            settingsMenuVisible = false;
            axisSkipActive = false;

            roomHistory.Clear();
            currentTargetRoom = null;
            previousRoom = null;
            spectatorPrepared = false;
            hasStartedExploration = false;
            preparingWorldReload = false;
            isTransitioning = false;
            currentTimer = 0f;

            WallpaperMod.Log?.LogInfo("WallpaperController: Shutdown complete");
        }

        private void HandleCriticalInput()
        {
            // Track button states to detect press edges (transitions from unpressed to pressed)
            // This pattern is more reliable than Input.GetKeyDown() alone

            // Escape key - return to main menu
            bool pauseButton = Input.GetKey(KeyCode.Escape);
            if (pauseButton && !lastPauseButton)
            {
                WallpaperMod.Log?.LogInfo("WallpaperController: ESC pressed, returning to main menu");
                if (Game?.manager != null)
                {
                    Game.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
                }
            }
            lastPauseButton = pauseButton;

            // F1/Tab for settings overlay
            bool toggleOverlayButton = Input.GetKey(KeyCode.F1) || Input.GetKey(KeyCode.Tab);
            if (toggleOverlayButton && !lastToggleOverlayButton)
            {
                ToggleSettingsMenu();
            }
            lastToggleOverlayButton = toggleOverlayButton;
        }

        private void HandleInput()
        {
            bool anyKeyPressed = Input.anyKeyDown
                || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(1)
                || Input.GetMouseButtonDown(2);

            if (anyKeyPressed)
            {
                Hud?.RegisterUserActivity();
            }

            // Note: Escape and F1/Tab are handled in HandleCriticalInput()
            // which runs before any early returns in Update()

            bool settingsActive = settingsMenuVisible && settingsOverlay != null && settingsOverlay.IsVisible;
            float baseStep = REGION_DURATION_STEP;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                baseStep *= 5f;
            }

            if (settingsActive)
            {
                bool overlayDirty = false;

                // Quick travel controls (Up/Down to switch focus, Left/Right to cycle, Enter/G to travel)
                bool upArrowButton = Input.GetKey(KeyCode.UpArrow);
                if (upArrowButton && !lastUpArrowButton)
                {
                    settingsOverlay?.CycleFocus(-1);
                    overlayDirty = true;
                }
                lastUpArrowButton = upArrowButton;

                bool downArrowButton = Input.GetKey(KeyCode.DownArrow);
                if (downArrowButton && !lastDownArrowButton)
                {
                    settingsOverlay?.CycleFocus(1);
                    overlayDirty = true;
                }
                lastDownArrowButton = downArrowButton;

                bool rightArrowButton = Input.GetKey(KeyCode.RightArrow);
                if (rightArrowButton && !lastRightArrowButton)
                {
                    settingsOverlay?.CycleCurrentSelection(1);
                    overlayDirty = true;
                }
                lastRightArrowButton = rightArrowButton;

                bool leftArrowButton = Input.GetKey(KeyCode.LeftArrow);
                if (leftArrowButton && !lastLeftArrowButton)
                {
                    settingsOverlay?.CycleCurrentSelection(-1);
                    overlayDirty = true;
                }
                lastLeftArrowButton = leftArrowButton;

                // Apply travel with Enter or G
                bool enterButton = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.G);
                if (enterButton && !lastEnterButton)
                {
                    settingsOverlay?.ApplyTravel();
                    ToggleSettingsMenu(); // Close overlay after applying
                }
                lastEnterButton = enterButton;

                // H key - toggle HUD
                bool hudToggleButton = Input.GetKey(KeyCode.H);
                if (hudToggleButton && !lastHudToggleButton)
                {
                    if (Hud != null)
                    {
                        bool newValue = !Hud.AlwaysShowHUD;
                        Hud.SetAlwaysShowHUD(newValue);
                        Hud.RegisterUserActivity();
                        overlayDirty = true;
                    }
                }
                lastHudToggleButton = hudToggleButton;

                if (overlayDirty)
                {
                    settingsOverlay?.Refresh();
                }
            }
            else
            {
                // H key - toggle HUD
                bool hudToggleButton = Input.GetKey(KeyCode.H);
                if (hudToggleButton && !lastHudToggleButton)
                {
                    if (Hud != null)
                    {
                        bool newValue = !Hud.AlwaysShowHUD;
                        Hud.SetAlwaysShowHUD(newValue);
                        Hud.RegisterUserActivity();
                        WallpaperMod.Log?.LogInfo($"WallpaperController: Always show HUD toggled {(newValue ? "ON" : "OFF")}");
                        settingsOverlay?.Refresh();
                    }
                }
                lastHudToggleButton = hudToggleButton;

                bool nextRoomRequested = false;

                // Right Arrow/D key with state tracking
                bool rightArrowButton = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
                if (rightArrowButton && !lastRightArrowButton)
                {
                    nextRoomRequested = true;
                }
                lastRightArrowButton = rightArrowButton;

                // Controller axis handling (already has debouncing via axisSkipActive)
                float horizontalAxis = 0f;
                try
                {
                    horizontalAxis = Input.GetAxisRaw("Horizontal");
                }
                catch
                {
                    // Axis not configured; ignore
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
                    Hud?.RegisterUserActivity();
                    ForceImmediateLocationChange();
                }
            }

            // N key - next room
            bool nextRoomButton = Input.GetKey(KeyCode.N);
            if (nextRoomButton && !lastNextRoomButton)
            {
                Hud?.RegisterUserActivity();
                ForceImmediateLocationChange();
            }
            lastNextRoomButton = nextRoomButton;

            // G key - next region
            bool regionForwardButton = Input.GetKey(KeyCode.G);
            if (regionForwardButton && !lastRegionForwardButton && !preparingWorldReload)
            {
                Hud?.RegisterUserActivity();
                regionTimerSeconds = 0f;
                RegionManager?.AdvanceToNextRegion();
            }
            lastRegionForwardButton = regionForwardButton;

            // B key - previous region
            bool regionBackButton = Input.GetKey(KeyCode.B);
            if (regionBackButton && !lastRegionBackButton && !preparingWorldReload)
            {
                Hud?.RegisterUserActivity();
                regionTimerSeconds = 0f;
                RegionManager?.AdvanceToPreviousRegion();
            }
            lastRegionBackButton = regionBackButton;

            // +/= or PageUp - increase region duration
            bool plusButton = Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.PageUp);
            if (plusButton && !lastPlusButton)
            {
                AdjustRegionDuration(baseStep);
            }
            lastPlusButton = plusButton;

            // - or PageDown - decrease region duration
            bool minusButton = Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.PageDown);
            if (minusButton && !lastMinusButton)
            {
                AdjustRegionDuration(-baseStep);
            }
            lastMinusButton = minusButton;

            // L key - toggle room lock
            bool lockButton = Input.GetKey(KeyCode.L);
            if (lockButton && !lastLockButton)
            {
                ToggleRoomLock();
            }
            lastLockButton = lockButton;
        }

        private void EnsureSpectatorState()
        {
            if (Game?.world == null)
            {
                return;
            }

            if (Game.cameras == null || Game.cameras.Length == 0)
            {
                return;
            }

            // Always detach camera from creatures
            foreach (var camera in Game.cameras)
            {
                if (camera != null)
                {
                    camera.followAbstractCreature = null;
                }
            }

            // Check for and remove player entities for the first ~30 frames after initialization
            // This catches players that spawn during world load without checking forever
            if (framesWithoutPlayers < PLAYER_CHECK_FRAMES)
            {
                if (Game.Players != null && Game.Players.Count > 0)
                {
                    foreach (var abstractPlayer in Game.Players)
                    {
                        if (abstractPlayer?.realizedCreature is global::Player realizedPlayer)
                        {
                            realizedPlayer.RemoveFromRoom();
                            realizedPlayer.Destroy();
                        }
                    }
                    Game.Players.Clear();
                    framesWithoutPlayers = 0; // Reset counter if we found players
                }
                else
                {
                    framesWithoutPlayers++;
                }
            }

            // Only run initialization logic once
            if (spectatorPrepared)
            {
                return;
            }

            roomHistory.Clear();
            currentRoomName = string.Empty;
            nextRoomName = string.Empty;
            previousRoomName = string.Empty;

            currentRegionCode = Game.world.name ?? currentRegionCode;

            // Temporarily disabled to test natural echo spawning
            // EnableEchoSpawning();

            spectatorPrepared = true;
            currentTimer = stayDuration;
            regionTimerSeconds = 0f;
            preparingWorldReload = false;
        }

        /// <summary>
        /// Set karma to maximum to allow echoes (ghosts) to spawn in wallpaper mode.
        /// Echoes are now confirmed working with just karma=10, karmaCap=10, and cycleNumber=5.
        /// </summary>
        private void EnableEchoSpawning()
        {
            // Check if echo spawning is enabled in config
            if (WallpaperMod.Options != null && !WallpaperMod.Options.EnableEchoes.Value)
            {
                WallpaperMod.Log?.LogInfo("WallpaperController: Echo spawning disabled in config");
                return;
            }

            // Access the session from the game
            if (Game?.session is StoryGameSession storySession &&
                storySession.saveState?.deathPersistentSaveData != null)
            {
                // Set karma to maximum (10) to allow all echoes to spawn
                storySession.saveState.deathPersistentSaveData.karma = 10;
                storySession.saveState.deathPersistentSaveData.karmaCap = 10;

                // Set cycle number to 5+ to simulate having played for several cycles
                // Echoes require this to bypass the sleep requirement after priming
                if (storySession.saveState.cycleNumber < 5)
                {
                    storySession.saveState.cycleNumber = 5;
                }

                WallpaperMod.Log?.LogInfo($"WallpaperController: Echo spawning enabled (Karma: 10/10, Cycle: {storySession.saveState.cycleNumber})");
            }
            else
            {
                WallpaperMod.Log?.LogWarning("WallpaperController: Could not enable echoes - not a story session or save state unavailable");
            }
        }

        private void TryInitializeHud(RoomCamera camera)
        {
            if (camera == null || hasInitializedHud)
            {
                return;
            }

            // HUD no longer needs camera.hud - it adds to Futile.stage directly
            var hud = new WallpaperHUD(camera, this);
            if (!hud.IsReady)
            {
                return;
            }

            Hud = hud;
            hasInitializedHud = true;
            Hud.RegisterUserActivity();
            WallpaperMod.Log?.LogInfo("WallpaperController: HUD initialized successfully");
        }

        private void TryInitializeSettingsOverlay(RoomCamera camera)
        {
            if (camera == null || hasInitializedSettingsOverlay)
            {
                return;
            }

            // Settings overlay also adds to Futile.stage directly
            settingsOverlay = new WallpaperSettingsOverlay(camera, this);
            hasInitializedSettingsOverlay = true;
            WallpaperMod.Log?.LogInfo("WallpaperController: Settings overlay initialized successfully");
        }

        private void StartTransitionToRandomRoom()
        {
            if (preparingWorldReload)
            {
                return;
            }

            if (!spectatorPrepared)
            {
                return;
            }

            if (Game.world == null || Game.world.abstractRooms == null || Game.world.abstractRooms.Length == 0)
            {
                WallpaperMod.Log?.LogWarning("WallpaperController: World not ready, cannot transition");
                return;
            }

            if (!string.IsNullOrEmpty(Game.world.name))
            {
                currentRegionCode = Game.world.name;
            }

            // Check if we should stay in the current room
            bool shouldStayInCurrentRoom = false;

            if (cameraMode == WallpaperModOptions.CameraMode.Sequential &&
                currentTargetRoom != null &&
                currentTargetRoom.realizedRoom != null &&
                currentTargetRoom.realizedRoom.cameraPositions != null)
            {
                int totalPositions = currentTargetRoom.realizedRoom.cameraPositions.Length;
                if (totalPositions > 1 && currentCameraPositionIndex < totalPositions - 1)
                {
                    shouldStayInCurrentRoom = true;
                }
            }
            else if (cameraMode == WallpaperModOptions.CameraMode.RandomExploration &&
                     remainingJumps > 0 &&
                     unvisitedPositions.Count > 0 &&
                     currentTargetRoom != null)
            {
                shouldStayInCurrentRoom = true;
            }

            AbstractRoom selectedRoom;
            if (shouldStayInCurrentRoom)
            {
                selectedRoom = currentTargetRoom;
            }
            else
            {
                selectedRoom = SelectRandomRoom(Game.world.abstractRooms);
                if (selectedRoom == null)
                {
                    WallpaperMod.Log?.LogWarning("WallpaperController: Failed to select next room");
                    return;
                }
            }

            var primaryCamera = Game.cameras[0];
            startPosition = primaryCamera.pos;

            if (selectedRoom.realizedRoom == null)
            {
                selectedRoom.RealizeRoom(Game.world, Game);
            }

            bool isNewRoom = selectedRoom != currentTargetRoom;
            if (selectedRoom.realizedRoom != null &&
                selectedRoom.realizedRoom.cameraPositions != null &&
                selectedRoom.realizedRoom.cameraPositions.Length > 0)
            {
                int camIndex = SelectCameraPosition(selectedRoom.realizedRoom.cameraPositions.Length, isNewRoom);
                targetPosition = selectedRoom.realizedRoom.cameraPositions[camIndex];
            }
            else
            {
                targetPosition = startPosition;
            }

            previousRoom = currentTargetRoom;
            previousRoomName = currentRoomName;

            currentTargetRoom = selectedRoom;
            nextRoomName = selectedRoom.name;

            // Only add to history if it's a new room
            if (isNewRoom)
            {
                roomHistory.Add(selectedRoom.name);
                if (roomHistory.Count > MAX_HISTORY)
                {
                    roomHistory.RemoveAt(0);
                }
            }

            isTransitioning = true;
            transitionProgress = 0f;
            currentTimer = 0f;

            WallpaperMod.Log?.LogInfo($"WallpaperController: Transitioning to room {selectedRoom.name}");
        }

        private void StartTransitionToSpecificRoom(AbstractRoom targetRoom)
        {
            if (preparingWorldReload || !spectatorPrepared)
            {
                return;
            }

            if (Game.cameras == null || Game.cameras.Length == 0)
            {
                return;
            }

            var primaryCamera = Game.cameras[0];
            startPosition = primaryCamera.pos;

            if (targetRoom.realizedRoom == null)
            {
                targetRoom.RealizeRoom(Game.world, Game);
            }

            bool isNewRoom = targetRoom != currentTargetRoom;
            if (targetRoom.realizedRoom != null &&
                targetRoom.realizedRoom.cameraPositions != null &&
                targetRoom.realizedRoom.cameraPositions.Length > 0)
            {
                int camIndex = SelectCameraPosition(targetRoom.realizedRoom.cameraPositions.Length, isNewRoom);
                targetPosition = targetRoom.realizedRoom.cameraPositions[camIndex];
            }
            else
            {
                targetPosition = startPosition;
            }

            previousRoom = currentTargetRoom;
            previousRoomName = currentRoomName;

            currentTargetRoom = targetRoom;
            nextRoomName = targetRoom.name;

            // Add to history if it's a new room
            if (isNewRoom && !roomHistory.Contains(targetRoom.name))
            {
                roomHistory.Add(targetRoom.name);
                if (roomHistory.Count > MAX_HISTORY)
                {
                    roomHistory.RemoveAt(0);
                }
            }

            isTransitioning = true;
            transitionProgress = 0f;
            currentTimer = 0f;

            WallpaperMod.Log?.LogInfo($"WallpaperController: Transitioning to specific room {targetRoom.name}");
        }

        private AbstractRoom SelectRandomRoom(AbstractRoom[] rooms)
        {
            var availableRooms = new List<AbstractRoom>();

            foreach (var room in rooms)
            {
                if (room == null || room.gate)
                {
                    continue;
                }

                if (roomHistory.Contains(room.name))
                {
                    continue;
                }

                availableRooms.Add(room);
            }

            if (availableRooms.Count == 0)
            {
                roomHistory.Clear();

                foreach (var room in rooms)
                {
                    if (room != null && !room.gate)
                    {
                        availableRooms.Add(room);
                    }
                }
            }

            if (availableRooms.Count == 0)
            {
                return null;
            }

            return availableRooms[random.Next(availableRooms.Count)];
        }

        private void UpdateTransition(float dt)
        {
            if (Game.cameras == null || Game.cameras.Length == 0 || Game.cameras[0] == null)
            {
                return;
            }

            transitionProgress = Mathf.Min(transitionProgress + dt / transitionDuration, 1f);
            float easedProgress = EaseInOutCubic(transitionProgress);

            var camera = Game.cameras[0];
            camera.pos = Vector2.Lerp(startPosition, targetPosition, easedProgress);

            if (transitionProgress >= 1f)
            {
                CompleteTransition(camera);
            }
        }

        private void CompleteTransition(RoomCamera camera)
        {
            isTransitioning = false;
            currentTimer = 0f;

            bool isNewRoom = false;
            if (currentTargetRoom != null && currentTargetRoom.realizedRoom != null)
            {
                // Use the selected camera position index instead of always 0
                camera.MoveCamera(currentTargetRoom.realizedRoom, currentCameraPositionIndex);
                camera.pos = targetPosition;

                // Only update room name and count if this is actually a new room
                if (currentRoomName != nextRoomName && !string.IsNullOrEmpty(nextRoomName))
                {
                    isNewRoom = true;
                    currentRoomName = nextRoomName;
                }
                nextRoomName = string.Empty;

                // Check for echo music when entering new room
                EchoMusic?.OnRoomChanged(currentTargetRoom);
            }

            if (previousRoom != null && previousRoom.realizedRoom != null && previousRoom != currentTargetRoom)
            {
                previousRoom.Abstractize();
            }

            // Only count as a new room explored if we actually changed rooms
            if (isNewRoom)
            {
                RegionManager?.OnRoomExplored();
            }
        }

        private float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        internal void OnRegionChanged(string regionCode)
        {
            WallpaperMod.Log?.LogInfo($"WallpaperController: Region changed request {regionCode}");

            if (Game?.manager == null)
            {
                return;
            }

            currentRegionCode = regionCode;
            regionTimerSeconds = 0f;

            if (!spectatorPrepared || Game.world == null)
            {
                return;
            }

            roomHistory.Clear();
            currentRoomName = string.Empty;
            nextRoomName = string.Empty;
            previousRoomName = string.Empty;

            if (!string.Equals(Game.world.name, regionCode, StringComparison.OrdinalIgnoreCase))
            {
                PrepareForWorldReload();
                WallpaperMod.Instance.QueueRegionReload(Game.manager, regionCode);
            }
            else
            {
                currentTimer = stayDuration;
            }
        }

        internal void PrepareForWorldReload()
        {
            preparingWorldReload = true;
            isTransitioning = false;
            spectatorPrepared = false;
            hasStartedExploration = false;
            currentTimer = 0f;
            roomHistory.Clear();
            Hud?.Destroy();
            Hud = null;
            hasInitializedHud = false;
            settingsOverlay?.Destroy();
            settingsOverlay = null;
            hasInitializedSettingsOverlay = false;
            settingsMenuVisible = false;
            axisSkipActive = false;
            framesWithoutPlayers = 0; // Reset player check counter for new region
        }

        private void ForceImmediateLocationChange()
        {
            if (preparingWorldReload || !spectatorPrepared || Game?.cameras == null || Game.cameras.Length == 0)
            {
                return;
            }

            var camera = Game.cameras[0];
            if (camera == null)
            {
                return;
            }

            if (isTransitioning)
            {
                transitionProgress = 1f;
                CompleteTransition(camera);
            }
            else
            {
                StartTransitionToRandomRoom();
                if (isTransitioning)
                {
                    transitionProgress = 1f;
                    CompleteTransition(camera);
                }
            }
        }

        private void AdjustRegionDuration(float deltaSeconds)
        {
            float newDuration = Mathf.Clamp(regionDurationSeconds + deltaSeconds, REGION_DURATION_MIN, REGION_DURATION_MAX);
            if (Mathf.Approximately(newDuration, regionDurationSeconds))
            {
                return;
            }

            regionDurationSeconds = newDuration;
            regionTimerSeconds = Mathf.Min(regionTimerSeconds, regionDurationSeconds);

            // Save to config
            if (WallpaperMod.Options != null)
            {
                WallpaperMod.Options.RegionDuration.Value = regionDurationSeconds;
            }

            WallpaperMod.Log?.LogInfo($"WallpaperController: Region duration set to {regionDurationSeconds / 60f:F1} minutes");
            Hud?.RegisterUserActivity();
            settingsOverlay?.Refresh();
        }

        private void ToggleSettingsMenu()
        {
            if (!hasInitializedSettingsOverlay)
            {
                TryInitializeSettingsOverlay(Game?.cameras != null && Game.cameras.Length > 0 ? Game.cameras[0] : null);
            }

            if (settingsOverlay == null)
            {
                return;
            }

            settingsMenuVisible = !settingsMenuVisible;
            settingsOverlay.SetVisible(settingsMenuVisible);
            settingsOverlay.Refresh();

            if (settingsMenuVisible)
            {
                Hud?.RegisterUserActivity();
            }
        }

        public string CurrentRegionCode => currentRegionCode ?? string.Empty;

        public string CurrentRoomName => currentRoomName;

        public string PreviousRoomName => previousRoomName;

        public string NextRoomName => nextRoomName;

        public int RoomsExploredInRegion => RegionManager?.GetRoomsExplored() ?? 0;

        public int RegionsExplored => RegionManager?.GetRegionsExplored() ?? 0;

        public int TotalRegions => RegionManager?.GetTotalRegions() ?? 0;

        public string NextRegionCode => RegionManager?.GetNextRegion() ?? string.Empty;

        public string PreviousRegionCode => RegionManager?.GetPreviousRegion() ?? string.Empty;

        public float RegionTimerSeconds => regionTimerSeconds;

        public float RegionDurationSeconds => regionDurationSeconds;

        public bool IsTransitioning => isTransitioning;

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

            // Update region manager to point to this region
            RegionManager?.ForceRegion(regionCode);

            // Trigger the change
            OnRegionChanged(regionCode);
        }

        /// <summary>
        /// Set the camera mode from the overlay
        /// </summary>
        public void SetCameraMode(WallpaperModOptions.CameraMode mode)
        {
            cameraMode = mode;
            currentCameraPositionIndex = 0;
            unvisitedPositions.Clear();
            remainingJumps = 0;
            WallpaperMod.Log?.LogInfo($"WallpaperController: Camera mode set to {mode}");
        }

        /// <summary>
        /// Toggle room lock (prevents automatic room transitions)
        /// </summary>
        public void ToggleRoomLock()
        {
            isRoomLocked = !isRoomLocked;
            WallpaperMod.Log?.LogInfo($"WallpaperController: Room lock {(isRoomLocked ? "ON" : "OFF")}");
            Hud?.RegisterUserActivity();
            settingsOverlay?.Refresh();
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
            if (isTransitioning)
            {
                // Complete current transition first
                transitionProgress = 1f;
                CompleteTransition(Game.cameras[0]);
            }

            // Set up transition to target room
            currentTargetRoom = targetRoom;
            StartTransitionToSpecificRoom(targetRoom);
        }

        public bool IsRoomLocked => isRoomLocked;

        /// <summary>
        /// Select camera position based on current camera mode
        /// </summary>
        private int SelectCameraPosition(int availablePositions, bool isNewRoom)
        {
            if (availablePositions == 0) return 0;

            switch (cameraMode)
            {
                case WallpaperModOptions.CameraMode.FirstOnly:
                    return 0;

                case WallpaperModOptions.CameraMode.Sequential:
                    // Reset index when entering a new room
                    if (isNewRoom)
                    {
                        currentCameraPositionIndex = 0;
                    }
                    else
                    {
                        currentCameraPositionIndex = (currentCameraPositionIndex + 1) % availablePositions;
                    }
                    return currentCameraPositionIndex;

                case WallpaperModOptions.CameraMode.RandomExploration:
                    if (isNewRoom)
                    {
                        // Entering new room: initialize unvisited positions and pick random start + jump count
                        unvisitedPositions.Clear();
                        for (int i = 0; i < availablePositions; i++)
                        {
                            unvisitedPositions.Add(i);
                        }

                        // Pick random start position
                        int startIndex = random.Next(unvisitedPositions.Count);
                        currentCameraPositionIndex = unvisitedPositions[startIndex];
                        unvisitedPositions.RemoveAt(startIndex);

                        // Decide how many more jumps to make (0 to remaining positions)
                        remainingJumps = unvisitedPositions.Count > 0 ? random.Next(unvisitedPositions.Count + 1) : 0;
                    }
                    else
                    {
                        // Staying in room: pick random from unvisited positions
                        if (unvisitedPositions.Count > 0)
                        {
                            int randomIndex = random.Next(unvisitedPositions.Count);
                            currentCameraPositionIndex = unvisitedPositions[randomIndex];
                            unvisitedPositions.RemoveAt(randomIndex);
                            remainingJumps--;
                        }
                    }
                    return currentCameraPositionIndex;

                case WallpaperModOptions.CameraMode.Random:
                default:
                    return random.Next(availablePositions);
            }
        }
    }
}
