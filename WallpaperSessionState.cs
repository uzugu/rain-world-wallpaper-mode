using UnityEngine;

namespace RainWorldWallpaperMod
{
    internal sealed class WallpaperSessionState
    {
        public bool HasStartedExploration { get; private set; }

        public bool IsPreparingWorldReload { get; private set; }

        public bool IsTransitioning { get; private set; }

        public float CurrentTimer { get; private set; }

        public float TransitionProgress { get; private set; }

        public void TryStartExploration(World world, float stayDuration)
        {
            if (HasStartedExploration || world?.abstractRooms == null || world.abstractRooms.Length == 0)
            {
                return;
            }

            HasStartedExploration = true;
            CurrentTimer = stayDuration;
        }

        public void TickStayTimer(float dt, bool isRoomLocked)
        {
            if (!isRoomLocked)
            {
                CurrentTimer += dt;
            }
        }

        public bool ShouldAutoTransition(float stayDuration, bool isRoomLocked)
        {
            return !IsTransitioning && CurrentTimer >= stayDuration && !isRoomLocked;
        }

        public void BeginTransition()
        {
            IsTransitioning = true;
            TransitionProgress = 0f;
            CurrentTimer = 0f;
        }

        public float AdvanceTransition(float dt, float transitionDuration)
        {
            TransitionProgress = Mathf.Min(TransitionProgress + dt / transitionDuration, 1f);
            return TransitionProgress;
        }

        public void ForceCompleteTransition()
        {
            TransitionProgress = 1f;
        }

        public void CompleteTransition()
        {
            IsTransitioning = false;
            CurrentTimer = 0f;
        }

        public void MarkWorldReady(float stayDuration)
        {
            CurrentTimer = stayDuration;
            IsPreparingWorldReload = false;
        }

        public void PrepareForReload()
        {
            IsPreparingWorldReload = true;
            IsTransitioning = false;
            HasStartedExploration = false;
            CurrentTimer = 0f;
            TransitionProgress = 0f;
        }

        public void Reset()
        {
            HasStartedExploration = false;
            IsPreparingWorldReload = false;
            IsTransitioning = false;
            CurrentTimer = 0f;
            TransitionProgress = 0f;
        }
    }
}
