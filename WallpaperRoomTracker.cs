using System;
using System.Collections.Generic;

namespace RainWorldWallpaperMod
{
    internal sealed class WallpaperRoomTracker
    {
        private const int MaxHistory = 10;

        private readonly System.Random random;
        private readonly List<string> roomHistory = new List<string>();
        private readonly List<int> unvisitedPositions = new List<int>();

        private AbstractRoom currentTargetRoom;
        private AbstractRoom previousRoom;
        private string currentRoomName = string.Empty;
        private string nextRoomName = string.Empty;
        private string previousRoomName = string.Empty;
        private WallpaperModOptions.CameraMode cameraMode = WallpaperModOptions.CameraMode.RandomExploration;
        private int currentCameraPositionIndex;
        private int remainingJumps;

        public WallpaperRoomTracker(System.Random random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public AbstractRoom CurrentTargetRoom => currentTargetRoom;

        public AbstractRoom PreviousRoom => previousRoom;

        public string CurrentRoomName => currentRoomName;

        public string NextRoomName => nextRoomName;

        public string PreviousRoomName => previousRoomName;

        public int CurrentCameraPositionIndex => currentCameraPositionIndex;

        public void Reset()
        {
            roomHistory.Clear();
            unvisitedPositions.Clear();
            currentTargetRoom = null;
            previousRoom = null;
            currentRoomName = string.Empty;
            nextRoomName = string.Empty;
            previousRoomName = string.Empty;
            currentCameraPositionIndex = 0;
            remainingJumps = 0;
        }

        public void SetCameraMode(WallpaperModOptions.CameraMode mode)
        {
            cameraMode = mode;
            currentCameraPositionIndex = 0;
            unvisitedPositions.Clear();
            remainingJumps = 0;
        }

        public bool ShouldStayInCurrentRoom()
        {
            if (cameraMode == WallpaperModOptions.CameraMode.Sequential &&
                currentTargetRoom?.realizedRoom?.cameraPositions != null)
            {
                int totalPositions = currentTargetRoom.realizedRoom.cameraPositions.Length;
                return totalPositions > 1 && currentCameraPositionIndex < totalPositions - 1;
            }

            if (cameraMode == WallpaperModOptions.CameraMode.RandomExploration &&
                remainingJumps > 0 &&
                unvisitedPositions.Count > 0 &&
                currentTargetRoom != null)
            {
                return true;
            }

            return false;
        }

        public AbstractRoom SelectRandomRoom(AbstractRoom[] rooms)
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

        public int SelectCameraPosition(int availablePositions, bool isNewRoom)
        {
            if (availablePositions <= 0)
            {
                currentCameraPositionIndex = 0;
                return 0;
            }

            switch (cameraMode)
            {
                case WallpaperModOptions.CameraMode.FirstOnly:
                    currentCameraPositionIndex = 0;
                    break;

                case WallpaperModOptions.CameraMode.Sequential:
                    if (isNewRoom)
                    {
                        currentCameraPositionIndex = 0;
                    }
                    else
                    {
                        currentCameraPositionIndex = (currentCameraPositionIndex + 1) % availablePositions;
                    }
                    break;

                case WallpaperModOptions.CameraMode.RandomExploration:
                    if (isNewRoom)
                    {
                        unvisitedPositions.Clear();
                        for (int i = 0; i < availablePositions; i++)
                        {
                            unvisitedPositions.Add(i);
                        }

                        int startIndex = random.Next(unvisitedPositions.Count);
                        currentCameraPositionIndex = unvisitedPositions[startIndex];
                        unvisitedPositions.RemoveAt(startIndex);
                        remainingJumps = unvisitedPositions.Count > 0 ? random.Next(unvisitedPositions.Count + 1) : 0;
                    }
                    else if (unvisitedPositions.Count > 0)
                    {
                        int randomIndex = random.Next(unvisitedPositions.Count);
                        currentCameraPositionIndex = unvisitedPositions[randomIndex];
                        unvisitedPositions.RemoveAt(randomIndex);
                        remainingJumps--;
                    }
                    break;

                case WallpaperModOptions.CameraMode.Random:
                default:
                    currentCameraPositionIndex = random.Next(availablePositions);
                    break;
            }

            return currentCameraPositionIndex;
        }

        public void BeginTransition(AbstractRoom targetRoom, bool isNewRoom)
        {
            previousRoom = currentTargetRoom;
            previousRoomName = currentRoomName;
            currentTargetRoom = targetRoom;
            nextRoomName = targetRoom?.name ?? string.Empty;

            if (isNewRoom && targetRoom != null)
            {
                roomHistory.Add(targetRoom.name);
                if (roomHistory.Count > MaxHistory)
                {
                    roomHistory.RemoveAt(0);
                }
            }
        }

        public bool CompleteTransition()
        {
            bool isNewRoom = currentRoomName != nextRoomName && !string.IsNullOrEmpty(nextRoomName);
            if (isNewRoom)
            {
                currentRoomName = nextRoomName;
            }

            nextRoomName = string.Empty;
            return isNewRoom;
        }

        public bool TryPopPreviousRoom(out string roomName)
        {
            roomName = null;
            if (roomHistory.Count < 2)
            {
                return false;
            }

            roomName = roomHistory[roomHistory.Count - 2];
            roomHistory.RemoveAt(roomHistory.Count - 1);
            return true;
        }

        public bool TryCycleCameraPosition(int totalPositions, int direction, out int cameraIndex)
        {
            cameraIndex = currentCameraPositionIndex;
            if (totalPositions <= 1)
            {
                return false;
            }

            currentCameraPositionIndex = (currentCameraPositionIndex + direction) % totalPositions;
            if (currentCameraPositionIndex < 0)
            {
                currentCameraPositionIndex += totalPositions;
            }

            cameraIndex = currentCameraPositionIndex;
            return true;
        }
    }
}
