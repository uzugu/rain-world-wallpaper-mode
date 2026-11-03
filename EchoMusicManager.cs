using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Music;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Manages echo music triggers for wallpaper mode based on distance to the echo room
    /// </summary>
    public class EchoMusicManager
    {
        private readonly RainWorldGame game;
        private AbstractRoom echoRoom;  // The room with the echo
        private bool echoMusicPlaying;

        // Room distance settings
        private const int MAX_ROOM_DISTANCE = 3;  // Can hear up to 3 rooms away

        // Reflection state for the AudioHighPassFilter check, resolved once and reused every tick
        private static bool filterLookupDone;
        private static Type highPassFilterType;  // Null if the type cannot be resolved
        private static PropertyInfo cutoffFrequencyProperty;
        private static Type cameraFieldsType;  // Camera type the two arrays below were built from
        private static FieldInfo[] cameraGameObjectFields;
        private static string[] cameraGameObjectLabels;  // "Camera.<field>" per entry, used in log lines

        // Map of region codes to their echo song IDs
        private static readonly Dictionary<string, string> RegionEchoMusic = new Dictionary<string, string>
        {
            { "SH", "NA_32 - Else1" },              // Shaded Citadel - Echo 1
            { "DS", "NA_33 - Else2" },              // Drainage System - Echo 2
            { "CC", "NA_34 - Else3" },              // Chimney Canopy - Echo 3
            { "SI", "NA_35 - Else4" },              // Sky Islands - Echo 4
            { "LF", "NA_36 - Else5" },              // Farm Arrays - Echo 5
            { "SB", "NA_37 - Else6" },              // Subterranean - Echo 6
            { "UW", "NA_38 - Else7" },              // Submerged Superstructure - Echo 7
            { "SL", "NA_42 - Else8" }               // Shoreline - Echo 8
        };

        public EchoMusicManager(RainWorldGame game)
        {
            this.game = game;
            echoRoom = null;
            echoMusicPlaying = false;

            WallpaperMod.Log?.LogInfo("EchoMusicManager: Initialized - will disable audio filter only");
        }

        /// <summary>
        /// Update - continuously disable Ghost's audio filter to prevent distortion
        /// </summary>
        public void Update()
        {
            EnsureFilterLookup();
            if (highPassFilterType == null || game?.cameras == null || game.cameras.Length == 0)
            {
                return;
            }

            // Check all possible locations for AudioHighPassFilter
            // Ghost can apply filters to various audio sources

            // 1. Check musicPlayer.gameObj
            if (game.manager?.musicPlayer?.gameObj != null)
            {
                DisableAudioFilters(game.manager.musicPlayer.gameObj, "MusicPlayer");
            }

            // 2. Check the GameObject-typed fields of the camera (found by reflection, cached per type)
            var camera = game.cameras[0];
            if (camera != null)
            {
                var cameraType = camera.GetType();
                if (cameraFieldsType != cameraType)
                {
                    CacheCameraGameObjectFields(cameraType);
                }

                for (int i = 0; i < cameraGameObjectFields.Length; i++)
                {
                    var fieldObj = cameraGameObjectFields[i].GetValue(camera) as GameObject;
                    if (fieldObj != null)
                    {
                        DisableAudioFilters(fieldObj, cameraGameObjectLabels[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Resolves the AudioHighPassFilter type and its cutoffFrequency property once.
        /// The type lives in UnityEngine.AudioModule, which the project does not reference.
        /// </summary>
        private static void EnsureFilterLookup()
        {
            if (filterLookupDone)
            {
                return;
            }

            filterLookupDone = true;
            highPassFilterType = Type.GetType("UnityEngine.AudioHighPassFilter, UnityEngine.AudioModule");
            if (highPassFilterType == null)
            {
                WallpaperMod.Log?.LogDebug("EchoMusicManager: AudioHighPassFilter type not found, skipping filter handling");
                return;
            }

            cutoffFrequencyProperty = highPassFilterType.GetProperty("cutoffFrequency");
            if (cutoffFrequencyProperty == null)
            {
                WallpaperMod.Log?.LogDebug("EchoMusicManager: AudioHighPassFilter.cutoffFrequency not found, cutoff backup skipped");
            }
        }

        /// <summary>
        /// Collects the GameObject-typed instance fields of the camera type, with a log label for each
        /// </summary>
        private static void CacheCameraGameObjectFields(Type cameraType)
        {
            var fields = new List<FieldInfo>();
            var labels = new List<string>();
            foreach (var field in cameraType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(GameObject))
                {
                    fields.Add(field);
                    labels.Add($"Camera.{field.Name}");
                }
            }

            cameraGameObjectFields = fields.ToArray();
            cameraGameObjectLabels = labels.ToArray();
            cameraFieldsType = cameraType;
        }

        private void DisableAudioFilters(GameObject target, string targetName)
        {
            if (target == null)
            {
                return;
            }

            // GetComponent returns the first AudioHighPassFilter on the object, if there is one
            var filter = target.GetComponent(highPassFilterType) as Behaviour;
            if (filter == null || !filter.enabled)
            {
                return;
            }

            // Disable the filter
            filter.enabled = false;
            WallpaperMod.Log?.LogInfo($"EchoMusicManager: Disabled AudioHighPassFilter on {targetName}");

            // Also force cutoff to minimum as backup
            cutoffFrequencyProperty?.SetValue(filter, 10f, null);
        }

        /// <summary>
        /// Check if entering a new room and start or stop echo music based on proximity to the echo
        /// </summary>
        public void OnRoomChanged(AbstractRoom newRoom)
        {
            if (newRoom == null || game == null)
            {
                WallpaperMod.Log?.LogWarning($"EchoMusic: OnRoomChanged called but newRoom or game is null");
                return;
            }

            WallpaperMod.Log?.LogDebug($"EchoMusic: ===== Entered room {newRoom.name} in region {newRoom.world?.name} =====");

            // Check if this room has an echo
            if (FindEcho(newRoom) != null)
            {
                WallpaperMod.Log?.LogDebug($"EchoMusic: Found echo in room {newRoom.name}");
                // Found an echo in this room - set it as the echo room
                if (echoRoom != newRoom)
                {
                    echoRoom = newRoom;
                    PlayEchoMusic(newRoom);
                }
            }
            else if (echoRoom != null)
            {
                // We're not in the echo room, but check if we're nearby
                int distance = GetRoomDistance(newRoom, echoRoom);
                WallpaperMod.Log?.LogDebug($"EchoMusic: Distance from echo room: {distance}");

                if (distance <= MAX_ROOM_DISTANCE)
                {
                    // Within hearing range - start music if not already playing
                    if (!echoMusicPlaying)
                    {
                        PlayEchoMusic(echoRoom);
                    }
                }
                else
                {
                    // Too far away - stop music completely
                    WallpaperMod.Log?.LogDebug($"EchoMusic: Too far away, fading out");
                    if (echoMusicPlaying)
                    {
                        StopEchoMusic();
                        echoRoom = null;
                    }
                }
            }
            else if (echoMusicPlaying)
            {
                // No echo room at all - stop music
                WallpaperMod.Log?.LogInfo($"EchoMusic: No echo room, stopping");
                StopEchoMusic();
            }
        }

        /// <summary>
        /// Calculate room-to-room distance using breadth-first search
        /// </summary>
        private int GetRoomDistance(AbstractRoom from, AbstractRoom to)
        {
            if (from == null || to == null)
            {
                return int.MaxValue;
            }

            if (from == to)
            {
                return 0;
            }

            // BFS to find shortest path
            var visited = new HashSet<AbstractRoom>();
            var queue = new Queue<(AbstractRoom room, int distance)>();
            queue.Enqueue((from, 0));
            visited.Add(from);

            while (queue.Count > 0)
            {
                var (currentRoom, distance) = queue.Dequeue();

                // Check all connections from this room
                if (currentRoom.connections != null)
                {
                    foreach (int connectionIndex in currentRoom.connections)
                    {
                        if (connectionIndex < 0 || currentRoom.world == null)
                        {
                            continue;
                        }

                        AbstractRoom connectedRoom = currentRoom.world.GetAbstractRoom(connectionIndex);
                        if (connectedRoom == null || visited.Contains(connectedRoom))
                        {
                            continue;
                        }

                        // Found the target room
                        if (connectedRoom == to)
                        {
                            return distance + 1;
                        }

                        // Add to queue for further exploration
                        visited.Add(connectedRoom);
                        queue.Enqueue((connectedRoom, distance + 1));
                    }
                }
            }

            // No path found
            return int.MaxValue;
        }

        /// <summary>
        /// Find the Ghost (echo) entity in a room
        /// </summary>
        private Ghost FindEcho(AbstractRoom room)
        {
            if (room?.realizedRoom == null)
            {
                return null;
            }

            // Check for Ghost objects in the room's update list
            foreach (var updatable in room.realizedRoom.updateList)
            {
                if (updatable is Ghost ghost)
                {
                    return ghost;
                }
            }

            return null;
        }

        /// <summary>
        /// Trigger echo music for the given room
        /// </summary>
        private void PlayEchoMusic(AbstractRoom room)
        {
            if (echoMusicPlaying || game?.manager?.musicPlayer == null)
            {
                return;
            }

            string regionCode = room.world?.name;
            if (string.IsNullOrEmpty(regionCode))
            {
                return;
            }

            // Look up the echo music track for this region
            if (RegionEchoMusic.TryGetValue(regionCode, out string songName))
            {
                // Check if this song is already playing
                if (game.manager.musicPlayer.song != null && game.manager.musicPlayer.song.name == songName)
                {
                    WallpaperMod.Log?.LogDebug($"EchoMusicManager: Song '{songName}' already playing, skipping request");
                    echoMusicPlaying = true;
                    return;
                }

                WallpaperMod.Log?.LogInfo($"EchoMusicManager: Playing echo music '{songName}' for region {regionCode}");

                // Request the music player to play the echo track
                // The hook will prevent the Ghost from interfering
                game.manager.musicPlayer.GameRequestsSong(new MusicEvent
                {
                    songName = songName,
                    prio = 10f,  // Lower priority to let it blend
                    fadeInTime = 40f,  // Quick fade in
                    cyclesRest = 0
                });

                echoMusicPlaying = true;
            }
            else
            {
                WallpaperMod.Log?.LogWarning($"EchoMusicManager: No echo music defined for region {regionCode}");
            }
        }

        /// <summary>
        /// Stop echo music when leaving echo rooms
        /// </summary>
        private void StopEchoMusic()
        {
            if (!echoMusicPlaying || game?.manager?.musicPlayer == null)
            {
                return;
            }

            WallpaperMod.Log?.LogInfo("EchoMusicManager: Stopping echo music");

            // Fade out the current song
            game.manager.musicPlayer.FadeOutAllSongs(40f);  // 40 frames fade out
            echoMusicPlaying = false;
        }

        /// <summary>
        /// Cleanup when shutting down
        /// </summary>
        public void Shutdown()
        {
            echoRoom = null;

            WallpaperMod.Log?.LogInfo("EchoMusicManager: Shutdown");
        }
    }
}
