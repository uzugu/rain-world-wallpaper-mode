using System;
using System.Collections.Generic;
using System.Linq;

namespace RainWorldWallpaperMod
{
    internal static class WallpaperRegionCatalog
    {
        public static readonly string[] VanillaRegions =
        {
            "SU", "HI", "CC", "GW", "SH", "DS", "SL", "SI", "LF", "UW", "SS", "SB"
        };

        public static readonly string[] DownpourRegions =
        {
            "LM", "RM", "DM", "LC", "MS", "VS", "CL", "OE"
        };

        private static readonly string[] WatcherModifiedBaseRegions =
        {
            "SU", "HI", "CC", "LF", "SH"
        };

        public static readonly string[] WatcherRegions =
        {
            "WVWA", "WVWB", "WRRA", "WPGA", "WARA", "WARB", "WARC", "WARD", "WARE", "WARF",
            "WARG", "WMPA", "WAUA", "WBLA", "WPTA", "WRFA", "WRFB", "WRSA", "WSKA", "WSKB",
            "WSKC", "WSKD", "WTDA", "WTDB", "WORA", "WDSR", "WGWR", "WHIR", "WSSR", "WSUR"
        };

        private static readonly Dictionary<string, string> StartRooms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SU", "SU_A01" },
            { "HI", "HI_A01" },
            { "CC", "CC_A01" },
            { "GW", "GW_A01" },
            { "SH", "SH_A01" },
            { "DS", "DS_A01" },
            { "SL", "SL_A01" },
            { "SI", "SI_A01" },
            { "LF", "LF_A01" },
            { "UW", "UW_A01" },
            { "SS", "SS_A01" },
            { "SB", "SB_A01" },
            { "LM", "LM_A01" },
            { "RM", "RM_A01" },
            { "DM", "DM_A01" },
            { "LC", "LC_A01" },
            { "MS", "MS_A01" },
            { "VS", "VS_A01" },
            { "CL", "CL_A01" },
            { "OE", "OE_A01" },

            { "WVWA", "WVWA_A01" },
            { "WVWB", "WVWB_A01" },
            { "WRRA", "WRRA_C01" },
            { "WPGA", "WPGA_E01" },
            { "WARA", "WARA_E08" },
            { "WARB", "WARB_B41" },
            { "WARC", "WARC_A01" },
            { "WARD", "WARD_E25" },
            { "WARE", "WARE_B29" },
            { "WARF", "WARF_A01" },
            { "WARG", "WARG_B31" },
            { "WMPA", "WMPA_A01" },
            { "WAUA", "WAUA_A01" },
            { "WBLA", "WBLA_A01" },
            { "WPTA", "WPTA_A01" },
            { "WRFA", "WRFA_H01" },
            { "WRFB", "WRFB_B01" },
            { "WRSA", "WRSA_L01" },
            { "WSKA", "WSKA_D01" },
            { "WSKB", "WSKB_C01" },
            { "WSKC", "WSKC_A27" },
            { "WSKD", "WSKD_B42" },
            { "WTDA", "WTDA_A13" },
            { "WTDB", "WTDB_A01" },
            { "WORA", "WORA_CITY8" },
            { "WDSR", "WDSR_A07" },
            { "WGWR", "WGWR_A01" },
            { "WHIR", "WHIR_A01" },
            { "WSSR", "WSSR_cramped" },
            { "WSUR", "WSUR_A01" }
        };

