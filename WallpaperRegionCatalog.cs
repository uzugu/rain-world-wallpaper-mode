using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RainWorldWallpaperMod
{
    internal static class WallpaperRegionCatalog
    {
        public static readonly string[] VanillaRegions =
        {
            "SU", "HI", "DS", "CC", "GW", "SH", "SL", "SI", "LF", "UW", "SS", "SB"
        };

        public static readonly string[] DownpourRegions =
        {
            "UG", "VS", "LM", "RM", "OE", "LC", "MS", "DM", "CL", "HR"
        };

        public static readonly string[] WatcherRegions =
        {
            "WVWA", "WVWB", "WRRA", "WPGA", "WARA", "WARB", "WARC", "WARD", "WARE", "WARF",
            "WARG", "WMPA", "WAUA", "WBLA", "WPTA", "WRFA", "WRFB", "WRSA", "WSKA", "WSKB",
            "WSKC", "WSKD", "WTDA", "WTDB", "WORA", "WDSR", "WGWR", "WHIR", "WSSR", "WSUR"
        };

        // These are only fallbacks for regions without an authored safariStartRoom.
        // Every entry is validated against the resolved world file before it is used.
        private static readonly Dictionary<string, string> FallbackStartRooms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SU", "SU_A01" }, { "HI", "HI_A01" }, { "DS", "DS_A01" }, { "CC", "CC_A01" },
            { "GW", "GW_A01" }, { "SH", "SH_A01" }, { "SL", "SL_A01" }, { "SI", "SI_A01" },
            { "LF", "LF_A01" }, { "UW", "UW_A01" }, { "SS", "SS_A01" }, { "SB", "SB_A01" },
            { "UG", "UG_A01" }, { "VS", "VS_A01" }, { "LM", "LM_A01" }, { "RM", "RM_A01" },
            { "OE", "OE_A01" }, { "LC", "LC_A01" }, { "MS", "MS_A01" }, { "DM", "DM_A01" },
            { "CL", "CL_A01" }, { "HR", "HR_C01" },

            { "WVWA", "WVWA_A01" }, { "WVWB", "WVWB_A01" }, { "WRRA", "WRRA_C01" },
            { "WPGA", "WPGA_E01" }, { "WARA", "WARA_E08" }, { "WARB", "WARB_B41" },
            { "WARC", "WARC_A01" }, { "WARD", "WARD_E25" }, { "WARE", "WARE_B29" },
            { "WARF", "WARF_A01" }, { "WARG", "WARG_B31" }, { "WMPA", "WMPA_A01" },
            { "WAUA", "WAUA_A01" }, { "WBLA", "WBLA_A01" }, { "WPTA", "WPTA_A01" },
            { "WRFA", "WRFA_H01" }, { "WRFB", "WRFB_B01" }, { "WRSA", "WRSA_L01" },
            { "WSKA", "WSKA_D01" }, { "WSKB", "WSKB_C01" }, { "WSKC", "WSKC_A27" },
            { "WSKD", "WSKD_B42" }, { "WTDA", "WTDA_A13" }, { "WTDB", "WTDB_A01" },
            { "WORA", "WORA_CITY8" }, { "WDSR", "WDSR_A07" }, { "WGWR", "WGWR_A01" },
            { "WSSR", "WSSR_cramped" }
        };

        // Used only if the game's DisplayName files cannot be read yet.
        private static readonly Dictionary<string, string> FallbackDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SU", "Outskirts" }, { "HI", "Industrial Complex" }, { "DS", "Drainage System" },
            { "CC", "Chimney Canopy" }, { "GW", "Garbage Wastes" }, { "SH", "Shaded Citadel" },
            { "SL", "Shoreline" }, { "SI", "Sky Islands" }, { "LF", "Farm Arrays" },
            { "UW", "The Exterior" }, { "SS", "Five Pebbles" }, { "SB", "Subterranean" },
            { "UG", "Undergrowth" }, { "VS", "Pipeyard" }, { "LM", "Waterfront Facility" },
            { "RM", "The Rot" }, { "OE", "Outer Expanse" }, { "LC", "Metropolis" },
            { "MS", "Submerged Superstructure" }, { "DM", "Looks to the Moon" },
            { "CL", "Silent Construct" }, { "HR", "Rubicon" },

            { "WARA", "Shattered Terrace" }, { "WARB", "Salination" }, { "WARC", "Fetid Glen" },
            { "WARD", "Cold Storage" }, { "WARE", "Heat Ducts" }, { "WARF", "Aether Ridge" },
            { "WARG", "The Surface" }, { "WAUA", "Ancient Urban" }, { "WBLA", "Badlands" },
            { "WDSR", "Decaying Tunnels" }, { "WGWR", "Infested Wastes" },
            { "WHIR", "Corrupted Factories" }, { "WMPA", "Migration Path" }, { "WORA", "Outer Rim" },
            { "WPGA", "Pillar Grove" }, { "WPTA", "Signal Spires" }, { "WRFA", "Coral Caves" },
            { "WRFB", "Turbulent Pump" }, { "WRRA", "Rusted Wrecks" }, { "WRSA", "Daemon" },
            { "WSKA", "Torrential Railways" }, { "WSKB", "Sunbaked Alley" },
            { "WSKC", "Stormy Coast" }, { "WSKD", "Shrouded Stacks" },
            { "WSSR", "Unfortunate Evolution" }, { "WSUR", "Crumbling Fringes" },
            { "WTDA", "Torrid Desert" }, { "WTDB", "Desolate Tract" },
            { "WVWA", "Verdant Waterways" }, { "WVWB", "Fractured Gateways" }
        };

        public static IReadOnlyList<string> GetRegionsForCampaign(string campaign)
        {
            SlugcatStats.Name slugcat = WallpaperModOptions.GetSlugcatName(campaign);
            try
            {
                // CRS hooks these two public game APIs using MetaProperties.txt, so this also
                // returns only custom regions explicitly marked for the selected campaign.
                List<string> regions = SlugcatStats.SlugcatStoryRegions(slugcat)
                    .Concat(SlugcatStats.SlugcatOptionalRegions(slugcat))
                    .Where(IsUsableRegionCode)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (regions.Count > 0)
                {
                    return regions;
                }
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Could not query campaign region APIs for '{campaign}': {ex.Message}");
            }

            if (string.Equals(campaign, "Watcher", StringComparison.OrdinalIgnoreCase) && ModCompatibility.IsWatcherEnabled)
            {
                return WatcherRegions.Where(code => !string.Equals(code, "WRSA", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return VanillaRegions;
        }

        public static IReadOnlyList<string> GetAllSelectableRegions()
        {
            string[] campaigns =
            {
                "White", "Yellow", "Red", "Gourmand", "Artificer", "Rivulet", "Spearmaster", "Saint", "Watcher"
            };

            return campaigns
                .SelectMany(GetRegionsForCampaign)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string GetDefaultStartRegionForCampaign(string campaign)
        {
            IReadOnlyList<string> regions = GetRegionsForCampaign(campaign);
            if (string.Equals(campaign, "Watcher", StringComparison.OrdinalIgnoreCase) &&
                regions.Contains("WVWA", StringComparer.OrdinalIgnoreCase))
            {
                return "WVWA";
            }

            return regions.FirstOrDefault() ?? "SU";
        }

        public static string NormalizeRegionForCampaign(string regionCode, string campaign)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return GetDefaultStartRegionForCampaign(campaign);
            }

            string normalized = regionCode.Trim().ToUpperInvariant();
            return IsRegionAllowedForCampaign(normalized, campaign) ? normalized : GetDefaultStartRegionForCampaign(campaign);
        }

        public static string InferCampaignForRegion(string regionCode, string currentCampaign)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return currentCampaign;
            }

            string normalized = regionCode.Trim().ToUpperInvariant();
            if (WatcherRegions.Contains(normalized, StringComparer.OrdinalIgnoreCase) &&
                GetRegionsForCampaign("Watcher").Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                return "Watcher";
            }

            return currentCampaign;
        }

        public static bool IsRegionAllowedForCampaign(string regionCode, string campaign)
        {
            return !string.IsNullOrWhiteSpace(regionCode) &&
                GetRegionsForCampaign(campaign).Contains(regionCode.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static string GetStartRoom(string regionCode, string campaign = null)
        {
            string normalized = string.IsNullOrWhiteSpace(regionCode) ? "SU" : regionCode.Trim().ToUpperInvariant();
            SlugcatStats.Name slugcat = WallpaperModOptions.GetSlugcatName(campaign ?? WallpaperMod.Options?.SelectedCampaign.Value);

            if (TryGetSafariStartRoom(normalized, slugcat, out string safariStartRoom))
            {
                return safariStartRoom;
            }

            string worldPath = GetWorldFilePath(normalized);
            if (FallbackStartRooms.TryGetValue(normalized, out string fallbackRoom) &&
                WallpaperRoomTracker.IsSafeWallpaperRoomName(fallbackRoom) &&
                RoomExistsInWorldFile(worldPath, fallbackRoom))
            {
                return fallbackRoom;
            }

            string firstRoom = GetFirstSafeRoom(worldPath);
            if (!string.IsNullOrWhiteSpace(firstRoom))
            {
                WallpaperMod.Log?.LogInfo($"Using first valid world-file room '{firstRoom}' for region '{normalized}'");
                return firstRoom;
            }

            WallpaperMod.Log?.LogWarning($"No authored or validated start room found for region '{normalized}'; using legacy fallback");
            return normalized + "_A01";
        }

        public static string GetDisplayName(string regionCode)
        {
            return GetDisplayName(regionCode, WallpaperMod.Options?.SelectedCampaign.Value);
        }

        public static string GetDisplayName(string regionCode, string campaign)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return string.Empty;
            }

            string normalized = regionCode.Trim().ToUpperInvariant();
            try
            {
                string displayName = Region.GetRegionFullName(normalized, WallpaperModOptions.GetSlugcatName(campaign));
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName.Trim();
                }
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogDebug($"Could not read DisplayName for '{normalized}': {ex.Message}");
            }

            return FallbackDisplayNames.TryGetValue(normalized, out string fallback) ? fallback : normalized;
        }

        public static bool IsKnownRegion(string regionCode)
        {
            if (!IsUsableRegionCode(regionCode))
            {
                return false;
            }

            string normalized = regionCode.Trim().ToUpperInvariant();
            try
            {
                return Region.GetFullRegionOrder().Contains(normalized, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return VanillaRegions.Concat(DownpourRegions).Concat(WatcherRegions)
                    .Contains(normalized, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool TryGetSafariStartRoom(string regionCode, SlugcatStats.Name slugcat, out string room)
        {
            room = null;
            string timeline = null;
            try
            {
                timeline = SlugcatStats.SlugcatToTimeline(slugcat)?.value;
            }
            catch
            {
                // Fall back to the campaign-independent properties file.
            }

            string directory = "World" + Path.DirectorySeparatorChar + regionCode + Path.DirectorySeparatorChar;
            string path = null;
            if (!string.IsNullOrWhiteSpace(timeline))
            {
                string timelinePath = AssetManager.ResolveFilePath(directory + "properties-" + timeline + ".txt");
                if (File.Exists(timelinePath))
                {
                    path = timelinePath;
                }
            }

            if (path == null)
            {
                path = AssetManager.ResolveFilePath(directory + "properties.txt");
            }

            if (!File.Exists(path))
            {
                return false;
            }

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine?.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf(':');
                if (separator <= 0 || !string.Equals(line.Substring(0, separator).Trim(), "safariStartRoom", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = line.Substring(separator + 1).Trim();
                if (WallpaperRoomTracker.IsSafeWallpaperRoomName(candidate) &&
                    RoomExistsInWorldFile(GetWorldFilePath(regionCode), candidate))
                {
                    room = candidate;
                    return true;
                }
            }

            return false;
        }

        private static string GetWorldFilePath(string regionCode)
        {
            string relativePath = "World" + Path.DirectorySeparatorChar + regionCode + Path.DirectorySeparatorChar +
                "world_" + regionCode.ToLowerInvariant() + ".txt";
            return AssetManager.ResolveFilePath(relativePath);
        }

        private static bool RoomExistsInWorldFile(string worldPath, string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName) || !File.Exists(worldPath))
            {
                return false;
            }

            return ReadRoomLines(worldPath).Any(line =>
            {
                int separator = line.IndexOf(':');
                return separator > 0 && string.Equals(line.Substring(0, separator).Trim(), roomName, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static string GetFirstSafeRoom(string worldPath)
        {
            string firstRoom = null;
            foreach (string line in ReadRoomLines(worldPath))
            {
                int separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                string candidate = line.Substring(0, separator).Trim();
                if (!IsUsableRegionCode(candidate) || !WallpaperRoomTracker.IsSafeWallpaperRoomName(candidate))
                {
                    continue;
                }

                if (firstRoom == null)
                {
                    firstRoom = candidate;
                }

                if (line.IndexOf(": SHELTER", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return candidate;
                }
            }

            return firstRoom;
        }

        private static IEnumerable<string> ReadRoomLines(string worldPath)
        {
            if (!File.Exists(worldPath))
            {
                yield break;
            }

            bool inRooms = false;
            foreach (string rawLine in File.ReadLines(worldPath))
            {
                string line = rawLine?.Trim();
                if (string.Equals(line, "ROOMS", StringComparison.OrdinalIgnoreCase))
                {
                    inRooms = true;
                    continue;
                }

                if (inRooms && string.Equals(line, "END ROOMS", StringComparison.OrdinalIgnoreCase))
                {
                    yield break;
                }

                if (inRooms && !string.IsNullOrWhiteSpace(line) && !line.StartsWith("//", StringComparison.Ordinal))
                {
                    yield return line;
                }
            }
        }

        private static bool IsUsableRegionCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            return trimmed.Length <= 32 && trimmed.All(character => char.IsLetterOrDigit(character) || character == '_');
        }
    }
}
