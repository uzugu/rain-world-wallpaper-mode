using System;
using System.Collections.Generic;
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

        // Transition settings
        private float transitionDuration = 5f;
        private float stayDuration = 15f;
        private float currentTimer = 0f;
        private bool isTransitioning = false;

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

        private bool hasInitializedHud = false;
        private readonly System.Random random = new System.Random();
        private bool spectatorPrepared;
        private bool hasStartedExploration;
        private bool preparingWorldReload;

        public WallpaperController(RainWorldGame game, string startRegion)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));

            currentRegionCode = startRegion;
            RegionManager = new RegionManager(this, startRegion);

            WallpaperMod.Log?.LogInfo($"WallpaperController: Initialized (start region: {currentRegionCode})");
        }

        /// <summary>
        /// Called every RainWorldGame.Update tick
        /// </summary>
        public void Update(float dt)
        {
            if (Game == null || Game.cameras == null || Game.cameras.Length == 0)
            {
                return;
            }

            EnsureSpectatorState();

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

            if (!hasStartedExploration && Game.world != null && Game.world.abstractRooms != null && Game.world.abstractRooms.Length > 0)
            {
                hasStartedExploration = true;
                currentTimer = stayDuration;
            }

            HandleInput();

            currentTimer += dt;

            if (!isTransitioning && currentTimer >= stayDuration)
            {
                StartTransitionToRandomRoom();
            }
            else if (isTransitioning)
            {
                UpdateTransition(dt);
            }

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

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                WallpaperMod.Log?.LogInfo("WallpaperController: ESC pressed, returning to main menu");
                Game.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.MainMenu);
            }
        }

        private void EnsureSpectatorState()
        {
            if (spectatorPrepared)
            {
                return;
            }

            if (Game?.world == null)
            {
                return;
            }

            if (Game.cameras == null || Game.cameras.Length == 0)
            {
                return;
            }

            foreach (var camera in Game.cameras)
            {
                if (camera != null)
                {
                    camera.followAbstractCreature = null;
                }
            }

            if (Game.Players != null)
            {
                foreach (var abstractPlayer in Game.Players)
                {
                    if (abstractPlayer?.realizedCreature is global::Player realizedPlayer)
                    {
                        realizedPlayer.RemoveFromRoom();
                    }
                }
            }

            roomHistory.Clear();
            currentRoomName = string.Empty;
            nextRoomName = string.Empty;
            previousRoomName = string.Empty;

            currentRegionCode = Game.world.name ?? currentRegionCode;
            spectatorPrepared = true;
            currentTimer = stayDuration;
            preparingWorldReload = false;
        }

        private void TryInitializeHud(RoomCamera camera)
        {
            if (camera == null || hasInitializedHud)
            {
                return;
            }

            if (camera.hud == null || camera.hud.fContainers == null || camera.hud.fContainers.Length == 0)
            {
                return;
            }

            var hud = new WallpaperHUD(camera, this);
            if (!hud.IsReady)
            {
                return;
            }

            Hud = hud;
            hasInitializedHud = true;
        }

        private void StartTransitionToRandomRoom()
        {
            if (!spectatorPrepared)
            {
                return;
            }

            if (Game.world == null || Game.world.abstractRooms == null || Game.world.abstractRooms.Length == 0)
            {
                WallpaperMod.Log?.LogWarning("WallpaperController: World not ready, cannot transition");
                return;
            }

            WallpaperMod.Log?.LogInfo($"WallpaperController: Preparing new location after {currentTimer:F1}s");

            if (!string.IsNullOrEmpty(Game.world.name))
            {
                currentRegionCode = Game.world.name;
            }

            AbstractRoom selectedRoom = SelectRandomRoom(Game.world.abstractRooms);

            if (selectedRoom == null)
            {
                WallpaperMod.Log?.LogWarning("WallpaperController: Failed to select next room");
                return;
            }

            var primaryCamera = Game.cameras[0];
            startPosition = primaryCamera.pos;

            if (selectedRoom.realizedRoom == null)
            {
                selectedRoom.RealizeRoom(Game.world, Game);
            }

            if (selectedRoom.realizedRoom != null &&
                selectedRoom.realizedRoom.cameraPositions != null &&
                selectedRoom.realizedRoom.cameraPositions.Length > 0)
            {
                int camIndex = random.Next(selectedRoom.realizedRoom.cameraPositions.Length);
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

            roomHistory.Add(selectedRoom.name);
            if (roomHistory.Count > MAX_HISTORY)
            {
                roomHistory.RemoveAt(0);
            }

            isTransitioning = true;
            transitionProgress = 0f;
            currentTimer = 0f;

            WallpaperMod.Log?.LogInfo($"WallpaperController: Transitioning to room {selectedRoom.name}");
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

            if (currentTargetRoom != null && currentTargetRoom.realizedRoom != null)
            {
                camera.MoveCamera(currentTargetRoom.realizedRoom, 0);
                camera.pos = targetPosition;

                currentRoomName = nextRoomName;
                nextRoomName = string.Empty;
            }

            if (previousRoom != null && previousRoom.realizedRoom != null && previousRoom != currentTargetRoom)
            {
                previousRoom.Abstractize();
            }

            RegionManager?.OnRoomExplored();

            WallpaperMod.Log?.LogInfo("WallpaperController: Transition complete");
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
        }

        public string CurrentRegionCode => currentRegionCode ?? string.Empty;

        public string CurrentRoomName => currentRoomName;

        public string PreviousRoomName => previousRoomName;

        public string NextRoomName => nextRoomName;

        public int RoomsExploredInRegion => RegionManager?.GetRoomsExplored() ?? 0;

        public int RegionsExplored => RegionManager?.GetRegionsExplored() ?? 0;

        public int TotalRegions => RegionManager?.GetTotalRegions() ?? 0;

        public int RoomsPerRegion => RegionManager?.RoomsPerRegion ?? 0;

        public bool IsTransitioning => isTransitioning;
    }
}
