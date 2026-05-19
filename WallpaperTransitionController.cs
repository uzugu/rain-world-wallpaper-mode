using System;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    internal sealed class WallpaperTransitionController
    {
        private readonly RainWorldGame game;
        private readonly WallpaperRoomTracker roomTracker;
        private readonly RegionManager regionManager;
        private readonly EchoMusicManager echoMusic;

        private float transitionDuration = 5f;
        private Vector2 startPosition;
        private Vector2 targetPosition;

        public WallpaperTransitionController(
            RainWorldGame game,
            WallpaperRoomTracker roomTracker,
            RegionManager regionManager,
            EchoMusicManager echoMusic)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.roomTracker = roomTracker ?? throw new ArgumentNullException(nameof(roomTracker));
            this.regionManager = regionManager;
            this.echoMusic = echoMusic;
        }

        public void SetTransitionDuration(float value)
        {
            transitionDuration = value;
        }

        public bool StartTransitionToRandomRoom(WallpaperSessionState sessionState)
        {
            if (game.world == null || game.world.abstractRooms == null || game.world.abstractRooms.Length == 0)
            {
                WallpaperMod.Log?.LogWarning("WallpaperController: World not ready, cannot transition");
                return false;
            }

            AbstractRoom selectedRoom = roomTracker.ShouldStayInCurrentRoom()
                ? roomTracker.CurrentTargetRoom
                : roomTracker.SelectRandomRoom(game.world.abstractRooms);

            if (selectedRoom == null)
            {
                WallpaperMod.Log?.LogWarning("WallpaperController: Failed to select next room");
                return false;
            }

            var primaryCamera = game.cameras[0];
            startPosition = primaryCamera.pos;

            if (selectedRoom.realizedRoom == null)
            {
                selectedRoom.RealizeRoom(game.world, game);
            }

            bool isNewRoom = selectedRoom != roomTracker.CurrentTargetRoom;
            if (selectedRoom.realizedRoom?.cameraPositions != null && selectedRoom.realizedRoom.cameraPositions.Length > 0)
            {
                int cameraIndex = roomTracker.SelectCameraPosition(selectedRoom.realizedRoom.cameraPositions.Length, isNewRoom);
                targetPosition = selectedRoom.realizedRoom.cameraPositions[cameraIndex];
            }
            else
            {
                targetPosition = startPosition;
            }

            roomTracker.BeginTransition(selectedRoom, isNewRoom);
            sessionState.BeginTransition();

            WallpaperMod.Log?.LogInfo($"WallpaperController: Transitioning to room {selectedRoom.name}");
            return true;
        }

        public bool StartTransitionToSpecificRoom(AbstractRoom targetRoom, WallpaperSessionState sessionState)
        {
            if (targetRoom == null || game.cameras == null || game.cameras.Length == 0)
            {
                return false;
            }

            var primaryCamera = game.cameras[0];
            startPosition = primaryCamera.pos;

            if (targetRoom.realizedRoom == null)
            {
                targetRoom.RealizeRoom(game.world, game);
            }

            bool isNewRoom = targetRoom != roomTracker.CurrentTargetRoom;
            if (targetRoom.realizedRoom?.cameraPositions != null && targetRoom.realizedRoom.cameraPositions.Length > 0)
            {
                int cameraIndex = roomTracker.SelectCameraPosition(targetRoom.realizedRoom.cameraPositions.Length, isNewRoom);
                targetPosition = targetRoom.realizedRoom.cameraPositions[cameraIndex];
            }
            else
            {
                targetPosition = startPosition;
            }

            roomTracker.BeginTransition(targetRoom, isNewRoom);
            sessionState.BeginTransition();

            WallpaperMod.Log?.LogInfo($"WallpaperController: Transitioning to specific room {targetRoom.name}");
            return true;
        }

        public void UpdateTransition(float dt, WallpaperSessionState sessionState)
        {
            if (game.cameras == null || game.cameras.Length == 0 || game.cameras[0] == null)
            {
                return;
            }

            float progress = sessionState.AdvanceTransition(dt, transitionDuration);
            float easedProgress = EaseInOutCubic(progress);

            var camera = game.cameras[0];
            camera.pos = Vector2.Lerp(startPosition, targetPosition, easedProgress);

            if (progress >= 1f)
            {
                CompleteTransition(camera, sessionState);
            }
        }

        public void ForceImmediateLocationChange(WallpaperSessionState sessionState)
        {
            if (game.cameras == null || game.cameras.Length == 0)
            {
                return;
            }

            var camera = game.cameras[0];
            if (camera == null)
            {
                return;
            }

            if (sessionState.IsTransitioning)
            {
                sessionState.ForceCompleteTransition();
                CompleteTransition(camera, sessionState);
                return;
            }

            if (StartTransitionToRandomRoom(sessionState))
            {
                sessionState.ForceCompleteTransition();
                CompleteTransition(camera, sessionState);
            }
        }

        public void SyncManualCameraPosition(Vector2 cameraPosition)
        {
            targetPosition = cameraPosition;
            startPosition = cameraPosition;
        }

        public bool CompleteTransition(RoomCamera camera, WallpaperSessionState sessionState)
        {
            sessionState.CompleteTransition();

            bool isNewRoom = false;
            if (roomTracker.CurrentTargetRoom?.realizedRoom != null)
            {
                camera.MoveCamera(roomTracker.CurrentTargetRoom.realizedRoom, roomTracker.CurrentCameraPositionIndex);
                camera.pos = targetPosition;
                isNewRoom = roomTracker.CompleteTransition();
            }

            if (roomTracker.PreviousRoom?.realizedRoom != null && roomTracker.PreviousRoom != roomTracker.CurrentTargetRoom)
            {
                roomTracker.PreviousRoom.Abstractize();
            }

            if (isNewRoom)
            {
                regionManager?.OnRoomExplored();
            }

            echoMusic?.OnRoomChanged(roomTracker.CurrentTargetRoom);
            return isNewRoom;
        }

        private float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }
    }
}
