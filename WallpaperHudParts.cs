using System;
using System.Collections.Generic;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// What the first line of the rain block is saying.
    /// </summary>
    internal enum WallpaperHudRainState
    {
        Waiting,
        Countdown,
        NoRainWait
    }

    /// <summary>
    /// One line of HUD text. A black copy sits one pixel down and to the right of the visible label. That is the same
    /// trick the game's own in-room text (HUD.TutorialText) uses to keep light text readable over bright rooms.
    /// </summary>
    internal sealed class WallpaperHudLine
    {
        private readonly FLabel label;
        private readonly FLabel shade;
        private string text = string.Empty;

        public WallpaperHudLine(FContainer parent, string fontName, Color color, FLabelAlignment alignment, float gapBelow)
        {
            GapBelow = gapBelow;

            // Both labels start empty and hidden. An empty FLabel reports a negative-height text rect, so a line
            // without text is never measured or laid out.
            shade = new FLabel(fontName, string.Empty) { alignment = alignment, color = Color.black, isVisible = false };
            label = new FLabel(fontName, string.Empty) { alignment = alignment, color = color, isVisible = false };
            parent.AddChild(shade);
            parent.AddChild(label);
        }

        /// <summary>Space between this line and the visible line below it.</summary>
        public float GapBelow { get; }

        public bool HasText => text.Length > 0;

        /// <summary>Height of the line box. It comes from the font, so the font of every language lays out correctly.</summary>
        public float Height => label.FontLineHeight;

        public float Width => HasText ? label.textRect.width : 0f;

        /// <summary>Sets the text. Returns true when it changed, which means the block needs a new layout.</summary>
        public bool SetText(string value)
        {
            if (value == null)
            {
                value = string.Empty;
            }

            if (string.Equals(text, value, StringComparison.Ordinal))
            {
                return false;
            }

            text = value;
            label.text = value;
            shade.text = value;

            bool hasText = value.Length > 0;
            label.isVisible = hasText;
            shade.isVisible = hasText;
            return true;
        }

        public void SetPosition(float x, float y)
        {
            // Whole pixels plus 0.01 is the snap the game's own HUD labels use to keep the bitmap fonts crisp.
            float px = Mathf.Round(x) + 0.01f;
            float py = Mathf.Round(y) + 0.01f;
            label.x = px;
            label.y = py;
            shade.x = px + 1f;
            shade.y = py - 1f;
        }

        public void Remove()
        {
            label.RemoveFromContainer();
            shade.RemoveFromContainer();
        }
    }

    /// <summary>
    /// A group of text lines that fades in and out on its own, with a soft shadow behind it. Lines are stacked upward
    /// from the bottom of the block, so the lowest visible line never moves when a line above it comes or goes.
    /// </summary>
    internal sealed class WallpaperHudBlock
    {
        // Fade steps per game tick: in over 20 ticks and out over 30, like the game's own text prompts.
        private const float FadeInStep = 1f / 20f;
        private const float FadeOutStep = 1f / 30f;
        private const float FadeCurve = 0.65f;

        // The shadow is a radial FlatLight gradient that is strongest at its centre and gone at its edge, so it is
        // made noticeably larger than the text it sits behind.
        private const float ShadowWidthScale = 1.8f;
        private const float ShadowWidthPad = 120f;
        private const float ShadowHeightScale = 1.2f;
        private const float ShadowHeightPad = 70f;

        private readonly List<WallpaperHudLine> lines = new List<WallpaperHudLine>();
        private readonly FSprite shadow;
        private float level;

        public WallpaperHudBlock(FContainer parent, FShader flatLight, float shadowAlpha)
        {
            Container = new FContainer { isVisible = false, alpha = 0f };
            parent.AddChild(Container);

            if (flatLight != null)
            {
                // Same recipe as the game's own HUD shadows (HUD.TutorialText, Menu.DarkGradient): a white square
                // drawn with the FlatLight shader in black.
                shadow = new FSprite("Futile_White") { shader = flatLight, color = Color.black, alpha = shadowAlpha, isVisible = false };
                Container.AddChild(shadow);
            }
        }

        public FContainer Container { get; }

        /// <summary>True when a line changed or the screen did, so Layout must run before the block is drawn.</summary>
        public bool LayoutDirty { get; set; } = true;

        public bool IsFullyShown => level >= 1f;

        /// <summary>Adds a line below the ones added before it.</summary>
        public WallpaperHudLine AddLine(string fontName, Color color, FLabelAlignment alignment, float gapBelow)
        {
            var line = new WallpaperHudLine(Container, fontName, color, alignment, gapBelow);
            lines.Add(line);
            return line;
        }

        /// <summary>Moves the fade one tick towards shown or hidden.</summary>
        public void Step(bool shown)
        {
            level = Mathf.MoveTowards(level, shown ? 1f : 0f, shown ? FadeInStep : FadeOutStep);
            Apply();
        }

        public void HideNow()
        {
            level = 0f;
            Apply();
        }

        private void Apply()
        {
            // A hidden container is switched off rather than left at zero alpha, because sprites with a custom shader
            // can still draw at zero alpha.
            bool visible = level > 0f;
            Container.isVisible = visible;
            if (visible)
            {
                Container.alpha = RWCustom.Custom.SCurve(level, FadeCurve);
            }
        }

        /// <summary>
        /// Stacks the lines that have text upward from <paramref name="bottomY"/>, leaving <paramref name="reservedBottom"/>
        /// free below the lowest one, and fits the shadow around the result. Returns the area the block fills.
        /// </summary>
        public Rect Layout(float anchorX, FLabelAlignment alignment, float bottomY, float reservedBottom, float minWidth)
        {
            LayoutDirty = false;

            float cursor = bottomY + reservedBottom;
            float width = minWidth;
            bool placed = false;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                WallpaperHudLine line = lines[i];
                if (!line.HasText)
                {
                    continue;
                }

                if (placed)
                {
                    cursor += line.GapBelow;
                }

                float height = line.Height;
                line.SetPosition(anchorX, cursor + height * 0.5f);
                cursor += height;
                width = Mathf.Max(width, line.Width);
                placed = true;
            }

            float left = anchorX;
            if (alignment == FLabelAlignment.Right)
            {
                left = anchorX - width;
            }
            else if (alignment == FLabelAlignment.Center)
            {
                left = anchorX - width * 0.5f;
            }

            var area = new Rect(left, bottomY, width, cursor - bottomY);
            if (shadow != null)
            {
                shadow.isVisible = placed;
                if (placed)
                {
                    shadow.x = area.center.x;
                    shadow.y = area.center.y;
                    shadow.width = area.width * ShadowWidthScale + ShadowWidthPad;
                    shadow.height = area.height * ShadowHeightScale + ShadowHeightPad;
                }
            }

            return area;
        }

        public void Remove()
        {
            foreach (WallpaperHudLine line in lines)
            {
                line.Remove();
            }

            Container.RemoveAllChildren();
            Container.RemoveFromContainer();
        }
    }

    /// <summary>
    /// The words of the HUD, and the lookups behind them. Nothing here touches a sprite.
    /// </summary>
    internal static class WallpaperHudText
    {
        public const string KeyboardHint = "Right/D Next   Left/A Prev   Up/Down Camera   N Skip   G/B Region   L Lock   H HUD   F1 Settings";

        public const string ControllerHint = "Stick Rooms/Camera   A Skip   LB/RB Region   X Lock   Y HUD   Start Settings";

        // A hosted desktop wallpaper gets mouse clicks but no keys (WallpaperHostMode).
        public const string HostedWallpaperHint = "Double-click Settings";

        // The cycle percentage at which No Rain Wait changes region. Mirrors WallpaperRainCycleController.NoRainThreshold.
        private const int NoRainThresholdPercent = 95;

        // What the game answers for a region that has no DisplayName file.
        private const string UnknownRegionName = "Unknown Region";

        public static string BuildRainLine(WallpaperHudRainState state, int value)
        {
            switch (state)
            {
                case WallpaperHudRainState.NoRainWait:
                    return $"No Rain Wait: {value}% / {NoRainThresholdPercent}%";
                case WallpaperHudRainState.Countdown:
                    return "Rain in " + FormatCountdown(value);
                default:
                    return $"Waiting for the rain ({value}%)";
            }
        }

        public static string BuildRegionCountLine(int explored, int total)
        {
            return $"Region {explored} of {total}";
        }

        public static string BuildNextRegionLine(string regionName)
        {
            return "Next region: " + regionName;
        }

        /// <summary>The room being viewed and, while the camera is on its way, the room it is heading for.</summary>
        public static string BuildRoomLine(string current, string next)
        {
            bool hasCurrent = !string.IsNullOrEmpty(current);
            bool hasNext = !string.IsNullOrEmpty(next);
            if (hasCurrent && hasNext)
            {
                return current + "  >  " + next;
            }

            if (hasCurrent)
            {
                return current;
            }

            return hasNext ? next : string.Empty;
        }

        public static string FormatCountdown(int totalSeconds)
        {
            int seconds = Math.Max(0, totalSeconds);
            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>The region's name as the game shows it in the current language.</summary>
        public static string ResolveRegionName(string regionCode, InGameTranslator translator)
        {
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return string.Empty;
            }

            string name = WallpaperRegionCatalog.GetDisplayName(regionCode, WallpaperMod.CurrentCampaign);
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, UnknownRegionName, StringComparison.OrdinalIgnoreCase))
            {
                // The code is more useful than "Unknown Region" for a custom region without a DisplayName file.
                return regionCode.Trim().ToUpperInvariant();
            }

            return Translate(translator, name);
        }

        /// <summary>
        /// Finds which of the region's sub-regions the room is in, the way HUD.SubregionTracker does: index 0 is the
        /// region itself, so a match starts at 1. Returns 0 (and a null region) when the room has no sub-region.
        /// </summary>
        public static int FindSubregionIndex(Room room, out Region region)
        {
            region = null;
            if (room == null || room.world == null || room.world.singleRoomWorld || room.world.region == null)
            {
                return 0;
            }

            string subregionName = room.abstractRoom != null ? room.abstractRoom.subregionName : null;
            List<string> subRegions = room.world.region.subRegions;
            if (subregionName == null || subRegions == null)
            {
                return 0;
            }

            for (int i = 1; i < subRegions.Count; i++)
            {
                if (subregionName == subRegions[i])
                {
                    region = room.world.region;
                    return i;
                }
            }

            return 0;
        }

        /// <summary>The sub-region's display name: the alternative name when the region defines one, translated.</summary>
        public static string GetSubregionName(Region region, int index, InGameTranslator translator)
        {
            string name = region.subRegions[index];
            if (region.altSubRegions != null && index < region.altSubRegions.Count && region.altSubRegions[index] != null)
            {
                name = region.altSubRegions[index];
            }

            return Translate(translator, name);
        }

        /// <summary>
        /// Translates with the game's translator. Debug builds of the game answer a marker string instead of the original
        /// text when a string has no translation, so those fall back to the original here.
        /// </summary>
        public static string Translate(InGameTranslator translator, string text)
        {
            if (translator == null || string.IsNullOrEmpty(text))
            {
                return text;
            }

            try
            {
                string translated = translator.Translate(text);
                if (string.IsNullOrEmpty(translated) || translated == "!NO TRANSLATION!" || translated == "DOUBLE TRANSLATION")
                {
                    return text;
                }

                return translated;
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogDebug($"WallpaperHUD: Could not translate '{text}': {ex.Message}");
                return text;
            }
        }
    }
}