        private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SU", "Outskirts" },
            { "HI", "Industrial Complex" },
            { "CC", "Chimney Canopy" },
            { "GW", "Garbage Wastes" },
            { "SH", "Shaded Citadel" },
            { "DS", "Drainage System" },
            { "SL", "Shoreline" },
            { "SI", "Sky Islands" },
            { "LF", "Farm Arrays" },
            { "UW", "The Exterior" },
            { "SS", "Five Pebbles" },
            { "SB", "Subterranean" },
            { "LM", "Waterfront Facility" },
            { "RM", "The Rot" },
            { "DM", "Looks to the Moon" },
            { "LC", "Metropolis" },
            { "MS", "Submerged Superstructure" },
            { "VS", "Pipeyard" },
            { "CL", "Silent Construct" },
            { "OE", "Outer Expanse" },
            { "WVWA", "Stormy Coast A" },
            { "WVWB", "Stormy Coast B" },
            { "WRRA", "Rustworks" },
            { "WPGA", "Phosphor Garden" },
            { "WARA", "Verdant Waterways A" },
            { "WARB", "Verdant Waterways B" },
            { "WARC", "Verdant Waterways C" },
            { "WARD", "Verdant Waterways D" },
            { "WARE", "Verdant Waterways E" },
            { "WARF", "Verdant Waterways F" },
            { "WARG", "Verdant Waterways G" },
            { "WMPA", "Aether Ridge" },
            { "WAUA", "Aether Ridge Outskirts" },
            { "WBLA", "Bloodmarsh" },
            { "WPTA", "Desolate Tracks" },
            { "WRFA", "Refinery A" },
            { "WRFB", "Refinery B" },
            { "WRSA", "Ripple Shore" },
            { "WSKA", "Coral Caves A" },
            { "WSKB", "Coral Caves B" },
            { "WSKC", "Coral Caves C" },
            { "WSKD", "Coral Caves D" },
            { "WTDA", "Torrid Desert A" },
            { "WTDB", "Torrid Desert B" },
            { "WORA", "Outer Rim" },
            { "WDSR", "Drainage System Ruins" },
            { "WGWR", "Garbage Wastes Ruins" },
            { "WHIR", "Shaded Citadel Ruins" },
            { "WSSR", "Five Pebbles Ruins" },
            { "WSUR", "Submerged Ruins" }
        };

        public static IReadOnlyList<string> GetRegionsForCampaign(string campaign)
        {
            if (string.Equals(campaign, "Watcher", StringComparison.OrdinalIgnoreCase) && ModCompatibility.IsWatcherEnabled)
            {
                return WatcherModifiedBaseRegions.Concat(WatcherRegions).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            if (ModCompatibility.IsDownpourEnabled)
            {
                return VanillaRegions.Concat(DownpourRegions).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            return VanillaRegions;
        }

        public static IReadOnlyList<string> GetAllSelectableRegions()
        {
            IEnumerable<string> regions = VanillaRegions;
            if (ModCompatibility.IsDownpourEnabled)
            {
                regions = regions.Concat(DownpourRegions);
            }

            if (ModCompatibility.IsWatcherEnabled)
            {
                regions = regions.Concat(WatcherRegions);
            }

            return regions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string GetDefaultStartRegionForCampaign(string campaign)
        {
            return string.Equals(campaign, "Watcher", StringComparison.OrdinalIgnoreCase) && ModCompatibility.IsWatcherEnabled ? "WVWA" : "SU";
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
            if (WatcherRegions.Contains(normalized, StringComparer.OrdinalIgnoreCase) && ModCompatibility.IsWatcherEnabled)
            {
                return "Watcher";
            }

            return currentCampaign;
        }

        public static bool IsRegionAllowedForCampaign(string regionCode, string campaign)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return false;
            }

            return GetRegionsForCampaign(campaign).Contains(regionCode.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        public static string GetStartRoom(string regionCode)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return "SU_A01";
            }

            string normalized = regionCode.Trim().ToUpperInvariant();
            return StartRooms.TryGetValue(normalized, out var room) ? room : normalized + "_A01";
        }

        public static string GetDisplayName(string regionCode)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return string.Empty;
            }

            return DisplayNames.TryGetValue(regionCode.Trim(), out var displayName) ? displayName : regionCode;
        }

        public static string GetDisplayName(string regionCode, string campaign)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return string.Empty;
            }

            string normalized = regionCode.Trim().ToUpperInvariant();
            if (string.Equals(normalized, "CL", StringComparison.OrdinalIgnoreCase))
            {
                return "Silent Construct";
            }

            return GetDisplayName(normalized);
        }

        public static bool IsKnownRegion(string regionCode)
        {
            return !string.IsNullOrWhiteSpace(regionCode) && StartRooms.ContainsKey(regionCode.Trim());
        }
    }
}
