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
        private List<string> regionOrder;
        private HashSet<string> visitedRegions;
        private string currentRegion;
        private int roomsExploredInRegion = 0;

        private int currentRegionIndex = 0;

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
            currentRegion = initialRegion.ToUpperInvariant();
            visitedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            roomsExploredInRegion = 0;
            InitializeRegions();
        }

        private void InitializeRegions()
        {
            var distinctRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var region in VANILLA_REGIONS)
            {
                distinctRegions.Add(region);
            }
            foreach (var region in DOWNPOUR_REGIONS)
            {
                distinctRegions.Add(region);
            }

            regionOrder = distinctRegions.ToList();

            // Shuffle region order for varied traversal
            for (int i = regionOrder.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (regionOrder[i], regionOrder[swapIndex]) = (regionOrder[swapIndex], regionOrder[i]);
            }

            if (!regionOrder.Any(r => string.Equals(r, currentRegion, StringComparison.OrdinalIgnoreCase)))
            {
                regionOrder.Insert(0, currentRegion);
            }

            currentRegionIndex = regionOrder.FindIndex(r => string.Equals(r, currentRegion, StringComparison.OrdinalIgnoreCase));
            if (currentRegionIndex < 0)
            {
                currentRegionIndex = 0;
                currentRegion = regionOrder[currentRegionIndex];
            }

            visitedRegions.Clear();
            visitedRegions.Add(currentRegion);
        }

        public void OnRoomExplored()
        {
            roomsExploredInRegion++;
            WallpaperMod.Log?.LogInfo($"RegionManager: Room {roomsExploredInRegion} explored in {currentRegion}");
        }

        public string GetCurrentRegion()
        {
            return currentRegion;
        }

        public string GetNextRegion()
        {
            if (regionOrder == null || regionOrder.Count == 0)
            {
                return currentRegion;
            }

            var nextIndex = (currentRegionIndex + 1) % regionOrder.Count;
            return regionOrder[nextIndex];
        }

        public string GetPreviousRegion()
        {
            if (regionOrder == null || regionOrder.Count == 0)
            {
                return currentRegion;
            }

            var prevIndex = (currentRegionIndex - 1 + regionOrder.Count) % regionOrder.Count;
            return regionOrder[prevIndex];
        }

        public int GetRoomsExplored()
        {
            return roomsExploredInRegion;
        }

        public int GetTotalRegions()
        {
            return regionOrder?.Count ?? 0;
        }

        public int GetRegionsExplored()
        {
            return visitedRegions?.Count ?? 0;
        }

        /// <summary>
        /// Returns a random unvisited region, or null if all regions have been visited
        /// </summary>
        public string GetRandomUnvisitedRegion()
        {
            if (regionOrder == null || regionOrder.Count == 0)
            {
                return null;
            }

            var unvisitedRegions = regionOrder.Where(r => !visitedRegions.Contains(r)).ToList();

            if (unvisitedRegions.Count == 0)
            {
                return null; // All regions visited
            }

            // Select random unvisited region
            int randomIndex = UnityEngine.Random.Range(0, unvisitedRegions.Count);
            return unvisitedRegions[randomIndex];
        }

        /// <summary>
        /// Checks if all regions in the current campaign have been visited
        /// </summary>
        public bool AreAllRegionsVisited()
        {
            if (regionOrder == null || regionOrder.Count == 0)
            {
                return true;
            }

            return visitedRegions.Count >= regionOrder.Count;
        }

        /// <summary>
        /// Called when campaign changes - clears visited regions to start fresh
        /// </summary>
        public void OnCampaignChange()
        {
            visitedRegions.Clear();
            roomsExploredInRegion = 0;
            WallpaperMod.Log?.LogInfo("RegionManager: Campaign changed, cleared visited regions");
        }

        public void Cleanup()
        {
            // Cleanup if needed
        }

        public void AdvanceToNextRegion()
        {
            if (regionOrder == null || regionOrder.Count == 0)
            {
                return;
            }

            currentRegionIndex = (currentRegionIndex + 1) % regionOrder.Count;
            SetCurrentRegion(regionOrder[currentRegionIndex]);
        }

        public void AdvanceToPreviousRegion()
        {
            if (regionOrder == null || regionOrder.Count == 0)
            {
                return;
            }

            currentRegionIndex = (currentRegionIndex - 1 + regionOrder.Count) % regionOrder.Count;
            SetCurrentRegion(regionOrder[currentRegionIndex]);
        }

        public void ForceRegion(string regionCode)
        {
            if (string.IsNullOrEmpty(regionCode))
            {
                return;
            }

            if (regionOrder == null)
            {
                InitializeRegions();
            }

            var index = regionOrder.FindIndex(r => string.Equals(r, regionCode, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                regionOrder.Add(regionCode.ToUpperInvariant());
                index = regionOrder.Count - 1;
            }

            currentRegionIndex = index;
            SetCurrentRegion(regionOrder[currentRegionIndex]);
        }

        public IReadOnlyList<string> GetAllRegions()
        {
            return regionOrder;
        }

        public void SetCurrentRegion(string regionCode)
        {
            currentRegion = regionCode.ToUpperInvariant();
            roomsExploredInRegion = 0;
            visitedRegions.Add(currentRegion);

            WallpaperMod.Log?.LogInfo($"RegionManager: Now exploring region {currentRegion}");
            controller.OnRegionChanged(currentRegion);
        }
    }
}
