using System;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    internal class WallpaperSettingsOverlay
    {
        private readonly WallpaperController controller;
        private readonly FContainer hostContainer;
        private readonly FContainer container;

        private readonly FLabel titleLabel;
        private readonly FLabel durationLabel;
        private readonly FLabel hudModeLabel;
        private readonly FLabel instructionsLabel;
        private readonly FLabel closeLabel;

        private bool isVisible;

        public bool IsVisible => isVisible;

        public WallpaperSettingsOverlay(RoomCamera camera, WallpaperController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));

            // Add directly to Futile stage like the HUD does
            hostContainer = Futile.stage;
            container = new FContainer();
            hostContainer.AddChild(container);
            container.MoveToFront();

            titleLabel = CreateLabel(100f, 520f, "Wallpaper Settings");
            titleLabel.scale = 1.2f;

            durationLabel = CreateLabel(100f, 490f, string.Empty);
            hudModeLabel = CreateLabel(100f, 460f, string.Empty);
            instructionsLabel = CreateLabel(100f, 420f, "Left/Right -> adjust +/- 1 min | Shift+Left/Right -> adjust +/- 5 min | H -> toggle HUD");
            instructionsLabel.scale = 0.9f;
            instructionsLabel.color = new Color(0.7f, 0.85f, 1f, 0.85f);

            closeLabel = CreateLabel(100f, 390f, "Press F1 or Tab to close");
            closeLabel.scale = 0.9f;
            closeLabel.color = new Color(0.7f, 0.85f, 1f, 0.65f);

            container.AddChild(titleLabel);
            container.AddChild(durationLabel);
            container.AddChild(hudModeLabel);
            container.AddChild(instructionsLabel);
            container.AddChild(closeLabel);

            container.isVisible = false;
            container.alpha = 0f;
            isVisible = false;

            Refresh();
        }

        public void SetVisible(bool visible)
        {
            if (container == null)
            {
                return;
            }

            if (visible)
            {
                hostContainer.AddChild(container);
                container.MoveToFront();
            }

            container.isVisible = visible;
            container.alpha = visible ? 1f : 0f;
            isVisible = visible;
        }

        public void Refresh()
        {
            if (container == null)
            {
                return;
            }

            float minutes = controller.RegionDurationSeconds / 60f;
            durationLabel.text = $"Region Duration: {minutes:F1} minutes";

            bool hudAlwaysVisible = controller.Hud?.AlwaysShowHUD ?? false;
            hudModeLabel.text = $"HUD Always Visible: {(hudAlwaysVisible ? "ON" : "OFF")} (press H to toggle)";
        }

        public void Destroy()
        {
            container?.RemoveFromContainer();
        }

        private FLabel CreateLabel(float x, float y, string text)
        {
            return new FLabel("font", text)
            {
                x = x,
                y = y,
                alignment = FLabelAlignment.Left,
                color = new Color(0f, 0.85f, 1f, 1f)
            };
        }
    }
}
