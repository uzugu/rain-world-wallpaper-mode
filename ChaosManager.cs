using System.Collections.Generic;
using UnityEngine;
using System;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Manages dynamic creature spawning to create a more lively environment
    /// </summary>
    public class ChaosManager
    {
        private readonly RainWorldGame game;
        private readonly WallpaperController controller;

        // Chaos configuration
        private int chaosLevel = 0; // 0 = disabled,1-10 = intensity levels
        private bool chaosEnabled = false;

        // Spawning timing
        private float spawnTimer = 0f;
        private float spawnInterval = 45f; // Seconds between spawns

        // Creature management
        private readonly List<AbstractCreature> spawnedCreatures = new List<AbstractCreature>();
        private int maxCreatures = 50;

        // Region creature analysis
        private readonly List<CreatureTemplate.Type> regionalCreatureTypes = new List<CreatureTemplate.Type>();
        private bool hasAnalyzedRegion = false;
        private float regionLoadTimer = 0f;
        private const float REGION_LOAD_DELAY = 3f; // Wait 3 seconds for world to fully load creatures

        // Weight-based spawn system - prevents too many powerful creatures
        private readonly Dictionary<CreatureTemplate.Type, int> creatureWeightCounters = new Dictionary<CreatureTemplate.Type, int>();

        // Spawn timing by chaos level (1-10)
        // Format: (normal interval, max creatures, ramp-up interval)
        // Balanced levels (5-7) recommended for performance stability
        private static readonly Dictionary<int, (float interval, int maxCreatures, float rampUpInterval)> ChaosLevels = new Dictionary<int, (float, int, float)>
        {
            { 1, (45f, 5, 10f) },     // Level 1: Minimal - 5 creatures max
            { 2, (40f, 8, 9f) },      // Level 2: Light - 8 creatures max
            { 3, (35f, 12, 8f) },     // Level 3: Moderate - 12 creatures max
            { 4, (30f, 18, 7f) },     // Level 4: Active - 18 creatures max
            { 5, (25f, 25, 6f) },     // Level 5: Balanced starting point - 25 creatures max
            { 6, (20f, 35, 5f) },     // Level 6: Getting busy - 35 creatures max
            { 7, (15f, 50, 4f) },     // Level 7: Maximum recommended - 50 creatures max
            { 8, (12f, 70, 3f) },     // Level 8: Heavy - 70 creatures max (may lag)
            { 9, (10f, 100, 2f) },    // Level 9: Extreme - 100 creatures max (risky)
            { 10, (8f, 150, 1f) }     // Level 10: ABSOLUTE CHAOS - 150 creatures max (very risky)
        };

        private float rampUpInterval = 10f; // Fast spawn interval for first half

        // Rain World never has more than four players, and some of its code keeps per-player data in arrays of four:
        // ScavengersWorldAI.Update indexes each scavenger outpost's int[4] killSquads by the position in game.Players.
        // A fifth entry there throws IndexOutOfRangeException on every tick while a region with an outpost (Outskirts
        // has one) is loaded, which stops the rain cycle, the rooms and the wallpaper itself.
        private const int MaxGamePlayers = 4;

        // Creature types excluded from normal chaos mode. Spawn All intentionally bypasses this list.
        private static readonly HashSet<string> BlacklistedCreatures = new HashSet<string>
        {
            "Slugcat",              // Player creatures - cannot be spawned like regular creatures
            "Overseer",             // Special entities that need specific initialization
            "TempleGuard",          // Special scripted creatures
            "DaddyLongLegs",        // Known to cause null reference issues in spectator mode
            "TentaclePlant",        // Stationary, might not work offscreen
            "PoleMimic",            // Stationary, might not work offscreen
            "StowawayBug",          // Tentacle-based creature that crashes when spawned offscreen
            "StandardGroundCreature" // Base class, not an actual creature type
        };

        // Weight system - larger/stronger creatures have higher weights (spawn less often)
        private static readonly Dictionary<string, int> CreatureWeights = new Dictionary<string, int>
        {
            // Weight 2 - Small/Common (every 2 rolls)
            { "Fly", 2 }, { "DropBug", 2 }, { "EggBug", 2 }, { "Snail", 2 }, { "Hazer", 2 }, { "VultureGrub", 2 },
            { "CicadaA", 2 }, { "CicadaB", 2 },
            { "SmallNeedleWorm", 2 }, { "BigNeedleWorm", 2 },
            { "SmallCentipede", 2 }, { "Centipede", 2 },
            { "TubeWorm", 2 }, { "JetFish", 2 },

            // Weight 3 - Standard Lizards & Medium Creatures
            { "PinkLizard", 3 }, { "GreenLizard", 3 }, { "BlueLizard", 3 }, { "WhiteLizard", 3 },
            { "YellowLizard", 3 }, { "BlackLizard", 3 }, { "Salamander", 3 },
            { "BigSpider", 3 }, { "SpitterSpider", 3 },
            { "GarbageWorm", 3 }, { "BigEel", 3 },

            // Weight 3 - Scavengers (increased spawn rate)
            { "Scavenger", 3 },
            { "ScavengerElite", 3 },

            // Weight 1 - Slugpups (Very Frequent Spawn Rate)
            { "SlugNPC", 1 },

            // Weight 4 - Dangerous/Aggressive
            { "RedLizard", 4 }, { "CyanLizard", 4 },
            { "SpitLizard", 4 }, { "EelLizard", 4 }, { "TrainLizard", 4 },
            { "MirosBird", 4 },
            { "Leech", 4 }, { "SeaLeech", 4 },
            { "AquaCenti", 4 },

            // Weight 5 - Elite Tier
            { "RedCentipede", 5 },
            { "MirosVulture", 5 },

            // Weight 8 - Vultures (reduced spawn rate)
            { "Vulture", 8 },

            // Weight 10 - King Vultures (very rare)
            { "KingVulture", 10 },

            // Weight 15 - Ultra Rare Scavengers
            { "ScavengerKing", 15 },

            // Weight 6 - Very Rare
            { "BrotherLongLegs", 6 },

            // Weight 10 - Ultra Rare (only with Spawn All)
            { "DaddyLongLegs", 10 },
            { "TempleGuard", 10 }
        };

        /// <summary>
        /// Get spawn weight for a creature type (higher = spawns less often)
        /// </summary>
        private int GetCreatureWeight(CreatureTemplate.Type type)
        {
            if (CreatureWeights.TryGetValue(type.ToString(), out int weight))
            {
                return weight;
            }
            // Default weight for unknown creatures
            return 3;
        }

        public ChaosManager(RainWorldGame game, WallpaperController controller)
        {
            this.game = game;
            this.controller = controller;

            WallpaperMod.Log?.LogInfo("ChaosManager: Initialized (disabled by default)");
        }

        /// <summary>
        /// Enable chaos mode with specified level (1-10, out-of-range values are clamped)
        /// </summary>
        public void EnableChaos(int level)
        {
            // Clamp instead of rejecting: WallpaperController retries EnableChaos every tick while IsEnabled is false,
            // so rejecting a bad saved level would warn on every tick and chaos would never start
            if (level < 1 || level > 10)
            {
                int clampedLevel = Mathf.Clamp(level, 1, 10);
                WallpaperMod.Log?.LogWarning($"ChaosManager: Chaos level {level} is outside 1-10, using {clampedLevel}");
                level = clampedLevel;
            }

            chaosLevel = level;
            chaosEnabled = true;

            if (ChaosLevels.TryGetValue(level, out var settings))
            {
                spawnInterval = settings.interval;
                maxCreatures = settings.maxCreatures;
                rampUpInterval = settings.rampUpInterval;
            }

            spawnTimer = 0f;
            regionLoadTimer = 0f; // Start load delay timer
            hasAnalyzedRegion = false; // Force re-analysis on next update

            WallpaperMod.Log?.LogInfo($"ChaosManager: Enabled at level {level} (ramp-up every {rampUpInterval}s for first {maxCreatures / 2}, then every {spawnInterval}s, max {maxCreatures} creatures)");
            WallpaperMod.Log?.LogInfo($"ChaosManager: Waiting {REGION_LOAD_DELAY}s for world to fully load before analyzing creatures...");
        }

        /// <summary>
        /// Disable chaos mode
        /// </summary>
        public void DisableChaos()
        {
            chaosEnabled = false;
            chaosLevel = 0;
            CleanupAllCreatures();

            WallpaperMod.Log?.LogInfo("ChaosManager: Disabled");
        }

        /// <summary>
        /// Get current spawn interval based on ramp-up phase
        /// </summary>
        private float GetCurrentSpawnInterval()
        {
            // Use fast ramp-up interval for first half of max creatures
            int halfMax = maxCreatures / 2;
            if (spawnedCreatures.Count < halfMax)
            {
                return rampUpInterval;
            }
            // Use normal interval after reaching half
            return spawnInterval;
        }

        /// <summary>
        /// Update chaos system - spawn creatures based on timer
        /// </summary>
        public void Update(float dt)
        {
            if (!chaosEnabled || game?.world == null)
            {
                return;
            }

            // Wait for world to fully load before analyzing creatures
            if (!hasAnalyzedRegion)
            {
                regionLoadTimer += dt;
                if (regionLoadTimer >= REGION_LOAD_DELAY)
                {
                    WallpaperMod.Log?.LogInfo($"ChaosManager: World loaded for {REGION_LOAD_DELAY}s, analyzing creatures now...");
                    AnalyzeRegionCreatures();
                }
                else
                {
                    // Still waiting for world to load
                    return;
                }
            }

            // Clean up dead/removed creatures
            CleanupDeadCreatures();

            // Spawning logic with dynamic interval (fast ramp-up, then normal)
            float currentInterval = GetCurrentSpawnInterval();
            spawnTimer += dt;
            if (spawnTimer >= currentInterval && spawnedCreatures.Count < maxCreatures)
            {
                SpawnRandomCreature();
                spawnTimer = 0f;
            }
        }

        /// <summary>
        /// Analyze the current region to determine what creatures spawn naturally
        /// </summary>
        private void AnalyzeRegionCreatures()
        {
            regionalCreatureTypes.Clear();

            if (game?.world?.abstractRooms == null)
            {
                WallpaperMod.Log?.LogWarning("ChaosManager: Cannot analyze region - world not ready");
                return;
            }

            bool spawnAll = WallpaperMod.Options?.ChaosSpawnAll.Value ?? false;
            var seenTypes = new HashSet<CreatureTemplate.Type>();
            var blacklistedTypes = new HashSet<string>();

            // Scan all rooms in the region for creature spawners
            foreach (var room in game.world.abstractRooms)
            {
                if (room?.creatures == null)
                {
                    continue;
                }

                foreach (var creature in room.creatures)
                {
                    if (creature?.creatureTemplate?.type != null)
                    {
                        var type = creature.creatureTemplate.type;

                        // Skip if already seen
                        if (seenTypes.Contains(type))
                        {
                            continue;
                        }

                        seenTypes.Add(type);

                        if (!spawnAll && IsBlacklisted(type))
                        {
                            blacklistedTypes.Add(type.ToString());
                            continue;
                        }

                        TryAddCreatureType(type, regionalCreatureTypes, enforceBlacklist: !spawnAll);
                    }
                }
            }

            hasAnalyzedRegion = true;

            // Chaos level 7+: Force-add SlugNPC, Scavenger (all variations), and ScavengerKing to any region
            if (chaosLevel >= 7)
            {
                WallpaperMod.Log?.LogInfo($"ChaosManager: 🔥 Chaos 7+ activated - forcing special creatures into spawn pool");
                WallpaperMod.Log?.LogInfo($"ChaosManager: Downpour DLC enabled = {ModCompatibility.IsDownpourEnabled}");

                // Add vanilla Scavenger
                if (!regionalCreatureTypes.Contains(CreatureTemplate.Type.Scavenger))
                {
                    regionalCreatureTypes.Add(CreatureTemplate.Type.Scavenger);
                    WallpaperMod.Log?.LogInfo($"ChaosManager: ✓ Force-added Scavenger (vanilla)");
                }

                // Add DLC creatures if Downpour is installed
                if (ModCompatibility.IsDownpourEnabled)
                {
                    // Force add SlugNPC (Slugpup)
                    var slugNPC = new CreatureTemplate.Type("SlugNPC", true);
                    if (TryAddCreatureType(slugNPC, regionalCreatureTypes, enforceBlacklist: false))
                    {
                        WallpaperMod.Log?.LogInfo($"ChaosManager: ✓ Force-added SlugNPC (Slugpup)");
                    }

                    var dlcCreatureNames = new[] { "ScavengerElite", "ScavengerKing" };

                    foreach (var creatureName in dlcCreatureNames)
                    {
                        try
                        {
                            // Create the type - constructor with true will register it if it doesn't exist
                            var creatureType = new CreatureTemplate.Type(creatureName, true);

                            if (TryAddCreatureType(creatureType, regionalCreatureTypes, enforceBlacklist: false))
                            {
                                WallpaperMod.Log?.LogInfo($"ChaosManager: ✓ Force-added {creatureName} (DLC)");
                            }
                            else
                            {
                                WallpaperMod.Log?.LogInfo($"ChaosManager: ⊙ {creatureName} already in spawn pool");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            WallpaperMod.Log?.LogError($"ChaosManager: ✗ Failed to add {creatureName}: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                }
                else
                {
                    WallpaperMod.Log?.LogInfo("ChaosManager: Downpour DLC not installed, skipping ScavengerElite/ScavengerKing");
                }
            }

            if (spawnAll)
            {
                WallpaperMod.Log?.LogWarning($"ChaosManager: ⚠️ SPAWN ALL MODE ENABLED - Blacklist disabled! Found {regionalCreatureTypes.Count} creature types:");
            }
            else
            {
                WallpaperMod.Log?.LogInfo($"ChaosManager: Analyzed region {game.world.name}, found {regionalCreatureTypes.Count} spawnable creature types:");
            }

            foreach (var type in regionalCreatureTypes)
            {
                WallpaperMod.Log?.LogInfo($"  ✓ {type}");
            }

            if (!spawnAll && blacklistedTypes.Count > 0)
            {
                WallpaperMod.Log?.LogInfo($"ChaosManager: Filtered out {blacklistedTypes.Count} blacklisted creature types:");
                foreach (var type in blacklistedTypes)
                {
                    WallpaperMod.Log?.LogInfo($"  ✗ {type}");
                }
            }

            if (regionalCreatureTypes.Count == 0)
            {
                WallpaperMod.Log?.LogWarning("ChaosManager: No spawnable creatures found in this region!");
            }
        }

        /// <summary>
        /// Spawn a random creature from the regional pool offscreen (with weight system)
        /// </summary>
        private void SpawnRandomCreature()
        {
            if (regionalCreatureTypes.Count == 0)
            {
                WallpaperMod.Log?.LogWarning("ChaosManager: No creature types available to spawn");
                return;
            }

            if (game?.cameras == null || game.cameras.Length == 0 || game.cameras[0]?.room == null)
            {
                return;
            }

            var camera = game.cameras[0];
            var currentRoom = camera.room;

            // Pick random creature type
            // var random = new System.Random();
            var creatureType = regionalCreatureTypes[UnityEngine.Random.Range(0, regionalCreatureTypes.Count)];

            // Get weight for this creature
            int weight = GetCreatureWeight(creatureType);

            // Initialize counter if needed
            if (!creatureWeightCounters.ContainsKey(creatureType))
            {
                creatureWeightCounters[creatureType] = 0;
            }

            // Increment counter
            creatureWeightCounters[creatureType]++;
            int currentCount = creatureWeightCounters[creatureType];

            // WallpaperMod.Log?.LogInfo($"ChaosManager: Rolled {creatureType} ({currentCount}/{weight})");

            // Check if we've reached threshold
            if (currentCount < weight)
            {
                // Building up - don't spawn yet
                // WallpaperMod.Log?.LogInfo($"ChaosManager: Building up {creatureType}: {currentCount}/{weight}");
                return;
            }

            // Reset counter to 1 (keeping momentum as requested)
            creatureWeightCounters[creatureType] = 1;

            // Pick a random room in the region (not the current room)
            if (game.world.abstractRooms == null || game.world.abstractRooms.Length <= 1)
            {
                WallpaperMod.Log?.LogWarning("ChaosManager: Not enough rooms in region for spawning");
                return;
            }

            AbstractRoom spawnRoom = null;
            
            // Try to spawn in a connected neighbor room first (for better visibility)
            if (currentRoom.abstractRoom.connections != null && currentRoom.abstractRoom.connections.Length > 0)
            {
                try 
                {
                    // Pick a random connection. Connections hold world room indices, which start at the region's
                    // firstRoomIndex rather than 0, so look the rooms up through the world (null for -1 or out of range)
                    var neighbors = currentRoom.abstractRoom.connections;
                    var validNeighbors = new System.Collections.Generic.List<AbstractRoom>();
                    foreach (var n in neighbors)
                    {
                        AbstractRoom neighbor = n > -1 ? game.world.GetAbstractRoom(n) : null;
                        if (neighbor != null && neighbor.index != currentRoom.abstractRoom.index && IsValidChaosSpawnRoom(neighbor))
                        {
                            validNeighbors.Add(neighbor);
                        }
                    }

                    if (validNeighbors.Count > 0)
                    {
                        spawnRoom = validNeighbors[UnityEngine.Random.Range(0, validNeighbors.Count)];
                        // WallpaperMod.Log?.LogInfo($"ChaosManager: Selected neighbor room {spawnRoom.name} for spawn");
                    }
                }
                catch (System.Exception) { /* Ignore and fall back to random */ }
            }

            // Fallback to random room if neighbor selection failed
            if (spawnRoom == null)
            {
                int attempts = 0;
                while (attempts < 10)
                {
                    var randomRoom = game.world.abstractRooms[UnityEngine.Random.Range(0, game.world.abstractRooms.Length)];
                    if (randomRoom != null && randomRoom.index != currentRoom.abstractRoom.index &&
                        IsValidChaosSpawnRoom(randomRoom))
                    {
                        spawnRoom = randomRoom;
                        break;
                    }
                    attempts++;
                }
            }

            if (spawnRoom == null)
            {
                WallpaperMod.Log?.LogWarning("ChaosManager: Could not find different room for spawn after 10 attempts");
                return;
            }

            try
            {
                // Create abstract creature in the spawn room (NOT current room)
                bool spawnAll = WallpaperMod.Options?.ChaosSpawnAll.Value ?? false;
                if (!spawnAll && IsBlacklisted(creatureType))
                {
                    WallpaperMod.Log?.LogWarning($"ChaosManager: Refusing blacklisted spawn type {creatureType}");
                    return;
                }

                // Issued by the game so it cannot collide with another entity (EntityID equality compares the number only)
                var entityID = game.GetNewID();
                var creatureTemplate = StaticWorld.GetCreatureTemplate(creatureType);
                if (creatureTemplate == null)
                {
                    WallpaperMod.Log?.LogWarning($"ChaosManager: No template for {creatureType}, skipping spawn");
                    return;
                }

                var abstractCreature = new AbstractCreature(
                    game.world,
                    creatureTemplate,
                    null,
                    spawnRoom.RandomNodeInRoom(),
                    entityID
                );

                // Initialize AI if the template supports it
                if (creatureTemplate.AI && abstractCreature.abstractAI == null)
                {
                    abstractCreature.InitiateAI();
                }

                // Custom handling for specific creatures
                if (creatureType.ToString().Contains("Scavenger"))
                {
                    // The game's own gear-up, called before AddEntity as MSC does for scavengers it spawns into a room.
                    // ScavengerAbstractAI only gears up scavengers created in the offscreen den by itself, plus Templars
                    // and Disciples, which already carry their gear here (hence the stuckObjects check)
                    if (abstractCreature.abstractAI is ScavengerAbstractAI scavengerAI && abstractCreature.stuckObjects.Count == 0)
                    {
                        scavengerAI.InitGearUp();
                    }
                }
                else if (creatureType.ToString() == "SlugNPC")
                {
                    HandleSlugNPC(abstractCreature);
                }

                // Add to spawn room (creatures will naturally migrate through the world)
                spawnRoom.AddEntity(abstractCreature);

                spawnedCreatures.Add(abstractCreature);

                int halfMax = maxCreatures / 2;
                bool justReachedHalf = spawnedCreatures.Count == halfMax;

                if (justReachedHalf)
                {
                    WallpaperMod.Log?.LogInfo($"ChaosManager: 🔥 Ramp-up phase complete! Spawned {creatureType} [weight {weight}] in room {spawnRoom.name} (total: {spawnedCreatures.Count}/{maxCreatures}) - switching to normal spawn rate");
                }
                else
                {
                    WallpaperMod.Log?.LogDebug($"ChaosManager: Spawned {creatureType} [weight {weight}] in room {spawnRoom.name} (total: {spawnedCreatures.Count}/{maxCreatures})");
                }
            }
            catch (System.Exception ex)
            {
                WallpaperMod.Log?.LogError($"ChaosManager: Failed to spawn {creatureType}: {ex.Message}");
            }
        }

        /// <summary>
        /// Configure SlugNPC (Slugpup) to be more active and independent
        /// </summary>
        /// <summary>
        /// Configure SlugNPC (Slugpup) to be more active and independent
        /// </summary>
        private void HandleSlugNPC(AbstractCreature creature)
        {
            // WallpaperMod.Log?.LogInfo($"DEBUG: Handling SlugNPC {creature.ID}");
            if (creature.state is PlayerState playerState)
            {
                // Make them energetic and brave
                // Re-assign personality to ensure changes stick
                AbstractCreature.Personality pers = creature.personality;
                pers.energy = 1f;
                pers.bravery = 1f;
                pers.nervous = 0f;
                pers.sympathy = 0f;
                creature.personality = pers;

                playerState.foodInStomach = 6; // Full belly
                // WallpaperMod.Log?.LogInfo($"DEBUG: Configured SlugNPC personality and food");
            }
            
            // CRITICAL FIX: Add to game.Players so AI doesn't crash when looking for players, but only up to the game's
            // own player count (MaxGamePlayers); pups past that still spawn, they are just not listed as players
            if (game.Players.Count < MaxGamePlayers && !game.Players.Contains(creature))
            {
                game.Players.Add(creature);
                // WallpaperMod.Log?.LogInfo($"DEBUG: Added SlugNPC {creature.ID} to game.Players to prevent AI crash");
            }

            if (creature.abstractAI != null)
            {
                // Force them to move to the current camera room
                if (game.cameras != null && game.cameras.Length > 0 && game.cameras[0].room != null)
                {
                    int targetRoomIndex = game.cameras[0].room.abstractRoom.index;
                    creature.abstractAI.SetDestination(new WorldCoordinate(targetRoomIndex, -1, -1, -1));
                    // WallpaperMod.Log?.LogInfo($"DEBUG: SlugNPC destination set to current room {targetRoomIndex}");
                }
            }
            else
            {
                // WallpaperMod.Log?.LogWarning($"DEBUG: SlugNPC has no abstractAI!");
            }
        }

        /// <summary>
        /// Remove creatures that have been destroyed or removed from world
        /// </summary>
        private void CleanupDeadCreatures()
        {
            spawnedCreatures.RemoveAll(creature =>
            {
                if (creature == null)
                {
                    return true;
                }

                if (!IsGoneFromWorld(creature))
                {
                    return false;
                }

                // HandleSlugNPC added pups to game.Players, so a pup that is gone must not stay there
                if (game?.session != null)
                {
                    game.Players.Remove(creature);
                }
                return true; // Remove from list
            });
        }

        /// <summary>
        /// True once the game has destroyed the creature or taken it out of the world. Checking only the realized creature
        /// misses creatures removed while abstract: AbstractCreature.Abstractize can destroy one whose realizedCreature is
        /// already null, and AbstractRoom.Abstractize turns a live quantified creature (Fly, Leech, Spider) that has stuck
        /// objects into a per-room count with RemoveEntity, without destroying it. Either way the entry would hold a
        /// maxCreatures slot for the rest of the world session.
        /// </summary>
        private static bool IsGoneFromWorld(AbstractCreature creature)
        {
            if (creature.slatedForDeletion ||
                (creature.realizedCreature != null && creature.realizedCreature.slatedForDeletetion))
            {
                return true;
            }

            if (!creature.creatureTemplate.quantified || creature.realizedCreature != null)
            {
                return false;
            }

            AbstractRoom room = creature.Room;
            return room != null && !room.creatures.Contains(creature) && !room.entitiesInDens.Contains(creature);
        }

        private static bool IsBlacklisted(CreatureTemplate.Type type)
        {
            return type == null || BlacklistedCreatures.Contains(type.ToString());
        }

        private static bool TryAddCreatureType(CreatureTemplate.Type type, List<CreatureTemplate.Type> list, bool enforceBlacklist)
        {
            if ((enforceBlacklist && IsBlacklisted(type)) || type == null || list.Contains(type) || StaticWorld.GetCreatureTemplate(type) == null)
            {
                return false;
            }

            list.Add(type);
            return true;
        }

        private static bool IsValidChaosSpawnRoom(AbstractRoom room)
        {
            if (!WallpaperRoomTracker.IsSafeWallpaperRoom(room) || room.shelter || IsShelterRoomName(room.name))
            {
                return false;
            }

            return true;
        }

        private static bool IsShelterRoomName(string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return true;
            }

            string[] parts = roomName.Split('_');
            if (parts.Length < 2)
            {
                return false;
            }

            string suffix = parts[parts.Length - 1];
            if (!suffix.StartsWith("S", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = 1; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i]))
                {
                    return false;
                }
            }

            return suffix.Length > 1;
        }

        /// <summary>
        /// Remove all spawned creatures from the world
        /// </summary>
        private void CleanupAllCreatures()
        {
            // Resolved once: the SlugNPCs HandleSlugNPC added to game.Players must not outlive their creatures
            List<AbstractCreature> players = game?.session != null ? game.Players : null;

            foreach (var creature in spawnedCreatures)
            {
                if (creature == null)
                {
                    continue;
                }

                try
                {
                    RemoveSpawnedCreature(creature, players);
                }
                catch (System.Exception ex)
                {
                    WallpaperMod.Log?.LogWarning($"ChaosManager: Failed to remove {creature.creatureTemplate?.type}: {ex.Message}");
                }
            }

            spawnedCreatures.Clear();
            WallpaperMod.Log?.LogInfo("ChaosManager: Cleaned up all spawned creatures");
        }

        /// <summary>
        /// Fully remove one spawned creature. Destroying only the realized creature is not enough: the AbstractCreature
        /// is only unregistered from its AbstractRoom if that room updates the destroyed object before it unloads (never
        /// for a creature in transit), a creature that is still abstract is never touched at all, and anything left
        /// registered is realized again when its room loads.
        /// </summary>
        private void RemoveSpawnedCreature(AbstractCreature creature, List<AbstractCreature> players)
        {
            Creature realized = creature.realizedCreature;
            if (realized != null)
            {
                // Release grasps in both directions so no Grasp is left pointing at a deleted creature.
                // Best effort: a failure here must not stop the removal below
                try
                {
                    realized.LoseAllGrasps();
                    realized.AllGraspsLetGoOfThisObject(true);
                }
                catch (System.Exception ex)
                {
                    WallpaperMod.Log?.LogWarning($"ChaosManager: Could not release grasps of {creature.creatureTemplate?.type}: {ex.Message}");
                }

                RemoveFromShortcutVessels(realized);

                realized.Destroy();
                realized.RemoveFromRoom();
            }

            // AbstractPhysicalObject.Destroy only flags the entity and unhooks its sticks, so unregister it from its room
            // as well. This comes after RemoveFromRoom, otherwise AbstractRoom.RemoveEntity warns that the realized creature remains
            creature.Destroy();
            creature.Room?.RemoveEntity(creature);

            // Only SlugNPCs were ever added here (HandleSlugNPC), Remove does nothing for any other creature
            players?.Remove(creature);
        }

        /// <summary>
        /// Drop any shortcut or flight vessel still carrying the creature. A creature in transit is in no room's update
        /// list, so the room never notices it was deleted, and the vessel would later hand it to a room or den
        /// (AbstractCreature.Move / MoveEntityToDen), registering the abstract creature again.
        /// </summary>
        private void RemoveFromShortcutVessels(Creature creature)
        {
            ShortcutHandler shortcuts = game?.shortcuts;
            if (shortcuts == null)
            {
                return;
            }

            shortcuts.transportVessels?.RemoveAll(vessel => vessel.creature == creature);
            shortcuts.betweenRoomsWaitingLobby?.RemoveAll(vessel => vessel.creature == creature);
            shortcuts.borderTravelVessels?.RemoveAll(vessel => vessel.creature == creature);
        }

        /// <summary>
        /// Cleanup when shutting down or changing regions
        /// </summary>
        public void Shutdown()
        {
            CleanupAllCreatures();
            regionalCreatureTypes.Clear();
            hasAnalyzedRegion = false;
            spawnTimer = 0f;
            regionLoadTimer = 0f;

            WallpaperMod.Log?.LogInfo("ChaosManager: Shutdown complete");
        }

        /// <summary>
        /// Called when region changes - force re-analysis on next update
        /// </summary>
        public void OnRegionChanged()
        {
            CleanupAllCreatures();
            regionalCreatureTypes.Clear();
            creatureWeightCounters.Clear(); // Reset weight counters for new region
            hasAnalyzedRegion = false;
            regionLoadTimer = 0f; // Reset load timer for new region
            spawnTimer = 0f; // Reset spawn timer

            WallpaperMod.Log?.LogInfo($"ChaosManager: Region changed, waiting {REGION_LOAD_DELAY}s before analyzing creatures");
        }

        // Public properties for UI
        public bool IsEnabled => chaosEnabled;
        public int ChaosLevel => chaosLevel;
        public int SpawnedCreatureCount => spawnedCreatures.Count;
        public int MaxCreatures => maxCreatures;
        public float SpawnInterval => spawnInterval;
    }
}
