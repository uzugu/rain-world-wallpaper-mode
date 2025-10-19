using System.Collections.Generic;
using UnityEngine;
using Music;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Manages echo music triggers for wallpaper mode with distance-based volume
    /// </summary>
    public class EchoMusicManager
    {
        private readonly RainWorldGame game;
        private AbstractRoom echoRoom;  // The room with the echo
        private bool echoMusicPlaying;
        private Ghost currentEcho;
        private static EchoMusicManager activeInstance;  // Track active instance for hook

        // Room distance settings
        private const int MAX_ROOM_DISTANCE = 3;  // Can hear up to 3 rooms away

        // Volume control
        private float targetVolume = 0f;
        private float currentVolume = 0f;
        private const float VOLUME_LERP_SPEED = 0.05f;

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
            currentEcho = null;

            activeInstance = this;

            WallpaperMod.Log?.LogInfo("EchoMusicManager: Initialized - will disable audio filter only");
        }

        /// <summary>
        /// Update - continuously disable Ghost's audio filter to prevent distortion
        /// </summary>
        public void Update()
        {
            if (game?.cameras == null || game.cameras.Length == 0)
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

            // 2. Check all AudioSources in the scene for filters
            var camera = game.cameras[0];
            if (camera != null)
            {
                // Use reflection to find all GameObject properties that might have AudioSources
                var cameraType = camera.GetType();
                var fields = cameraType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                foreach (var field in fields)
                {
                    if (field.FieldType == typeof(GameObject))
                    {
                        var fieldObj = field.GetValue(camera) as GameObject;
                        if (fieldObj != null)
                        {
                            DisableAudioFilters(fieldObj, $"Camera.{field.Name}");
                        }
                    }
                }
            }
        }

        private void DisableAudioFilters(GameObject target, string targetName)
        {
            if (target == null)
            {
                return;
            }

            var components = target.GetComponents<UnityEngine.Component>();
            foreach (var component in components)
            {
                if (component != null && component.GetType().Name == "AudioHighPassFilter")
                {
                    var componentType = component.GetType();

                    // Disable the filter
                    var enabledProp = componentType.GetProperty("enabled");
                    if (enabledProp != null)
                    {
                        bool isEnabled = (bool)enabledProp.GetValue(component, null);
                        if (isEnabled)
                        {
                            enabledProp.SetValue(component, false, null);
                            WallpaperMod.Log?.LogInfo($"EchoMusicManager: Disabled AudioHighPassFilter on {targetName}");
                        }
                    }

                    // Also force cutoff to minimum as backup
                    var cutoffProp = componentType.GetProperty("cutoffFrequency");
                    if (cutoffProp != null)
                    {
                        cutoffProp.SetValue(component, 10f, null);
                    }
                }
            }
        }

        /// <summary>
        /// Check if entering a new room and update volume based on proximity to echo
        /// </summary>
        public void OnRoomChanged(AbstractRoom newRoom)
        {
            if (newRoom == null || game == null)
            {
                WallpaperMod.Log?.LogWarning($"EchoMusic: OnRoomChanged called but newRoom or game is null");
                return;
            }

            WallpaperMod.Log?.LogInfo($"EchoMusic: ===== Entered room {newRoom.name} in region {newRoom.world?.name} =====");

            // Check if this room has an echo
            Ghost echo = FindEcho(newRoom);
            if (echo != null)
            {
                WallpaperMod.Log?.LogInfo($"EchoMusic: Found echo in room {newRoom.name}");
                // Found an echo in this room - set it as the echo room
                if (echoRoom != newRoom)
                {
                    echoRoom = newRoom;
                    currentEcho = echo;
                    PlayEchoMusic(newRoom);
                }
                // In the echo room = full volume
                targetVolume = 1f;
                WallpaperMod.Log?.LogInfo($"EchoMusic: In echo room, setting target volume to 1.0");
            }
            else if (echoRoom != null)
            {
                // We're not in the echo room, but check if we're nearby
                int distance = GetRoomDistance(newRoom, echoRoom);
                WallpaperMod.Log?.LogInfo($"EchoMusic: Distance from echo room: {distance}");

                if (distance <= MAX_ROOM_DISTANCE)
                {
                    // Within hearing range - calculate volume based on distance
                    // Distance 0 = same room = 1.0 volume (but we handle that above)
                    // Distance 1 = adjacent = 0.6 volume
                    // Distance 2 = 2 rooms away = 0.3 volume
                    // Distance 3 = 3 rooms away = 0.1 volume
                    float volumeByDistance = 1f - (distance / (float)(MAX_ROOM_DISTANCE + 1));
                    targetVolume = Mathf.Max(0.1f, volumeByDistance);
                    WallpaperMod.Log?.LogInfo($"EchoMusic: Setting target volume to {targetVolume:F2} based on distance");

                    // Start music if not already playing
                    if (!echoMusicPlaying)
                    {
                        PlayEchoMusic(echoRoom);
                    }
                }
                else
                {
                    // Too far away - stop music completely
                    WallpaperMod.Log?.LogInfo($"EchoMusic: Too far away, fading out");
                    targetVolume = 0f;
                    if (echoMusicPlaying && currentVolume < 0.05f)
                    {
                        StopEchoMusic();
                        echoRoom = null;
                        currentEcho = null;
                    }
                }
            }
            else if (echoMusicPlaying && currentVolume < 0.05f)
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
                WallpaperMod.Log?.LogInfo($"EchoMusicManager: Playing echo music '{songName}' for region {regionCode}");

                // Check if this song is already playing
                if (game.manager.musicPlayer.song != null && game.manager.musicPlayer.song.name == songName)
                {
                    WallpaperMod.Log?.LogInfo($"EchoMusicManager: Song '{songName}' already playing, skipping request");
                    echoMusicPlaying = true;
                    return;
                }

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
                currentVolume = 0f;  // Start at 0 volume, will lerp up based on distance
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
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            echoRoom = null;
            currentEcho = null;
            currentVolume = 0f;
            targetVolume = 0f;

            WallpaperMod.Log?.LogInfo("EchoMusicManager: Shutdown");
        }
    }
}
