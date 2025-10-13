using System;
using UnityEngine;
using RWCustom;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Auto-hiding HUD for Wallpaper Mode
    /// Shows current location, next location, and region progress
    /// Fades out after inactivity, reappears on mouse movement
    /// </summary>
    public class WallpaperHUD
    {
        private RoomCamera camera;
        private WallpaperController controller;

        // HUD Elements
        private FContainer hudContainer;
        private FLabel currentLocationLabel;
        private FLabel nextLocationLabel;
        private FLabel previousLocationLabel;
        private FLabel regionProgressLabel;

        // Mouse tracking
        private Vector2 lastMousePosition;
        private float idleTimer = 0f;
        private float fadeDelay = 3f; // Fade after 3 seconds

        // Fade state
        private float currentAlpha = 1f;
        private float targetAlpha = 1f;
        private bool isVisible = true;

        // Configuration (will be loaded from config later)
        private bool showNextLocation = true;
        private bool showPreviousLocation = false;
        private bool alwaysShowHUD = false;

        public bool IsReady { get; private set; }

        public WallpaperHUD(RoomCamera camera, WallpaperController controller)
        {
            this.camera = camera;
            this.controller = controller;

            InitializeHUD();
            lastMousePosition = UnityEngine.Input.mousePosition;
        }

        private void InitializeHUD()
        {
            if (camera.hud == null || camera.hud.fContainers == null || camera.hud.fContainers.Length == 0)
            {
                WallpaperMod.Log?.LogWarning("WallpaperHUD: Cannot initialize, no HUD container");
                IsReady = false;
                return;
            }

            // Create main container
            hudContainer = new FContainer();
            camera.hud.fContainers[0].AddChild(hudContainer);

            // Create labels
            currentLocationLabel = CreateLabel(100f, 700f, "");
            nextLocationLabel = CreateLabel(100f, 670f, "");
            previousLocationLabel = CreateLabel(100f, 640f, "");
            regionProgressLabel = CreateLabel(100f, 610f, "");

            hudContainer.AddChild(currentLocationLabel);
            if (showNextLocation) hudContainer.AddChild(nextLocationLabel);
            if (showPreviousLocation) hudContainer.AddChild(previousLocationLabel);
            hudContainer.AddChild(regionProgressLabel);

            WallpaperMod.Log?.LogInfo("WallpaperHUD: Initialized");
            IsReady = true;
        }

        private FLabel CreateLabel(float x, float y, string text)
        {
            return new FLabel("font", text)
            {
                x = x,
                y = y,
                alignment = FLabelAlignment.Left,
                color = new Color(0f, 0.8f, 1f, 1f) // Bright cyan
            };
        }

        public void Update()
        {
            if (currentLocationLabel == null)
            {
                return;
            }

            // Check mouse movement
            Vector2 currentMousePos = UnityEngine.Input.mousePosition;

            if (Vector2.Distance(currentMousePos, lastMousePosition) > 1f)
            {
                // Mouse moved - show HUD
                OnMouseMoved();
                lastMousePosition = currentMousePos;
            }

            // Update idle timer
            if (!alwaysShowHUD && isVisible)
            {
                idleTimer += Time.deltaTime;

                if (idleTimer >= fadeDelay)
                {
                    // Start fade out
                    targetAlpha = 0f;
                    isVisible = false;
                }
            }

            // Animate alpha
            if (currentAlpha != targetAlpha)
            {
                float fadeSpeed = 2f; // Fade over 0.5 seconds
                currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
                UpdateAlpha();
            }

            // Update text
            UpdateLabels();
        }

        private void OnMouseMoved()
        {
            if (!alwaysShowHUD && !isVisible)
            {
                // Fade back in
                targetAlpha = 1f;
                isVisible = true;
            }

            // Reset idle timer
            idleTimer = 0f;
        }

        private void UpdateAlpha()
        {
            if (hudContainer != null)
            {
                hudContainer.alpha = currentAlpha;
            }
        }

        private void UpdateLabels()
        {
            if (controller.RegionManager == null || currentLocationLabel == null)
            {
                return;
            }

            // Current location
            string currentRegion = controller.CurrentRegionCode;
            string currentRoom = controller.CurrentRoomName;
            currentLocationLabel.text = $"Current: {GetRegionName(currentRegion)}{FormatRoomName(currentRoom)}";

            // Next location (placeholder)
            if (showNextLocation && nextLocationLabel != null)
            {
                string nextRoom = controller.NextRoomName;
                nextLocationLabel.text = string.IsNullOrEmpty(nextRoom)
                    ? "Next: [Calculating...]"
                    : $"Next: {nextRoom}";
            }

            // Previous location (placeholder)
            if (showPreviousLocation && previousLocationLabel != null)
            {
                string previousRoom = controller.PreviousRoomName;
                previousLocationLabel.text = string.IsNullOrEmpty(previousRoom)
                    ? "Previous: [N/A]"
                    : $"Previous: {previousRoom}";
            }

            // Region progress
            int regionsExplored = controller.RegionsExplored;
            int totalRegions = controller.TotalRegions;
            int roomsExplored = controller.RoomsExploredInRegion;
            int roomsTarget = controller.RoomsPerRegion > 0 ? controller.RoomsPerRegion : 20;

            regionProgressLabel.text = $"Region: {regionsExplored}/{totalRegions} | Rooms: {roomsExplored}/{roomsTarget}";
        }

        private string GetRegionName(string regionCode)
        {
            // Map region codes to full names
            switch (regionCode)
            {
                case "SU": return "Outskirts";
                case "HI": return "Industrial Complex";
                case "CC": return "Chimney Canopy";
                case "GW": return "Garbage Wastes";
                case "SH": return "Shaded Citadel";
                case "DS": return "Drainage System";
                case "SL": return "Shoreline";
                case "SI": return "Sky Islands";
                case "LF": return "Farm Arrays";
                case "UW": return "The Exterior";
                case "SS": return "Five Pebbles";
                case "SB": return "Subterranean";
                case "LM": return "Looks to the Moon";
                case "RM": return "Pipeyard";
                case "DM": return "Metropolis";
                case "LC": return "Outer Expanse";
                case "MS": return "Waterfront Facility";
                case "VS": return "Undergrowth";
                case "CL": return "Silent Construct";
                case "OE": return "Rubicon";
                default: return regionCode;
            }
        }

        private string FormatRoomName(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                return string.Empty;
            }

            return $" - {roomName}";
        }

        public void Destroy()
        {
            if (hudContainer != null)
            {
                hudContainer.RemoveFromContainer();
            }

            WallpaperMod.Log?.LogInfo("WallpaperHUD: Destroyed");
            IsReady = false;
        }
    }
}
