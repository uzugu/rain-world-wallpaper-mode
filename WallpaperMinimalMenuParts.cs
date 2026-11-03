using System;
using Menu;
using RWCustom;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// What a row of the minimalistic menu does when it is activated.
    /// </summary>
    internal enum MinimalRowKind
    {
        /// <summary>Steps through a list. Confirm or a click goes forward, left and right go either way.</summary>
        Cycler,

        /// <summary>Shows On or Off and flips when it is confirmed or clicked.</summary>
        Toggle,

        /// <summary>A text button without a value.</summary>
        Action
    }

    /// <summary>
    /// The anchor all the text of the menu hangs from. The menu moves it to slide the column in, and everything below
    /// it follows, including the rows' mouse areas, because menu objects find their screen position through their owners.
    /// </summary>
    internal sealed class MinimalColumn : PositionedMenuObject
    {
        public MinimalColumn(Menu.Menu menu, MenuObject owner, Vector2 pos)
            : base(menu, owner, pos)
        {
        }
    }

    /// <summary>
    /// One selectable line of the menu: a label, usually a value, and a thin marker that shows the selection. It is a
    /// ButtonTemplate like the game's own buttons, so the menu's mouse hover, keyboard and controller navigation, click
    /// handling and button sounds all work on it without any extra code. What the vanilla buttons cannot do is stay
    /// selected while left and right are used, so a cycler reads those itself. That is how the game's sliders work as
    /// well: they read the menu's input while they are selected, and the menu is told, through nextSelectable, that
    /// left and right lead nowhere.
    /// </summary>
    internal sealed class MinimalRow : ButtonTemplate
    {
        /// <summary>Height of a row. It is also the distance between two rows and the height of the mouse area.</summary>
        public const float RowHeight = 28f;

        /// <summary>Where the values start, counted from the left edge of the labels, unless a long label pushes them out.</summary>
        public const float DefaultValueOffset = 220f;

        // The mouse area starts this far left of the label, over the marker, and ends this far right of the last thing drawn.
        // The row's own origin is that left edge, so the column's origin lands on the labels' left edge.
        private const float HitPadLeft = 22f;
        private const float HitPadRight = 10f;

        // The selection marker: a short 2 px bar, centred MarkerGap left of the label, sliding in from MarkerSlide further out.
        private const float MarkerGap = 12f;
        private const float MarkerSlide = 5f;
        private const float MarkerWidth = 2f;
        private const float MarkerHeight = 14f;

        // The < and > beside the value of a selected cycler. A click within BackZone left of the value goes backwards.
        private const float GlyphGap = 8f;
        private const float BackZone = 26f;

        // A value wider than this is scaled down, down to MinValueScale, instead of running over the screen.
        private const float MaxValueWidth = 260f;
        private const float MinValueScale = 0.6f;

        // Holding left or right repeats after the delays the menu itself uses for repeating a selection move.
        private const int RepeatDelay = 20;
        private const int RepeatInterval = 6;

        private static readonly Color DarkGrey = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.DarkGrey);
        private static readonly Color MediumGrey = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);
        private static readonly Color White = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.White);

        private readonly string signal;
        private readonly string backSignal;
        private readonly MenuLabel nameLabel;
        private readonly MenuLabel valueLabel;
        private readonly MenuLabel backGlyph;
        private readonly MenuLabel forwardGlyph;
        private readonly FSprite marker;
        private readonly float glyphWidth;

        private float valueOffset = DefaultValueOffset;
        private float valueWidth;
        private string valueText = string.Empty;

        // Animation. Every value moves a little each tick (Custom.LerpAndTick) and is blended with last tick's in GrafUpdate.
        private float selectFade;
        private float lastSelectFade;
        private float pulse;
        private float lastPulse;
        private float glyphFade;
        private float lastGlyphFade;
        private float backHot;
        private float lastBackHot;
        private float forwardHot;
        private float lastForwardHot;

        private int holdTicks;
        private int repeatTicks;

        public MinimalRow(Menu.Menu menu, MenuObject owner, MinimalRowKind kind, string labelText, string signal, string backSignal = null)
            : base(menu, owner, new Vector2(-HitPadLeft, 0f), new Vector2(HitPadLeft + HitPadRight, RowHeight))
        {
            Kind = kind;
            this.signal = signal;
            this.backSignal = backSignal;

            nameLabel = AddLabel(labelText, HitPadLeft, FLabelAlignment.Left);

            if (kind != MinimalRowKind.Action)
            {
                valueLabel = AddLabel(string.Empty, HitPadLeft + valueOffset, FLabelAlignment.Left);
            }

            if (kind == MinimalRowKind.Cycler)
            {
                backGlyph = AddLabel("<", HitPadLeft + valueOffset - GlyphGap, FLabelAlignment.Right);
                forwardGlyph = AddLabel(">", HitPadLeft + valueOffset, FLabelAlignment.Left);
                backGlyph.label.isVisible = false;
                forwardGlyph.label.isVisible = false;
                glyphWidth = forwardGlyph.label.textRect.width;
            }

            marker = new FSprite("pixel") { scaleX = MarkerWidth, scaleY = MarkerHeight, isVisible = false };
            Container.AddChild(marker);

            UpdateLayout();
        }

        public MinimalRowKind Kind { get; }

        /// <summary>A greyed-out row keeps its place in the navigation but cannot be activated.</summary>
        public bool Enabled
        {
            get => !buttonBehav.greyedOut;
            set => buttonBehav.greyedOut = !value;
        }

        /// <summary>Width of the label in pixels, which the menu uses to find where the values can start.</summary>
        public float LabelWidth => nameLabel.label.textRect.width;

        /// <summary>Moves the values to this distance from the left edge of the labels.</summary>
        public void SetValueColumn(float offset)
        {
            valueOffset = offset;
            UpdateLayout();
        }

        public void SetValue(string text)
        {
            if (valueLabel == null)
            {
                return;
            }

            text = text ?? string.Empty;
            if (string.Equals(text, valueText, StringComparison.Ordinal))
            {
                return;
            }

            valueText = text;
            valueLabel.text = text;

            // A long name (custom regions, translations) is scaled down. An empty label has no meaningful width.
            float width = text.Length > 0 ? valueLabel.label.textRect.width : 0f;
            valueLabel.label.scale = width > MaxValueWidth ? Mathf.Max(MinValueScale, MaxValueWidth / width) : 1f;
            valueWidth = width * valueLabel.label.scale;
            UpdateLayout();
        }

        private MenuLabel AddLabel(string text, float x, FLabelAlignment alignment)
        {
            // The label has no width: its x is the edge the text is aligned to. Its height is the row's, so the text is
            // centred on the row.
            MenuLabel label = new MenuLabel(menu, this, text, new Vector2(x, 0f), new Vector2(0f, RowHeight), bigText: false);
            label.label.alignment = alignment;
            subObjects.Add(label);
            return label;
        }

        private static void MoveLabelX(MenuLabel label, float x)
        {
            label.pos = new Vector2(x, label.pos.y);
            label.lastPos = label.pos;
        }

        // Places the value and the glyphs and sizes the mouse area to what is drawn.
        private void UpdateLayout()
        {
            if (Kind == MinimalRowKind.Action)
            {
                size.x = HitPadLeft + LabelWidth + HitPadRight;
                return;
            }

            float valueLeft = HitPadLeft + valueOffset;
            MoveLabelX(valueLabel, valueLeft);

            float right = valueLeft + valueWidth;
            if (Kind == MinimalRowKind.Cycler)
            {
                MoveLabelX(backGlyph, valueLeft - GlyphGap);
                MoveLabelX(forwardGlyph, valueLeft + valueWidth + GlyphGap);
                right += GlyphGap + glyphWidth;
            }

            size.x = right + HitPadRight;
        }

        public override void Update()
        {
            lastSelectFade = selectFade;
            lastPulse = pulse;
            lastGlyphFade = glyphFade;
            lastBackHot = backHot;
            lastForwardHot = forwardHot;

            // Clicks are found in here (ButtonBehavior calls Clicked), so an activation has set pulse and the hot
            // values by the time they are moved on below.
            base.Update();

            bool selected = Selected;
            bool cycles = Kind == MinimalRowKind.Cycler && Enabled;

            // Quick to light up, slower to fade, so the row that was just left trails off.
            selectFade = selected
                ? Custom.LerpAndTick(selectFade, 1f, 0.3f, 0.08f)
                : Custom.LerpAndTick(selectFade, 0f, 0.15f, 0.04f);
            pulse = Custom.LerpAndTick(pulse, 0f, 0.1f, 0.05f);
            glyphFade = Custom.LerpAndTick(glyphFade, selected && cycles ? 1f : 0f, 0.3f, 0.08f);

            // With the mouse, the glyph that a click would use stays lit: "<" over its own area, ">" everywhere else.
            bool mouse = selected && cycles && menu.manager.menuesMouseMode;
            bool overBack = mouse && OverBackZone();
            backHot = Custom.LerpAndTick(backHot, overBack ? 1f : 0f, 0.3f, 0.1f);
            forwardHot = Custom.LerpAndTick(forwardHot, mouse && !overBack ? 1f : 0f, 0.3f, 0.1f);

            if (selected && cycles)
            {
                HandleSideInput();
            }
            else
            {
                holdTicks = 0;
                repeatTicks = 0;
            }
        }

        // Left and right change a selected cycler. The menu cannot move the selection sideways from here, because
        // nextSelectable[0] and [2] point at the row itself, so the input is free to be used. Like the game's sliders
        // this only reads the input while the menu is in keyboard or controller mode; in mouse mode a key press just
        // switches the mode.
        private void HandleSideInput()
        {
            int x = menu.input.x;
            if (x == 0 || menu.manager.menuesMouseMode || menu.GetFreezeMenuFunctions())
            {
                holdTicks = 0;
                repeatTicks = 0;
                return;
            }

            if (menu.lastInput.x != x)
            {
                // A new press, or a switch from one direction to the other.
                holdTicks = 0;
                repeatTicks = 0;
                Activate(x);
            }
            else if (menu.input.y == 0)
            {
                holdTicks++;
                if (holdTicks > RepeatDelay)
                {
                    repeatTicks++;
                    if (repeatTicks > RepeatInterval)
                    {
                        repeatTicks = 0;
                        Activate(x);
                    }
                }
            }
        }

        // The mouse is in screen space, like ScreenPos. The value starts at the label's own screen position.
        private bool OverBackZone()
        {
            float valueLeft = valueLabel.ScreenPos.x;
            float mouseX = menu.mousePosition.x;
            return mouseX >= valueLeft - BackZone && mouseX < valueLeft;
        }

        public override void Clicked()
        {
            if (buttonBehav.greyedOut)
            {
                return;
            }

            // Confirm and a click go forward. A click on the "<" goes back.
            bool back = Kind == MinimalRowKind.Cycler && menu.manager.menuesMouseMode && OverBackZone();
            Activate(back ? -1 : 1);
        }

        private void Activate(int direction)
        {
            pulse = 1f;
            if (Kind == MinimalRowKind.Cycler)
            {
                if (direction < 0)
                {
                    backHot = 1f;
                }
                else
                {
                    forwardHot = 1f;
                }
            }

            Singal(this, direction < 0 && backSignal != null ? backSignal : signal);
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);

            float selected = Mathf.Lerp(lastSelectFade, selectFade, timeStacker);
            float flash = Mathf.Lerp(lastPulse, pulse, timeStacker);
            bool enabled = Enabled;

            // The label is DarkGrey and the value MediumGrey; the selected row brightens by one step. An action has no
            // value to carry the brightness, so its label goes all the way to White.
            Color labelColor = DarkGrey;
            Color valueColor = DarkGrey;
            if (enabled)
            {
                labelColor = Color.Lerp(Color.Lerp(DarkGrey, Kind == MinimalRowKind.Action ? White : MediumGrey, selected), White, flash);
                valueColor = Color.Lerp(Color.Lerp(MediumGrey, White, selected), White, flash);
            }

            nameLabel.label.color = labelColor;
            if (valueLabel != null)
            {
                valueLabel.label.color = valueColor;
            }

            if (backGlyph != null)
            {
                float glyphs = Mathf.Lerp(lastGlyphFade, glyphFade, timeStacker);
                ShowGlyph(backGlyph, glyphs, Mathf.Lerp(lastBackHot, backHot, timeStacker));
                ShowGlyph(forwardGlyph, glyphs, Mathf.Lerp(lastForwardHot, forwardHot, timeStacker));
            }

            marker.isVisible = selected > 0.01f;
            if (marker.isVisible)
            {
                Vector2 drawPos = DrawPos(timeStacker);
                marker.x = drawPos.x + HitPadLeft - MarkerGap - (1f - selected) * MarkerSlide;
                marker.y = drawPos.y + RowHeight / 2f;
                marker.alpha = selected;

                // A greyed-out row is still reachable with the keyboard, so it still shows where the selection is.
                marker.color = enabled ? White : MediumGrey;
            }
        }

        private static void ShowGlyph(MenuLabel glyph, float visible, float hot)
        {
            glyph.label.isVisible = visible > 0.01f;
            if (glyph.label.isVisible)
            {
                glyph.label.alpha = visible;
                glyph.label.color = Color.Lerp(MediumGrey, White, hot);
            }
        }

        public override void RemoveSprites()
        {
            marker.RemoveFromContainer();
            base.RemoveSprites();
        }
    }

    /// <summary>
    /// A soft darkening at the left of the screen, behind the text column and nowhere else. It is a stack of black
    /// FlatLight blobs, which is "Futile_White" drawn with the FlatLight shader: the recipe of Menu.DarkGradient and of the
    /// HUD's shadows. Their centres sit by the left edge of the screen, so the darkening is strongest there and has faded
    /// out by the middle of the screen.
    /// </summary>
    internal sealed class MinimalShade
    {
        private readonly FSprite[] blobs;

        public MinimalShade(FContainer parent, FShader flatLight, int count, float alpha)
        {
            blobs = new FSprite[count];
            for (int i = 0; i < blobs.Length; i++)
            {
                blobs[i] = new FSprite("Futile_White") { shader = flatLight, color = Color.black, alpha = alpha };
                parent.AddChild(blobs[i]);
            }
        }

        /// <summary>
        /// Spreads the blobs evenly from <paramref name="bottom"/> to <paramref name="top"/>, the first and last centred on
        /// those two edges. Each reaches <paramref name="radiusX"/> to the right of <paramref name="centerX"/> and a bit
        /// more than the distance to its neighbour up and down, so the darkness stays about even along the column and has
        /// faded out a little beyond its ends. Sprites are not moved by the menu's page, so these are plain screen
        /// coordinates.
        /// </summary>
        public void Layout(float centerX, float radiusX, float bottom, float top)
        {
            float height = top - bottom;
            float spacing = blobs.Length > 1 ? height / (blobs.Length - 1) : 0f;
            float radiusY = blobs.Length > 1 ? spacing * 1.25f : height * 0.6f;
            for (int i = 0; i < blobs.Length; i++)
            {
                blobs[i].x = centerX;
                blobs[i].y = blobs.Length > 1 ? bottom + spacing * i : bottom + height * 0.5f;
                blobs[i].width = radiusX * 2f;
                blobs[i].height = radiusY * 2f;
            }
        }

        public void Remove()
        {
            for (int i = 0; i < blobs.Length; i++)
            {
                blobs[i].RemoveFromContainer();
            }
        }
    }
}
