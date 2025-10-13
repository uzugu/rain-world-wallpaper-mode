using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Manages region selection and transitions for wallpaper mode
    /// </summary>
    public class RegionManager
    {
        private WallpaperController controller;
        private readonly string initialRegion;
        private List<string> allRegions;
        private Queue<string> regionQueue;
        private string currentRegion;
        private int roomsExploredInRegion = 0;

        // Configuration
        private int roomsPerRegion = 20;

        // All Rain World regions (vanilla + Downpour)
        private static readonly string[] VANILLA_REGIONS = new string[]
        {
            "SU", // Outskirts
            "HI", // Industrial Complex
            "CC", // Chimney Canopy
            "GW", // Garbage Wastes
            "SH", // Shaded Citadel
            "DS", // Drainage System
            "SL", // Shoreline
            "SI", // Sky Islands
            "LF", // Farm Arrays
            "UW", // The Exterior
            "SS", // Five Pebbles
            "SB"  // Subterranean
        };

        private static readonly string[] DOWNPOUR_REGIONS = new string[]
        {
            "LM", // Looks to the Moon
            "RM", // Pipeyard
            "DM", // Metropolis
            "LC", // Outer Expanse
            "MS", // Waterfront Facility
            "VS", // Undergrowth
            "CL", // Silent Construct
            "OE"  // Rubicon
        };

        public RegionManager(WallpaperController controller, string startRegion)
        {
            this.controller = controller;
            initialRegion = string.IsNullOrEmpty(startRegion) ? "SU" : startRegion;
            currentRegion = initialRegion;
            roomsExploredInRegion = 0;
            InitializeRegions();
        }

        private void InitializeRegions()
        {
            allRegions = new List<string>();
            allRegions.AddRange(VANILLA_REGIONS);
            allRegions.AddRange(DOWNPOUR_REGIONS);

            // Remove duplicates and ensure start region is tracked
            allRegions = allRegions.Distinct().ToList();

            // Prepare queue without the current region
            var available = allRegions.Where(r => !string.Equals(r, currentRegion, StringComparison.OrdinalIgnoreCase)).ToList();
            ShuffleRegions(available);
            regionQueue = new Queue<string>(available);
        }

        private void ShuffleRegions(List<string> regions = null)
        {
            var source = regions ?? allRegions;
            var shuffled = source.OrderBy(x => UnityEngine.Random.value).ToList();
            regionQueue = new Queue<string>(shuffled);
        }

        public void OnRoomExplored()
        {
            roomsExploredInRegion++;
            WallpaperMod.Log?.LogInfo($"RegionManager: Room {roomsExploredInRegion}/{roomsPerRegion} in {currentRegion}");

            if (roomsExploredInRegion >= roomsPerRegion)
            {
                SwitchToNextRegion();
            }
        }

        private void SwitchToNextRegion()
        {
            WallpaperMod.Log?.LogInfo($"RegionManager: Switching from {currentRegion}");

            // Get next region
            if (regionQueue.Count == 0)
            {
                // Reshuffle when all regions explored
                ShuffleRegions();
            }

            AssignNextRegion();
        }

        public string GetCurrentRegion()
        {
            return currentRegion;
        }

        public int GetRoomsExplored()
        {
            return roomsExploredInRegion;
        }

        public int GetTotalRegions()
        {
            return allRegions.Count;
        }

        public int GetRegionsExplored()
        {
            return allRegions.Count - regionQueue.Count;
        }

        public int RoomsPerRegion => roomsPerRegion;

        public void Cleanup()
        {
            // Cleanup if needed
        }

        private void AssignNextRegion()
        {
            if (regionQueue.Count == 0)
            {
                var refill = allRegions.Where(r => !string.Equals(r, currentRegion, StringComparison.OrdinalIgnoreCase)).ToList();
                ShuffleRegions(refill);
            }

            if (regionQueue.Count > 0)
            {
                currentRegion = regionQueue.Dequeue();
                roomsExploredInRegion = 0;

                WallpaperMod.Log?.LogInfo($"RegionManager: Now exploring region {currentRegion}");
                controller.OnRegionChanged(currentRegion);
            }
            else
            {
                WallpaperMod.Log?.LogWarning("RegionManager: No regions available to assign");
            }
        }
    }
}
