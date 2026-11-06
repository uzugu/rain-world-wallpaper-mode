using System;
using System.Collections.Generic;
using Music;
using UnityEngine;
using Watcher;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Brings echoes to life in a wallpaper session. The game ties them to a player in three places that a session
    /// without one never gets through:
    ///   World.SpawnGhost            creates an echo only for a save that has met it and has the karma for it
    ///   RoomCamera.Update           raises a Spinning Top's shading only while the camera follows a Player
    ///   PlayerThreatTracker.Update  starts an echo's song from where the player is
    /// Here the camera stands in for the player. An awake echo then shades the rooms around it by the game's own measure
    /// (RoomCamera.UpdateGhostMode), and the gold flakes, slowed time and muffled sound follow from that. Once per region
    /// the tour also walks in to the echo, because a random tour can spend a whole region without coming near it.
    /// </summary>
    public class EchoManager
    {
        // How far out the walk to the echo starts. The game fades an echo's presence over three rooms.
        private const int ApproachRooms = 3;
        private const int MinRoomsBeforeVisit = 2;
        private const int MaxRoomsBeforeVisit = 5;
        private const int TicksBetweenStatusLines = 200;

        // The middle of a room screen, as the game measures it in GhostWorldPresence.GhostMode.
        private static readonly Vector2 ScreenCentre = new Vector2(700f, 400f);

        private readonly RainWorldGame game;
        private readonly Queue<AbstractRoom> visit = new Queue<AbstractRoom>();
        private bool visitPlanned;
        private int roomsBeforeVisit;
        private bool faulted;
        private int ticksUntilStatusLine;

        public EchoManager(RainWorldGame game)
        {
            this.game = game;
        }

        /// <summary>
        /// Called after World.SpawnGhost. A wallpaper session plays on a blank save, which has met no echo, so most regions
        /// would only ever show the short GhostHunch flash. Which echoes a campaign's world has stays the game's choice.
        /// </summary>
        internal static void WakeRegionEcho(World world)
        {
            if (world.worldGhost != null || world.region == null ||
                !World.CheckForRegionGhost(world.game.StoryCharacter, world.region.name))
            {
                return;
            }

            var echo = new GhostWorldPresence(world, GhostWorldPresence.GetGhostID(world.region.name));
            if (!WallpaperRoomTracker.IsSafeWallpaperRoom(echo.ghostRoom))
            {
                return;
            }

            world.worldGhost = echo;
            world.migrationInfluences.Add(echo);
            WallpaperMod.Log?.LogInfo($"EchoManager: Woke the echo of {world.region.name} in {echo.ghostRoom.name}");
        }

        /// <summary>
        /// The next room of this region's walk to its echo, or null while the tour is free to go anywhere.
        /// </summary>
        public AbstractRoom NextVisitRoom()
        {
            if (!visitPlanned)
            {
                visitPlanned = true;
                PlanVisit();
            }

            if (roomsBeforeVisit > 0)
            {
                roomsBeforeVisit--;
                return null;
            }

            return visit.Count > 0 ? visit.Dequeue() : null;
        }

        /// <summary>
        /// The screen of a room that shows its echo, or -1. An echo's presence fills the whole room, but the echo itself is
        /// only on one of its screens.
        /// </summary>
        public int FindEchoScreen(Room room)
        {
            Ghost echo = room != null ? FindEcho(room) : null;
            if (echo == null || room.cameraPositions == null)
            {
                return -1;
            }

            int closest = -1;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < room.cameraPositions.Length; i++)
            {
                float distance = Vector2.Distance(room.cameraPositions[i] + ScreenCentre, echo.placedObject.pos);
                if (distance < closestDistance)
                {
                    closest = i;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        /// <summary>
        /// Called after RoomCamera.Update. With a Spinning Top in the room the game leaves ghostMode alone in
        /// UpdateGhostMode and moves it here instead, by the followed player's distance to the Top. A wallpaper camera
        /// follows nobody, so the middle of the screen takes the player's place.
        /// </summary>
        public void OnCameraUpdate(RoomCamera camera)
        {
            if (faulted || !camera.spinningTopGhostMode || camera.room == null)
            {
                return;
            }

            try
            {
                Ghost top = FindEcho(camera.room);
                float target = 0f;
                if (top != null)
                {
                    float distance = Vector2.Distance(top.placedObject.pos, camera.pos + camera.sSize * 0.5f);
                    target = Mathf.Lerp(0.11f, 1f, Mathf.InverseLerp(1500f, 0f, distance));
                }

                camera.targetSpinningTopGhostMode = target;
                camera.ghostMode = Mathf.Lerp(camera.ghostMode, target, 0.06f);
                camera.lightBloomAlpha = camera.ghostMode * 0.8f;
            }
            catch (Exception ex)
            {
                OnFault(ex);
            }
        }

        /// <summary>
        /// Called once per game tick. With no player, PlayerThreatTracker.Update stops at its first check, so the echo part
        /// of it runs here on what the camera sees. GhostSong reads its volume from these same fields, and ends itself a
        /// few seconds after they drop to zero.
        /// </summary>
        public void Update()
        {
            if (faulted)
            {
                return;
            }

            try
            {
                UpdateEchoSong();
            }
            catch (Exception ex)
            {
                OnFault(ex);
            }
        }

        private void UpdateEchoSong()
        {
            RoomCamera camera = game.cameras != null && game.cameras.Length > 0 ? game.cameras[0] : null;
            MusicPlayer music = game.manager?.musicPlayer;
            PlayerThreatTracker tracker = music?.threatTracker;
            if (camera?.room == null || tracker == null)
            {
                return;
            }

            // The same floor the tracker uses, so that the faint edge of a presence does not start a song.
            float presence = camera.ghostMode > (ModManager.MMF ? 0.1f : 0f) ? camera.ghostMode : 0f;
            bool laughing = presence == 0f && camera.spinningTopLaughMode > 0f;
            tracker.ghostMode = presence;
            tracker.spinningTopLaughMode = laughing ? 1f : 0f;
            if (presence == 0f && !laughing)
            {
                return;
            }

            music.FadeOutAllNonGhostSongs(120f);

            ticksUntilStatusLine--;
            if (ticksUntilStatusLine <= 0)
            {
                ticksUntilStatusLine = TicksBetweenStatusLines;
                WallpaperMod.Log?.LogDebug($"EchoManager: Presence {camera.ghostMode:0.00} in {camera.room.abstractRoom.name}, song {music.song?.name ?? "none"}");
            }

            GhostWorldPresence source = FindPresence(camera.room, camera.currentCameraPosition);
            if (source == null)
            {
                return;
            }

            if (ModManager.Watcher && source.ghostID == WatcherEnums.GhostID.SpinningTop)
            {
                music.RequestGhostSong(source.songName, source.spinningTopSpawnId);
            }
            else
            {
                music.RequestGhostSong(source.songName);
            }
        }

        /// <summary>
        /// Ends the session's hold on the song. It then fades out by the game's own rule.
        /// </summary>
        public void Shutdown()
        {
            PlayerThreatTracker tracker = game.manager?.musicPlayer?.threatTracker;
            if (tracker != null)
            {
                tracker.ghostMode = 0f;
                tracker.spinningTopLaughMode = 0f;
            }

            visit.Clear();
        }

        // The walk is planned backwards from the echo, one connected room at a time, so it ends in the echo's room.
        private void PlanVisit()
        {
            roomsBeforeVisit = UnityEngine.Random.Range(MinRoomsBeforeVisit, MaxRoomsBeforeVisit + 1);

            AbstractRoom echoRoom = PickEchoRoom(game.world);
            if (echoRoom == null)
            {
                return;
            }

            var path = new List<AbstractRoom> { echoRoom };
            while (path.Count <= ApproachRooms)
            {
                AbstractRoom next = PickNeighbour(path);
                if (next == null)
                {
                    break;
                }

                path.Add(next);
            }

            for (int i = path.Count - 1; i >= 0; i--)
            {
                visit.Enqueue(path[i]);
            }

            WallpaperMod.Log?.LogInfo($"EchoManager: The tour will walk in to the echo in {echoRoom.name} through {path.Count - 1} rooms");
        }

        private static AbstractRoom PickEchoRoom(World world)
        {
            var rooms = new List<AbstractRoom>();
            if (world.worldGhost != null)
            {
                rooms.Add(world.worldGhost.ghostRoom);
            }

            foreach (GhostWorldPresence top in world.spinningTopPresences)
            {
                // GhostMode is zero for a Top this save cannot meet yet, whatever the position.
                if (top.GhostMode(top.ghostRoom, Vector2.zero) > 0f)
                {
                    rooms.Add(top.ghostRoom);
                }
            }

            rooms.RemoveAll(room => !WallpaperRoomTracker.IsSafeWallpaperRoom(room));
            return rooms.Count > 0 ? rooms[UnityEngine.Random.Range(0, rooms.Count)] : null;
        }

        private static AbstractRoom PickNeighbour(List<AbstractRoom> path)
        {
            AbstractRoom last = path[path.Count - 1];
            var neighbours = new List<AbstractRoom>();
            foreach (int connection in last.connections)
            {
                AbstractRoom neighbour = connection > -1 ? last.world.GetAbstractRoom(connection) : null;
                if (WallpaperRoomTracker.IsSafeWallpaperRoom(neighbour) && !path.Contains(neighbour))
                {
                    neighbours.Add(neighbour);
                }
            }

            return neighbours.Count > 0 ? neighbours[UnityEngine.Random.Range(0, neighbours.Count)] : null;
        }

        private static GhostWorldPresence FindPresence(Room room, int cameraPosition)
        {
            foreach (GhostWorldPresence top in room.world.spinningTopPresences)
            {
                if (top.GhostMode(room, cameraPosition) > 0f || top.LaughMode(room.abstractRoom) > 0f)
                {
                    return top;
                }
            }

            return room.world.worldGhost;
        }

        // A Spinning Top is a Ghost too.
        private static Ghost FindEcho(Room room)
        {
            foreach (UpdatableAndDeletable updatable in room.updateList)
            {
                if (updatable is Ghost ghost)
                {
                    return ghost;
                }
            }

            return null;
        }

        private void OnFault(Exception ex)
        {
            // Echoes are an extra: never let them take the wallpaper down, and do not repeat the error every tick.
            faulted = true;
            WallpaperMod.Log?.LogError($"EchoManager: Stopped after an error, echoes stay quiet until the next region - {ex}");
        }
    }
}
