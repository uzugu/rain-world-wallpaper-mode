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
        private int chaosLevel = 0; // 0 = disabled, 1-10 = intensity levels
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
        private static readonly Dictionary<int, (float interval, int maxCreatures, float rampUpInterval)> ChaosLevels = new Dictionary<int, (float, int, float)>
        {
            { 1, (45f, 50, 10f) },    // Level 1: Controlled chaos - ramp up every 10s
            { 2, (40f, 70, 9f) },     // Level 2: Getting busy - ramp up every 9s
            { 3, (35f, 90, 8f) },     // Level 3: Very active - ramp up every 8s
            { 4, (30f, 110, 7f) },    // Level 4: Intense - ramp up every 7s
            { 5, (25f, 130, 6f) },    // Level 5: Overwhelming - ramp up every 6s
            { 6, (20f, 150, 5f) },    // Level 6: Extreme - ramp up every 5s
            { 7, (15f, 175, 4f) },    // Level 7: Insane - ramp up every 4s
            { 8, (12f, 200, 3f) },    // Level 8: Mayhem - ramp up every 3s
            { 9, (10f, 225, 2f) },    // Level 9: Apocalyptic - ramp up every 2s
            { 10, (8f, 250, 1f) }     // Level 10: ABSOLUTE CHAOS - ramp up every 1s!
        };

        private float rampUpInterval = 10f; // Fast spawn interval for first half

        // Creature types that should never be spawned by chaos system
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
        /// Enable chaos mode with specified level (1-10)
        /// </summary>
        public void EnableChaos(int level)
        {
            if (level < 1 || level > 10)
            {
                WallpaperMod.Log?.LogWarning($"ChaosManager: Invalid chaos level {level}, must be 1-10");
                return;
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

            // DEBUG: Manual Spawn Triggers
            // if (Input.GetKeyDown(KeyCode.P))
            // {
            //     WallpaperMod.Log?.LogInfo("DEBUG: Forcing SlugNPC Spawn!");
            //     SpawnCreature("SlugNPC");
            // }
            // if (Input.GetKeyDown(KeyCode.O))
            // {
            //     WallpaperMod.Log?.LogInfo("DEBUG: Forcing Scavenger Spawn!");
            //     SpawnCreature("Scavenger");
            // }

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

        private void SpawnCreature(string type)
        {
             try
            {
                if (game?.world == null) return;
                
                // Find a room
                AbstractRoom spawnRoom = null;
                
                // Try to find a neighbor of the current camera room first
                if (game.cameras != null && game.cameras.Length > 0 && game.cameras[0].room != null)
                {
                    Room currentRoom = game.cameras[0].room;
                    if (currentRoom.abstractRoom.connections != null && currentRoom.abstractRoom.connections.Length > 0)
                    {
                        int neighborIndex = currentRoom.abstractRoom.connections[UnityEngine.Random.Range(0, currentRoom.abstractRoom.connections.Length)];
                        if (neighborIndex > -1 && neighborIndex < game.world.abstractRooms.Length)
                        {
                            spawnRoom = game.world.abstractRooms[neighborIndex];
                        }
                    }
                    
                    // Fallback to current room if no neighbor
                    if (spawnRoom == null) spawnRoom = currentRoom.abstractRoom;
                }

                if (spawnRoom == null)
                {
                     WallpaperMod.Log?.LogInfo("DEBUG: Could not find spawn room!");
                     return;
                }

                WallpaperMod.Log?.LogInfo($"DEBUG: Spawning {type} in {spawnRoom.name}");

                CreatureTemplate.Type creatureType = new CreatureTemplate.Type(type);
                if (creatureType == null)
                {
                     // Try to parse if it's a special type
                     try {
                        creatureType = (CreatureTemplate.Type)ExtEnum<CreatureTemplate.Type>.Parse(typeof(CreatureTemplate.Type), type, true);
                     } catch {}
                }

                 if (StaticWorld.GetCreatureTemplate(creatureType) == null)
                {
                    WallpaperMod.Log?.LogInfo($"DEBUG: Template for {type} is NULL!");
                    return;
                }

                WorldCoordinate spawnPos = spawnRoom.RandomNodeInRoom();
                // Ensure valid den or exit if possible, otherwise random
                
                AbstractCreature abstractCreature = new AbstractCreature(game.world, StaticWorld.GetCreatureTemplate(creatureType), null, spawnPos, game.GetNewID());
                spawnRoom.AddEntity(abstractCreature);
                abstractCreature.RealizeInRoom();
                if (abstractCreature.abstractAI == null)
                {
                    abstractCreature.InitiateAI(); // Only init if missing
                }

                WallpaperMod.Log?.LogInfo($"DEBUG: Successfully spawned {type} ID: {abstractCreature.ID}");

                if (type.Contains("Scavenger")) EquipScavenger(abstractCreature);
                if (type == "SlugNPC") HandleSlugNPC(abstractCreature);
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"DEBUG: Error spawning {type}: {ex}");
            }
        }

        // ... (rest of file)


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

                        // Check if blacklisted (unless spawn all is enabled)
                        if (!spawnAll && BlacklistedCreatures.Contains(type.ToString()))
                        {
                            blacklistedTypes.Add(type.ToString());
                            continue;
                        }

                        regionalCreatureTypes.Add(type);
                    }
                }
            }

            hasAnalyzedRegion = true;

            // Chaos level 7+: Force-add SlugNPC, Scavenger (all variations), and ScavengerKing to any region
            if (chaosLevel >= 7)
            {
                WallpaperMod.Log?.LogInfo($"ChaosManager: 🔥 Chaos 7+ activated - forcing special creatures into spawn pool");
                WallpaperMod.Log?.LogInfo($"ChaosManager: ModManager.MSC (Downpour DLC) = {ModManager.MSC}");

                // Add vanilla Scavenger
                if (!regionalCreatureTypes.Contains(CreatureTemplate.Type.Scavenger))
                {
                    regionalCreatureTypes.Add(CreatureTemplate.Type.Scavenger);
                    WallpaperMod.Log?.LogInfo($"ChaosManager: ✓ Force-added Scavenger (vanilla)");
                }

                // Add DLC creatures if Downpour is installed
                if (ModManager.MSC)
                {
                    // Force add SlugNPC (Slugpup)
                    var slugNPC = new CreatureTemplate.Type("SlugNPC", true);
                    if (!regionalCreatureTypes.Contains(slugNPC))
                    {
                        regionalCreatureTypes.Add(slugNPC);
                        WallpaperMod.Log?.LogInfo($"ChaosManager: ✓ Force-added SlugNPC (Slugpup)");
                    }

                    var dlcCreatureNames = new[] { "ScavengerElite", "ScavengerKing" };

                    foreach (var creatureName in dlcCreatureNames)
                    {
                        try
                        {
                            // Create the type - constructor with true will register it if it doesn't exist
                            var creatureType = new CreatureTemplate.Type(creatureName, true);

                            // Force add to pool without checking template (trusting it will work or fail gracefully at spawn time)
                            if (!regionalCreatureTypes.Contains(creatureType))
                            {
                                regionalCreatureTypes.Add(creatureType);
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
                    // Pick a random connection
                    var neighbors = currentRoom.abstractRoom.connections;
                    // Filter out -1 (no connection) and current room
                    var validNeighbors = new System.Collections.Generic.List<int>();
                    foreach (var n in neighbors)
                    {
                        if (n > -1 && n < game.world.abstractRooms.Length && n != currentRoom.abstractRoom.index)
                        {
                            validNeighbors.Add(n);
                        }
                    }

                    if (validNeighbors.Count > 0)
                    {
                        int neighborIndex = validNeighbors[UnityEngine.Random.Range(0, validNeighbors.Count)];
                        spawnRoom = game.world.abstractRooms[neighborIndex];
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
                    if (randomRoom != null && randomRoom.index != currentRoom.abstractRoom.index)
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
                var entityID = new EntityID(-1, UnityEngine.Random.Range(0, 100000));
                var creatureTemplate = StaticWorld.GetCreatureTemplate(creatureType);
                var abstractCreature = new AbstractCreature(
                    game.world,
                    creatureTemplate,
                    null,
                    new WorldCoordinate(spawnRoom.index, -1, -1, 0),
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
                    EquipScavenger(abstractCreature);
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
                    WallpaperMod.Log?.LogInfo($"ChaosManager: Spawned {creatureType} [weight {weight}] in room {spawnRoom.name} (total: {spawnedCreatures.Count}/{maxCreatures})");
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
            
            // CRITICAL FIX: Add to game.Players so AI doesn't crash when looking for players
            if (!game.Players.Contains(creature))
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
                if (creature == null || creature.realizedCreature == null || creature.realizedCreature.slatedForDeletetion)
                {
                    return true; // Remove from list
                }
                return false;
            });
        }

        /// <summary>
        /// Remove all spawned creatures
        /// </summary>
        private void CleanupAllCreatures()
        {
            foreach (var creature in spawnedCreatures)
            {
                if (creature?.realizedCreature != null)
                {
                    creature.realizedCreature.Destroy();
                }
            }

            spawnedCreatures.Clear();
            WallpaperMod.Log?.LogInfo("ChaosManager: Cleaned up all spawned creatures");
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

        /// <summary>
        /// Equip scavengers with random weapons and items
        /// </summary>
        private void EquipScavenger(AbstractCreature abstractCreature)
        {
            if (abstractCreature?.realizedCreature == null)
            {
                return;
            }

            var scavenger = abstractCreature.realizedCreature as Scavenger;
            if (scavenger == null)
            {
                return;
            }

            try
            {
                var random = new System.Random();
                string scavType = abstractCreature.creatureTemplate.type.ToString();

                // ScavengerElite: Keep their natural special weapons, don't equip anything
                if (scavType == "ScavengerElite")
                {
                    return;
                }

                // ScavengerKing: Elite equipment (3 items with explosive spears)
                bool isKing = scavType == "ScavengerKing";

                // Kings always get 3 items, regular get 1-2
                int numItems = isKing ? 3 : random.Next(1, 3);

                for (int i = 0; i < numItems; i++)
                {
                    var entityID = new EntityID(-1, random.Next(100000));
                    AbstractPhysicalObject abstractItem = null;

                    int itemChoice = random.Next(100);

                    if (isKing)
                    {
                        // ScavengerKing: 50% explosive spear, 30% spear, 20% rock
                        if (itemChoice < 50)
                        {
                            // Explosive spear
                            abstractItem = new AbstractSpear(
                                game.world,
                                null,
                                abstractCreature.pos,
                                entityID,
                                true,  // explosive!
                                false
                            );
                        }
                        else if (itemChoice < 80)
                        {
                            // Regular spear
                            abstractItem = new AbstractSpear(
                                game.world,
                                null,
                                abstractCreature.pos,
                                entityID,
                                false,
                                false
                            );
                        }
                        else
                        {
                            // Rock
                            abstractItem = new AbstractPhysicalObject(
                                game.world,
                                AbstractPhysicalObject.AbstractObjectType.Rock,
                                null,
                                abstractCreature.pos,
                                entityID
                            );
                        }
                    }
                    else
                    {
                        // Regular Scavenger: 40% spear, 25% explosive spear, 20% grenade, 10% rock, 5% extra grenade
                        if (itemChoice < 40)
                        {
                            // Regular spear
                            abstractItem = new AbstractSpear(
                                game.world,
                                null,
                                abstractCreature.pos,
                                entityID,
                                false,
                                false
                            );
                        }
                        else if (itemChoice < 65)
                        {
                            // Explosive spear
                            abstractItem = new AbstractSpear(
                                game.world,
                                null,
                                abstractCreature.pos,
                                entityID,
                                true,  // explosive!
                                false
                            );
                        }
                        else if (itemChoice < 90)
                        {
                            // Grenade (ScavengerBomb) - 25% total chance (20% + 5% instead of singularity bomb)
                            abstractItem = new AbstractPhysicalObject(
                                game.world,
                                AbstractPhysicalObject.AbstractObjectType.ScavengerBomb,
                                null,
                                abstractCreature.pos,
                                entityID
                            );
                        }
                        else
                        {
                            // Rock
                            abstractItem = new AbstractPhysicalObject(
                                game.world,
                                AbstractPhysicalObject.AbstractObjectType.Rock,
                                null,
                                abstractCreature.pos,
                                entityID
                            );
                        }
                    }

                    if (abstractItem != null)
                    {
                        scavenger.room.abstractRoom.AddEntity(abstractItem);
                        abstractItem.RealizeInRoom();

                        // Try to give item to scavenger using Grab method
                        if (abstractItem.realizedObject != null && scavenger.grasps != null)
                        {
                            for (int graspIndex = 0; graspIndex < scavenger.grasps.Length; graspIndex++)
                            {
                                if (scavenger.grasps[graspIndex] == null)
                                {
                                    scavenger.Grab(abstractItem.realizedObject, graspIndex, 0, Creature.Grasp.Shareability.CanNotShare, 1f, false, false);
                                    break;
                                }
                            }
                        }
                    }
                }

                if (isKing)
                {
                    WallpaperMod.Log?.LogInfo($"ChaosManager: 👑 Equipped ScavengerKing with elite gear!");
                }
            }
            catch (System.Exception ex)
            {
                WallpaperMod.Log?.LogError($"ChaosManager: Failed to equip scavenger: {ex.Message}");
            }
        }

        // Public properties for UI
        public bool IsEnabled => chaosEnabled;
        public int ChaosLevel => chaosLevel;
        public int SpawnedCreatureCount => spawnedCreatures.Count;
        public int MaxCreatures => maxCreatures;
        public float SpawnInterval => spawnInterval;
    }
}
