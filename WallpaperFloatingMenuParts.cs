using System.Collections.Generic;
using Menu;
using RWCustom;
using UnityEngine;

namespace RainWorldWallpaperMod
{
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
