using System;
using System.Collections.Generic;
using MoreSlugcats;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Chaos mode: many more of the creatures a region already has.
    ///
    /// The game fills a region from the CREATURES list of its world file, which names a den for every creature and differs
    /// per campaign (World.spawners). Chaos runs that same list again, more times, the way WorldLoader.GeneratePopulation
    /// does: each new creature starts in the den its spawner names and leaves it when the game lets it. So every creature
    /// is one the region really has, in a place it really lives, and nothing here decides what goes where or follows the
    /// camera. Whether the camera meets them is left to chance, and with enough of them it will.
    ///
    /// Levels are exponential: level 5 adds 150 creatures, and each level doubles the one below it.
    /// </summary>
    public class ChaosManager
    {
        private const int CreaturesAtLevelFive = 150;
        private const int MaxSpawnsPerTick = 20;
        private const int TicksBetweenPrunes = 40;
        private const int TicksBetweenStatusLines = 400;
        private const int RoomPickAttempts = 10;

        // A region's chance of a slugpup per cycle, used as their share of the new creatures, up to this much.
        private const float MaxSlugpupShare = 0.5f;

        // With the "every creature" setting on, the share of new creatures that come from anywhere in the game.
        private const float EveryCreatureShare = 0.5f;

        // Overseers need a player (OverseerCommunicationModule), and a Slugcat is one. No region has GrappleSnakes and the
        // game cannot draw one: GrappleSnakeGraphics leaves a sprite slot empty, which throws when a camera sees it.
        private static readonly HashSet<string> NeverSpawned = new HashSet<string> { "Slugcat", "Overseer", "GrappleSnake" };

        // These only work in the place the game puts them, so the "every creature" setting leaves them out. In their own
        // dens, where the region's list has them, they are fine.
        private static readonly HashSet<string> NeedsOwnDen = new HashSet<string>
        {
            "GarbageWorm",          // Lives in garbage holes
            "TentaclePlant",        // MMF kills these outside a den
            "PoleMimic",
            "Deer"                  // Only travels through transportation nodes
        };

        /// <summary>
        /// One line of a region's creature list: a den, and what it can hold. A lineage is a chain of creatures that the
        /// game moves along over the cycles, any of which can live in that den.
        /// </summary>
        private sealed class DenEntry
        {
            public WorldCoordinate Den;
            public readonly List<KeyValuePair<CreatureTemplate, string>> Residents = new List<KeyValuePair<CreatureTemplate, string>>();
        }

        private readonly RainWorldGame game;

        // Everyone chaos added who is still alive. Bodies stay in the world, and they are replaced.
        private readonly List<AbstractCreature> crowd = new List<AbstractCreature>();

        // A den appears once per creature the region's list puts there, so a pick is as likely as the list makes it.
        private readonly List<DenEntry> roster = new List<DenEntry>();
        private readonly List<CreatureTemplate> everyCreature = new List<CreatureTemplate>();

        private int chaosLevel;
        private bool chaosEnabled;
        private bool rosterReady;
        private bool spawningFailed;
        private float slugpupShare;
        private int ticksUntilPrune;
        private int ticksSinceStatusLine;
        private float lastStatusLineTime;

        public ChaosManager(RainWorldGame game)
        {
            this.game = game;

            WallpaperMod.Log?.LogInfo("ChaosManager: Initialized (disabled by default)");
        }

        public bool IsEnabled => chaosEnabled;

        /// <summary>
        /// How many extra creatures a region holds at this level.
        /// </summary>
        public static int CreaturesAtLevel(int level)
        {
            return Mathf.RoundToInt(CreaturesAtLevelFive * Mathf.Pow(2f, level - 5));
        }

        /// <summary>
        /// What a level means, for the settings menus.
        /// </summary>
        public static string DescribeLevel(int level)
        {
            return $"{level} ({CreaturesAtLevel(level)} extra creatures)";
        }

        /// <summary>
        /// Enable chaos mode with specified level (1-10, out-of-range values are clamped)
        /// </summary>
        public void EnableChaos(int level)
        {
            // Clamp instead of rejecting: WallpaperController retries EnableChaos every tick while IsEnabled is false,
            // so rejecting a bad saved level would warn on every tick and chaos would never start
            int allowedLevel = Mathf.Clamp(level, WallpaperModOptions.ChaosLevelRangeMin, WallpaperModOptions.ChaosLevelRangeMax);
            if (allowedLevel != level)
            {
                WallpaperMod.Log?.LogWarning($"ChaosManager: Chaos level {level} is out of range, using {allowedLevel}");
            }

            chaosLevel = allowedLevel;
            chaosEnabled = true;
            rosterReady = false;

            WallpaperMod.Log?.LogInfo($"ChaosManager: Enabled at level {chaosLevel}, {CreaturesAtLevel(chaosLevel)} extra creatures in the region");
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
        /// Called once per game tick. Keeps the region at its number of extra creatures, so whoever dies is replaced.
        /// </summary>
        public void Update()
        {
            if (!chaosEnabled || spawningFailed || game?.world == null)
            {
                return;
            }

            if (!rosterReady)
            {
                BuildRoster();
            }

            ticksUntilPrune--;
            if (ticksUntilPrune <= 0)
            {
                ticksUntilPrune = TicksBetweenPrunes;
                crowd.RemoveAll(IsGone);
            }

            int missing = CreaturesAtLevel(chaosLevel) - crowd.Count;
            for (int i = 0; i < Mathf.Min(missing, MaxSpawnsPerTick) && !spawningFailed; i++)
            {
                SpawnOne();
            }

            LogStatus();
        }

        // How many of them the camera is actually meeting, and whether the game keeps up. The tick length the wallpaper is
        // handed is nominal, so the tick rate has to come from real time.
        private void LogStatus()
        {
            ticksSinceStatusLine++;
            if (ticksSinceStatusLine < TicksBetweenStatusLines)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            float ticksPerSecond = lastStatusLineTime > 0f && now > lastStatusLineTime ? ticksSinceStatusLine / (now - lastStatusLineTime) : 0f;
            lastStatusLineTime = now;
            ticksSinceStatusLine = 0;

            AbstractRoom room = game.cameras != null && game.cameras.Length > 0 ? game.cameras[0].room?.abstractRoom : null;
            int inView = 0;
            if (room != null)
            {
                foreach (AbstractCreature creature in room.creatures)
                {
                    if (creature.state.alive)
                    {
                        inView++;
                    }
                }
            }

            WallpaperMod.Log?.LogDebug($"ChaosManager: {crowd.Count}/{CreaturesAtLevel(chaosLevel)} extra creatures alive, {inView} creatures in {room?.name}, {ticksPerSecond:0} ticks/s, {game.world.activeRooms.Count} rooms loaded");
        }

        private void BuildRoster()
        {
            rosterReady = true;
            roster.Clear();
            everyCreature.Clear();

            foreach (World.CreatureSpawner spawner in game.world.spawners)
            {
                if (spawner is World.SimpleSpawner simple)
                {
                    var entry = new DenEntry { Den = simple.den };
                    AddResident(entry, simple.creatureType, simple.spawnDataString);
                    AddToRoster(entry, simple.amount);
                }
                else if (spawner is World.Lineage lineage)
                {
                    var entry = new DenEntry { Den = lineage.den };
                    for (int i = 0; i < lineage.creatureTypes.Length; i++)
                    {
                        if (lineage.creatureTypes[i] >= 0)
                        {
                            var type = new CreatureTemplate.Type(ExtEnum<CreatureTemplate.Type>.values.GetEntry(lineage.creatureTypes[i]));
                            AddResident(entry, type, lineage.spawnData[i]);
                        }
                    }

                    AddToRoster(entry, 1);
                }
            }

            slugpupShare = FindSlugpupShare();

            if (WallpaperMod.Options?.ChaosSpawnAll.Value ?? false)
            {
                foreach (CreatureTemplate template in StaticWorld.creatureTemplates)
                {
                    if (IsSpawnable(template) && !NeedsOwnDen.Contains(template.type.ToString()))
                    {
                        everyCreature.Add(template);
                    }
                }
            }

            LogRoster();
        }

        // Swarm creatures (batflies, leeches, coalescipedes) are only a number per room until the room loads, so they are
        // not creatures to multiply here. They stay as the game has them.
        private static bool IsSpawnable(CreatureTemplate template)
        {
            return template != null && !template.virtualCreature && !template.quantified &&
                !NeverSpawned.Contains(template.type.ToString());
        }

        private static void AddResident(DenEntry entry, CreatureTemplate.Type type, string spawnData)
        {
            CreatureTemplate template = StaticWorld.GetCreatureTemplate(type);
            if (IsSpawnable(template))
            {
                entry.Residents.Add(new KeyValuePair<CreatureTemplate, string>(template, spawnData));
            }
        }

        // The checks GeneratePopulation makes before it puts a creature in a den.
        private void AddToRoster(DenEntry entry, int amount)
        {
            AbstractRoom room = game.world.GetAbstractRoom(entry.Den);
            if (entry.Residents.Count == 0 || room == null || entry.Den.abstractNode >= room.nodes.Length ||
                (room.nodes[entry.Den.abstractNode].type != AbstractRoomNode.Type.Den &&
                 room.nodes[entry.Den.abstractNode].type != AbstractRoomNode.Type.GarbageHoles))
            {
                return;
            }

            for (int i = 0; i < Mathf.Max(1, amount); i++)
            {
                roster.Add(entry);
            }
        }

        // Slugpups are not on the creature lists. The game gives each region a chance of one per cycle, and only in these
        // campaigns (StoryGameSession.slugPupMaxCount). It also asks for a beaten Gourmand campaign, which a wallpaper
        // should not depend on.
        private float FindSlugpupShare()
        {
            SlugcatStats.Name campaign = game.StoryCharacter;
            bool campaignHasPups = campaign == SlugcatStats.Name.White || campaign == SlugcatStats.Name.Red ||
                campaign == MoreSlugcatsEnums.SlugcatStatsName.Gourmand;
            if (!ModManager.MSC || !campaignHasPups || game.world.region == null)
            {
                return 0f;
            }

            return Mathf.Min(game.world.region.regionParams.slugPupSpawnChance, MaxSlugpupShare);
        }

        private void LogRoster()
        {
            var counts = new Dictionary<string, int>();
            foreach (DenEntry entry in roster)
            {
                foreach (KeyValuePair<CreatureTemplate, string> resident in entry.Residents)
                {
                    string name = resident.Key.type.ToString();
                    counts.TryGetValue(name, out int count);
                    counts[name] = count + 1;
                }
            }

            var parts = new List<string>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                parts.Add($"{pair.Key} {pair.Value}");
            }

            parts.Sort(string.CompareOrdinal);
            WallpaperMod.Log?.LogInfo($"ChaosManager: {game.world.name} roster has {roster.Count} den places: {string.Join(", ", parts.ToArray())}; slugpup share {slugpupShare:0.##}; every creature list {everyCreature.Count}");
        }

        private void SpawnOne()
        {
            try
            {
                if (UnityEngine.Random.value < slugpupShare)
                {
                    SpawnInAnyRoom(StaticWorld.GetCreatureTemplate(MoreSlugcatsEnums.CreatureTemplateType.SlugNPC));
                }
                else if (everyCreature.Count > 0 && UnityEngine.Random.value < EveryCreatureShare)
                {
                    SpawnInAnyRoom(everyCreature[UnityEngine.Random.Range(0, everyCreature.Count)]);
                }
                else if (roster.Count > 0)
                {
                    SpawnInDen(roster[UnityEngine.Random.Range(0, roster.Count)]);
                }
            }
            catch (Exception ex)
            {
                // A creature that cannot be built would only fail again, every tick. Clearing chaosEnabled would not hold,
                // as WallpaperController turns chaos back on while the setting says so.
                WallpaperMod.Log?.LogError($"ChaosManager: Stopped until the next region after a creature failed to spawn: {ex}");
                spawningFailed = true;
            }
        }

        private void SpawnInDen(DenEntry entry)
        {
            KeyValuePair<CreatureTemplate, string> resident = entry.Residents[UnityEngine.Random.Range(0, entry.Residents.Count)];
            AbstractCreature creature = CreateCreature(resident.Key, entry.Den, resident.Value);
            creature.Room.MoveEntityToDen(creature);
        }

        // For creatures with no den on the region's list. AddEntity only registers a creature, so a room that is already
        // loaded would never show it; the room must load after it.
        private void SpawnInAnyRoom(CreatureTemplate template)
        {
            AbstractRoom[] rooms = game.world.abstractRooms;
            for (int attempt = 0; attempt < RoomPickAttempts; attempt++)
            {
                AbstractRoom room = rooms[UnityEngine.Random.Range(0, rooms.Length)];
                if (WallpaperRoomTracker.IsSafeWallpaperRoom(room) && room.realizedRoom == null &&
                    room.AttractionForCreature(template.type) != AbstractRoom.CreatureRoomAttraction.Forbidden &&
                    room.NodesRelevantToCreature(template) > 0)
                {
                    room.AddEntity(CreateCreature(template, new WorldCoordinate(room.index, -1, -1, room.RandomRelevantNode(template)), null));
                    return;
                }
            }
        }

        private AbstractCreature CreateCreature(CreatureTemplate template, WorldCoordinate place, string spawnData)
        {
            var creature = new AbstractCreature(game.world, template, null, place, game.GetNewID());
            creature.spawnData = spawnData;
            creature.setCustomFlags();

            // A den's list can hold a creature back for the night or for before the rain, and everyone heads for a den in
            // the last minute of the cycle. These stay out whatever the hour, or most of them would never be seen.
            creature.ignoreCycle = true;
            creature.preCycle = false;

            if (template.type == MoreSlugcatsEnums.CreatureTemplateType.SlugNPC)
            {
                ConfigureSlugpup(creature);
            }

            crowd.Add(creature);
            return creature;
        }

        // Makes a slugpup energetic and brave. It is not listed in game.Players: with anyone in that list an echo's encounter
        // can end the session and ScavengersWorldAI indexes arrays of four by it.
        private static void ConfigureSlugpup(AbstractCreature creature)
        {
            if (creature.state is PlayerState playerState)
            {
                AbstractCreature.Personality personality = creature.personality;
                personality.energy = 1f;
                personality.bravery = 1f;
                personality.nervous = 0f;
                personality.sympathy = 0f;
                creature.personality = personality;

                playerState.foodInStomach = 6;
            }
        }

        private static bool IsGone(AbstractCreature creature)
        {
            return creature.slatedForDeletion || creature.state.dead ||
                (creature.realizedCreature != null && creature.realizedCreature.slatedForDeletetion);
        }

        /// <summary>
        /// Remove everyone chaos added from the world
        /// </summary>
        private void CleanupAllCreatures()
        {
            foreach (AbstractCreature creature in crowd)
            {
                try
                {
                    RemoveSpawnedCreature(creature);
                }
                catch (Exception ex)
                {
                    WallpaperMod.Log?.LogWarning($"ChaosManager: Failed to remove {creature.creatureTemplate?.type}: {ex.Message}");
                }
            }

            crowd.Clear();
            WallpaperMod.Log?.LogInfo("ChaosManager: Cleaned up all spawned creatures");
        }

        /// <summary>
        /// Fully remove one creature. Destroying only the realized creature is not enough: the AbstractCreature
        /// is only unregistered from its AbstractRoom if that room updates the destroyed object before it unloads (never
        /// for a creature in transit), a creature that is still abstract is never touched at all, and anything left
        /// registered is realized again when its room loads.
        /// </summary>
        private void RemoveSpawnedCreature(AbstractCreature creature)
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
                catch (Exception ex)
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
            rosterReady = false;

            WallpaperMod.Log?.LogInfo("ChaosManager: Shutdown complete");
        }

        /// <summary>
        /// Called when the region starts over without a world reload
        /// </summary>
        public void OnRegionChanged()
        {
            CleanupAllCreatures();
            rosterReady = false;
        }
    }
}
