using System;
using System.Collections.Generic;
using Menu;
using RWCustom;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// The timing of the floating settings menu's motion, all counted in menu ticks (40 per second): everything slides
    /// in from the right, one item after the next from the top, bobs gently while the menu is idle and slides out to
    /// the right when the menu closes. The functions only turn tick counts into fractions; the menu moves the objects.
    /// </summary>
    internal static class FloatingMotion
    {
        /// <summary>Ticks one item takes to slide in.</summary>
        public const int EnterTicks = 14;

        /// <summary>Ticks between the start of one card sliding in and the start of the next one below it.</summary>
        public const int StaggerTicks = 2;

        /// <summary>Ticks the slide-out takes.</summary>
        public const int ExitTicks = 8;

        private const float EnterEase = 0.8f;
        private const float BobAmplitude = 1.5f;
        private const float BobSpeed = 0.045f;
        private const float BobPhaseStep = 0.9f;

        /// <summary>
        /// How much of the slide-in distance an item that starts on tick delay still has left on this tick: 1 until
        /// its turn comes, 0 once it has arrived. The easing is the game's own S-curve.
        /// </summary>
        public static float EnterRemaining(int ticks, int delay)
        {
            float progress = Mathf.Clamp01((ticks - delay) / (float)EnterTicks);
            return 1f - Custom.SCurve(progress, EnterEase);
        }

        /// <summary>How far the slide-out has come, from 0 when it starts to 1 when it is over.</summary>
        public static float ExitProgress(int exitTicks)
        {
            return Mathf.Clamp01(exitTicks / (float)ExitTicks);
        }

        /// <summary>
        /// The share of the slide distance covered by the slide-out. It speeds up as it goes, so the items leave the
        /// screen quickly instead of drifting away.
        /// </summary>
        public static float ExitTravelled(float exitProgress)
        {
            return exitProgress * exitProgress;
        }

        /// <summary>
        /// The vertical bobbing offset in pixels. Each item bobs with its own phase, so the column ripples. The bob
        /// grows in as the item arrives and fades out as the menu leaves, so it never fights the slides.
        /// </summary>
        public static float Bob(int ticks, int delay, int index, float exitProgress)
        {
            float settled = Mathf.Clamp01((ticks - delay) / (float)EnterTicks);
            double wave = Math.Sin(ticks * (double)BobSpeed + index * (double)BobPhaseStep);
            return (float)wave * BobAmplitude * settled * (1f - exitProgress);
        }
    }

    /// <summary>
    /// One control card of the floating settings menu: a translucent rounded rectangle that owns the label and the
    /// control shown on it. The card is a positioned menu object, so everything it owns is drawn relative to it and
    /// moves with it, and the buttons it owns still register in the page's selectables (a menu object finds its page
    /// through its owners). The frame lights up while one of the card's controls is selected and dims when all of
    /// them are greyed out.
    /// </summary>
    internal sealed class FloatingCard : PositionedMenuObject
    {
        private const float FillAlpha = 0.7f;
        private const float LitFillAlpha = 0.82f;

        /// <summary>
        /// The controls on the card. Selecting one of them lights the card, and greying out all of them dims it.
        /// </summary>
        public readonly List<ButtonTemplate> Controls = new List<ButtonTemplate>();

        private readonly RoundedRect frame;
        private float glow;
        private float lastGlow;

        public FloatingCard(Menu.Menu menu, MenuObject owner, Vector2 pos, Vector2 size)
            : base(menu, owner, pos)
        {
            // Made first, because sprites are drawn in the order they were added: the frame stays under the rest.
            frame = new RoundedRect(menu, this, Vector2.zero, size, filled: true);
            frame.fillAlpha = FillAlpha;
            subObjects.Add(frame);
        }

        public override void Update()
        {
            base.Update();

            bool lit = false;
            for (int i = 0; i < Controls.Count; i++)
            {
                if (menu.selectedObject == Controls[i] && !Controls[i].buttonBehav.greyedOut)
                {
                    lit = true;
                }
            }

            lastGlow = glow;
            glow = Custom.LerpAndTick(glow, lit ? 1f : 0f, 0.15f, 0.1f);

            // Set after the base update has stored the previous fill, so the change is interpolated.
            frame.fillAlpha = Mathf.Lerp(FillAlpha, LitFillAlpha, glow);
        }

        public override void GrafUpdate(float timeStacker)
        {
            bool dimmed = Controls.Count > 0;
            for (int i = 0; i < Controls.Count; i++)
            {
                if (!Controls[i].buttonBehav.greyedOut)
                {
                    dimmed = false;
                }
            }

            // The frame reads its border colour while it draws, so the colour is set before the base class draws it.
            HSLColor border = Menu.Menu.MenuColor(dimmed ? Menu.Menu.MenuColors.DarkGrey : Menu.Menu.MenuColors.MediumGrey);
            frame.borderColor = HSLColor.Lerp(border, Menu.Menu.MenuColor(Menu.Menu.MenuColors.White), Mathf.Lerp(lastGlow, glow, timeStacker));
            base.GrafUpdate(timeStacker);
        }
    }
}
