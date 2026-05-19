using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly FLabel chaosLabel;
        private readonly FLabel chaosLevelLabel;
        private readonly FLabel chaosSpawnAllLabel;
        private readonly FLabel noRainTransitionLabel;
        private readonly FLabel chaosWarningLabel;
        private readonly FLabel instructionsLabel;
        private readonly FLabel closeLabel;

        // Quick travel UI
        private readonly FLabel quickTravelTitle;
        private readonly FLabel campaignLabel;
        private readonly FLabel regionLabel;
        private readonly FLabel cameraModeLabel;
        private readonly FLabel rotStateLabel;
        private readonly FLabel roomLabel;
        private readonly FLabel lockLabel;
        private readonly FLabel travelInstructions;

        private List<string> availableCampaigns;
        private List<string> availableRegions;
        private List<string> availableCameraModes;
        private List<string> availableRotStates;
        private List<string> availableRooms;
        private int selectedCampaignIndex;
        private int selectedRegionIndex;
        private int selectedCameraModeIndex;
        private int selectedRotStateIndex;
        private int selectedRoomIndex;

        // Focus tracking: 0 = campaign, 1 = region, 2 = camera mode, 3 = Watcher state, 4 = room, 5 = lock, 6 = chaos mode, 7 = chaos level, 8 = chaos spawn all, 9 = no rain transition
        private int currentFocus = 0;

        private bool isVisible;

        public bool IsVisible => isVisible;

        public WallpaperSettingsOverlay(RoomCamera camera, WallpaperController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));

            // Initialize campaign and region lists
            InitializeSelectionLists();

            // Add directly to Futile stage like the HUD does
            hostContainer = Futile.stage;
            container = new FContainer();
            hostContainer.AddChild(container);
            container.MoveToFront();

            titleLabel = CreateLabel(100f, 520f, "Wallpaper Settings");
            titleLabel.scale = 1.2f;

            durationLabel = CreateLabel(100f, 490f, string.Empty);
            hudModeLabel = CreateLabel(100f, 460f, string.Empty);

            chaosLabel = CreateLabel(100f, 430f, string.Empty);
            chaosLevelLabel = CreateLabel(100f, 400f, string.Empty);
            chaosSpawnAllLabel = CreateLabel(100f, 370f, string.Empty);
            noRainTransitionLabel = CreateLabel(100f, 340f, string.Empty);
            chaosWarningLabel = CreateLabel(100f, 310f, "⚠️ Chaos changes apply on next region");
            chaosWarningLabel.scale = 0.85f;
            chaosWarningLabel.color = new Color(1f, 0.7f, 0f, 0.85f);

            instructionsLabel = CreateLabel(100f, 280f, "H -> toggle HUD | Regions change automatically when rain starts");
            instructionsLabel.scale = 0.9f;
            instructionsLabel.color = new Color(0.7f, 0.85f, 1f, 0.85f);

            closeLabel = CreateLabel(100f, 250f, "Press F1 or Tab to close");
            closeLabel.scale = 0.9f;
            closeLabel.color = new Color(0.7f, 0.85f, 1f, 0.65f);

            // Quick travel UI
            quickTravelTitle = CreateLabel(100f, 210f, "=== Quick Travel ===");
            quickTravelTitle.scale = 1.1f;
            quickTravelTitle.color = new Color(1f, 0.85f, 0f, 1f);

            campaignLabel = CreateLabel(100f, 180f, string.Empty);
            regionLabel = CreateLabel(100f, 150f, string.Empty);
            cameraModeLabel = CreateLabel(100f, 120f, string.Empty);
            rotStateLabel = CreateLabel(100f, 90f, string.Empty);
            roomLabel = CreateLabel(100f, 60f, string.Empty);
            lockLabel = CreateLabel(100f, 30f, string.Empty);

            travelInstructions = CreateLabel(100f, 5f, "Right/D -> Next | Left/A -> Prev | Enter/G -> Apply | L -> Lock");
            travelInstructions.scale = 0.9f;
            travelInstructions.color = new Color(0.7f, 0.85f, 1f, 0.65f);

            container.AddChild(titleLabel);
            container.AddChild(durationLabel);
            container.AddChild(hudModeLabel);
            container.AddChild(chaosLabel);
            container.AddChild(chaosLevelLabel);
            container.AddChild(chaosSpawnAllLabel);
            container.AddChild(noRainTransitionLabel);
            container.AddChild(chaosWarningLabel);
            container.AddChild(instructionsLabel);
            container.AddChild(closeLabel);
            container.AddChild(quickTravelTitle);
            container.AddChild(campaignLabel);
            container.AddChild(regionLabel);
            container.AddChild(cameraModeLabel);
            container.AddChild(rotStateLabel);
            container.AddChild(roomLabel);
            container.AddChild(lockLabel);
            container.AddChild(travelInstructions);

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

                // Refresh room list when opening overlay (in case region changed)
                RefreshRoomList();
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

            // Display rain countdown status
            if (controller.IsNoRainWaitMode)
            {
                float progress = controller.CycleProgress * 100f;
                durationLabel.text = $"No Rain Wait: {progress:F0}% / 95% until region change";
            }
            else if (controller.IsRainCountdownActive)
            {
                float remainingSeconds = controller.RainCountdownRemaining;
                int minutes = (int)(remainingSeconds / 60f);
                int seconds = (int)(remainingSeconds % 60f);
                durationLabel.text = $"Rain Countdown: {minutes}m {seconds}s until region change";
            }
            else
            {
                durationLabel.text = "Rain-based region changes: Waiting for rain...";
            }

            bool hudAlwaysVisible = controller.Hud?.AlwaysShowHUD ?? false;
            hudModeLabel.text = $"HUD Always Visible: {(hudAlwaysVisible ? "ON" : "OFF")} (press H to toggle)";

            // Update chaos labels
            RefreshChaosLabels();

            // Update quick travel labels
            RefreshQuickTravelLabels();
        }

        private void RefreshChaosLabels()
        {
            bool chaosEnabled = WallpaperMod.Options?.EnableChaos.Value ?? false;
            int chaosLevel = WallpaperMod.Options?.ChaosLevel.Value ?? 1;
            bool chaosSpawnAll = WallpaperMod.Options?.ChaosSpawnAll.Value ?? false;
            bool noRainTransition = WallpaperMod.Options?.NoRainTransition.Value ?? false;

            bool isFocusedMode = currentFocus == 6;
            bool isFocusedLevel = currentFocus == 7;
            bool isFocusedSpawnAll = currentFocus == 8;
            bool isFocusedNoRain = currentFocus == 9;

            string modePrefix = isFocusedMode ? ">> " : "   ";
            string levelPrefix = isFocusedLevel ? ">> " : "   ";
            string spawnAllPrefix = isFocusedSpawnAll ? ">> " : "   ";
            string noRainPrefix = isFocusedNoRain ? ">> " : "   ";

            chaosLabel.text = $"{modePrefix}Chaos Mode: [{(chaosEnabled ? "ON" : "OFF")}]";
            chaosLabel.color = isFocusedMode ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);

            chaosLevelLabel.text = $"{levelPrefix}Chaos Level: < {chaosLevel} >";
            chaosLevelLabel.color = isFocusedLevel ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);

            chaosSpawnAllLabel.text = $"{spawnAllPrefix}Spawn ALL: [{(chaosSpawnAll ? "ON" : "OFF")}] (experimental!)";
            chaosSpawnAllLabel.color = isFocusedSpawnAll ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);

            noRainTransitionLabel.text = $"{noRainPrefix}No Rain Wait: [{(noRainTransition ? "ON" : "OFF")}]";
            noRainTransitionLabel.color = isFocusedNoRain ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);
        }

        private void RefreshQuickTravelLabels()
        {
            if (availableCampaigns != null && availableCampaigns.Count > 0)
            {
                string campaignName = GetCampaignDisplayName(availableCampaigns[selectedCampaignIndex]);
                bool isFocused = currentFocus == 0;
                string prefix = isFocused ? ">> " : "   ";
                campaignLabel.text = $"{prefix}Campaign:  < {campaignName} >";
                campaignLabel.color = isFocused ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);
            }

            if (availableRegions != null && availableRegions.Count > 0)
            {
                string regionName = GetRegionDisplayName(availableRegions[selectedRegionIndex]);
                bool isFocused = currentFocus == 1;
                string prefix = isFocused ? ">> " : "   ";
                regionLabel.text = $"{prefix}Region:    < {regionName} >";
                regionLabel.color = isFocused ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);
            }

            if (availableCameraModes != null && availableCameraModes.Count > 0)
            {
                string modeName = GetCameraModeDisplayName(availableCameraModes[selectedCameraModeIndex]);
                bool isFocused = currentFocus == 2;
                string prefix = isFocused ? ">> " : "   ";
                cameraModeLabel.text = $"{prefix}Camera:    < {modeName} >";
                cameraModeLabel.color = isFocused ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);
            }

            if (availableRotStates != null && availableRotStates.Count > 0)
            {
                bool watcherCampaign = IsSelectedCampaignWatcher();
                string rotStateName = watcherCampaign ? GetRotStateDisplayName(availableRotStates[selectedRotStateIndex]) : "Watcher campaign only";
                bool isFocused = currentFocus == 3;
                string prefix = isFocused ? ">> " : "   ";
                rotStateLabel.text = $"{prefix}Watcher:   < {rotStateName} >";
                rotStateLabel.color = watcherCampaign
                    ? (isFocused ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f))
                    : new Color(0.5f, 0.5f, 0.5f, 0.8f);
            }

            if (availableRooms != null && availableRooms.Count > 0)
            {
                bool regionMatches = IsSelectedRegionCurrent();
                string roomName = availableRooms[selectedRoomIndex];
                bool isFocused = currentFocus == 4;
                string prefix = isFocused ? ">> " : "   ";

                if (!regionMatches)
                {
                    // Show that room selection is disabled when region doesn't match
                    roomLabel.text = $"{prefix}Room:      < Random > (travel to region first)";
                    roomLabel.color = new Color(0.5f, 0.5f, 0.5f, 0.8f); // Gray out
                }
                else
                {
                    roomLabel.text = $"{prefix}Room:      < {roomName} >";
                    roomLabel.color = isFocused ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);
                }
            }

            // Lock toggle
            bool isLocked = controller?.IsRoomLocked ?? false;
            bool isFocusedLock = currentFocus == 5;
            string lockPrefix = isFocusedLock ? ">> " : "   ";
            lockLabel.text = $"{lockPrefix}Lock Room: [{(isLocked ? "ON" : "OFF")}]";
            lockLabel.color = isFocusedLock ? new Color(1f, 0.85f, 0f, 1f) : new Color(0f, 0.85f, 1f, 1f);
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

        private void InitializeSelectionLists()
        {
            // Get all campaigns from enum
            availableCampaigns = Enum.GetNames(typeof(WallpaperModOptions.CampaignChoice)).ToList();
            selectedCampaignIndex = 0;

            // Try to match current campaign
            if (WallpaperMod.Options != null)
            {
                string currentCampaign = WallpaperMod.Options.SelectedCampaign.Value;
                int index = availableCampaigns.FindIndex(c => c == currentCampaign);
                if (index >= 0)
                {
                    selectedCampaignIndex = index;
                }
            }

            RefreshRegionList(preferredRegion: controller?.RegionMgr?.GetCurrentRegion());

            // Get all camera modes from enum
            availableCameraModes = Enum.GetNames(typeof(WallpaperModOptions.CameraMode)).ToList();
            selectedCameraModeIndex = 0;

            // Try to match current camera mode
            if (WallpaperMod.Options != null)
            {
                string currentMode = WallpaperMod.Options.CameraModeConfig.Value;
                int index = availableCameraModes.FindIndex(m => m == currentMode);
                if (index >= 0)
                {
                    selectedCameraModeIndex = index;
                }
            }

            availableRotStates = Enum.GetNames(typeof(WallpaperModOptions.RotState)).ToList();
            selectedRotStateIndex = 0;

            if (WallpaperMod.Options != null)
            {
                string currentRotState = WallpaperMod.Options.RotStateConfig.Value;
                int index = availableRotStates.FindIndex(s => s == currentRotState);
                if (index >= 0)
                {
                    selectedRotStateIndex = index;
                }
            }

            // Get all rooms in current region
            RefreshRoomList();
        }

        private void RefreshRoomList()
        {
            availableRooms = new List<string> { "Random" };
            selectedRoomIndex = 0;

            if (controller?.Game?.world?.abstractRooms != null)
            {
                foreach (var room in controller.Game.world.abstractRooms)
                {
                    if (room != null && !room.gate && !string.IsNullOrEmpty(room.name))
                    {
                        availableRooms.Add(room.name);
                    }
                }

                // Try to match current room
                string currentRoom = controller.CurrentRoomName;
                if (!string.IsNullOrEmpty(currentRoom))
                {
                    int index = availableRooms.FindIndex(r => string.Equals(r, currentRoom, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        selectedRoomIndex = index;
                    }
                }
            }
        }

        private void RefreshRegionList(string preferredRegion = null)
        {
            string selectedCampaign = availableCampaigns != null && availableCampaigns.Count > 0
                ? availableCampaigns[selectedCampaignIndex]
                : WallpaperMod.Options?.SelectedCampaign.Value;

            availableRegions = WallpaperRegionCatalog.GetRegionsForCampaign(selectedCampaign).ToList();
            selectedRegionIndex = 0;

            string regionToSelect = !string.IsNullOrWhiteSpace(preferredRegion)
                ? preferredRegion
                : WallpaperRegionCatalog.GetDefaultStartRegionForCampaign(selectedCampaign);

            int index = availableRegions.FindIndex(r => string.Equals(r, regionToSelect, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                selectedRegionIndex = index;
            }
        }

        public void CycleFocus(int direction)
        {
            currentFocus = (currentFocus + direction + 10) % 10;
            RefreshQuickTravelLabels();
            RefreshChaosLabels();
        }

        public void CycleCurrentSelection(int direction)
        {
            if (currentFocus == 0)
            {
                CycleCampaign(direction);
            }
            else if (currentFocus == 1)
            {
                CycleRegion(direction);
            }
            else if (currentFocus == 2)
            {
                CycleCameraMode(direction);
            }
            else if (currentFocus == 3)
            {
                if (IsSelectedCampaignWatcher())
                {
                    CycleRotState(direction);
                }
            }
            else if (currentFocus == 4)
            {
                // Only allow room cycling if selected region matches current region
                if (IsSelectedRegionCurrent())
                {
                    CycleRoom(direction);
                }
                // Otherwise do nothing - room selection disabled
            }
            else if (currentFocus == 5)
            {
                // Lock is toggled, not cycled
                controller?.ToggleRoomLock();
                RefreshQuickTravelLabels();
            }
            else if (currentFocus == 6)
            {
                // Chaos mode toggle
                if (WallpaperMod.Options != null)
                {
                    WallpaperMod.Options.EnableChaos.Value = !WallpaperMod.Options.EnableChaos.Value;
                    RefreshChaosLabels();
                }
            }
            else if (currentFocus == 7)
            {
                // Chaos level cycling (1-10)
                if (WallpaperMod.Options != null)
                {
                    int currentLevel = WallpaperMod.Options.ChaosLevel.Value;
                    int newLevel = currentLevel + direction;

                    // Wrap around between 1 and 10
                    if (newLevel < 1) newLevel = 10;
                    if (newLevel > 10) newLevel = 1;

                    WallpaperMod.Options.ChaosLevel.Value = newLevel;
                    RefreshChaosLabels();
                }
            }
            else if (currentFocus == 8)
            {
                // Chaos spawn all toggle
                if (WallpaperMod.Options != null)
                {
                    WallpaperMod.Options.ChaosSpawnAll.Value = !WallpaperMod.Options.ChaosSpawnAll.Value;
                    RefreshChaosLabels();
                }
            }
            else if (currentFocus == 9)
            {
                // No rain transition toggle
                if (WallpaperMod.Options != null)
                {
                    WallpaperMod.Options.NoRainTransition.Value = !WallpaperMod.Options.NoRainTransition.Value;
                    RefreshChaosLabels();
                }
            }
        }

        private bool IsSelectedRegionCurrent()
        {
            if (availableRegions == null || availableRegions.Count == 0) return false;
            if (controller?.RegionMgr == null) return false;

            string selectedRegion = availableRegions[selectedRegionIndex];
            string currentRegion = controller.RegionMgr.GetCurrentRegion();

            return string.Equals(selectedRegion, currentRegion, StringComparison.OrdinalIgnoreCase);
        }

        private void CycleCampaign(int direction)
        {
            if (availableCampaigns == null || availableCampaigns.Count == 0) return;

            selectedCampaignIndex = (selectedCampaignIndex + direction + availableCampaigns.Count) % availableCampaigns.Count;
            RefreshRegionList();
            selectedRoomIndex = 0;
            RefreshQuickTravelLabels();
        }

        private void CycleRegion(int direction)
        {
            if (availableRegions == null || availableRegions.Count == 0) return;

            selectedRegionIndex = (selectedRegionIndex + direction + availableRegions.Count) % availableRegions.Count;

            // When region changes, reset room selection to Random
            selectedRoomIndex = 0; // "Random" is always index 0

            RefreshQuickTravelLabels();
        }

        private void CycleCameraMode(int direction)
        {
            if (availableCameraModes == null || availableCameraModes.Count == 0) return;

            selectedCameraModeIndex = (selectedCameraModeIndex + direction + availableCameraModes.Count) % availableCameraModes.Count;
            RefreshQuickTravelLabels();
        }

        private void CycleRotState(int direction)
        {
            if (availableRotStates == null || availableRotStates.Count == 0) return;

            selectedRotStateIndex = (selectedRotStateIndex + direction + availableRotStates.Count) % availableRotStates.Count;
            if (WallpaperMod.Options != null)
            {
                WallpaperMod.Options.RotStateConfig.Value = availableRotStates[selectedRotStateIndex];
            }
            RefreshQuickTravelLabels();
        }

        private void CycleRoom(int direction)
        {
            if (availableRooms == null || availableRooms.Count == 0) return;

            selectedRoomIndex = (selectedRoomIndex + direction + availableRooms.Count) % availableRooms.Count;
            RefreshQuickTravelLabels();
        }

        public void ApplyTravel()
        {
            if (availableCampaigns == null || availableRegions == null || availableCameraModes == null || availableRotStates == null || availableRooms == null) return;

            string selectedCampaign = availableCampaigns[selectedCampaignIndex];
            string selectedRegion = availableRegions[selectedRegionIndex];
            string selectedCameraMode = availableCameraModes[selectedCameraModeIndex];
            string selectedRotState = IsSelectedCampaignWatcher() ? availableRotStates[selectedRotStateIndex] : WallpaperModOptions.RotState.Natural.ToString();
            string selectedRoom = availableRooms[selectedRoomIndex];

            bool regionChanging = !IsSelectedRegionCurrent();
            string currentCampaign = WallpaperMod.Options?.SelectedCampaign.Value;
            bool campaignChanging = !string.Equals(currentCampaign, selectedCampaign, StringComparison.OrdinalIgnoreCase);

            WallpaperMod.Log?.LogInfo($"Quick Travel: Campaign={selectedCampaign}, Region={selectedRegion}, CameraMode={selectedCameraMode}, RotState={selectedRotState}, Room={selectedRoom}");

            // Update the config
            if (WallpaperMod.Options != null)
            {
                WallpaperMod.Options.SelectedCampaign.Value = selectedCampaign;
                WallpaperMod.Options.StartRegion.Value = selectedRegion;
                WallpaperMod.Options.CameraModeConfig.Value = selectedCameraMode;
                WallpaperMod.Options.RotStateConfig.Value = selectedRotState;
            }

            // Update camera mode in controller
            controller?.SetCameraMode(WallpaperModOptions.GetCameraMode(selectedCameraMode));
            if (campaignChanging)
            {
                controller?.RegionMgr?.OnCampaignChange(selectedRegion);
                regionChanging = true;
            }

            // If changing regions, do that first (room change will happen after region loads)
            if (regionChanging)
            {
                controller?.RequestRegionChange(selectedRegion);
                // Room list will be refreshed when overlay is opened again in new region
            }
            else
            {
                // If staying in same region and a specific room is selected, jump to it
                if (selectedRoom != "Random")
                {
                    controller?.RequestRoomChange(selectedRoom);
                }
            }
        }

        public void ToggleLockShortcut()
        {
            controller?.ToggleRoomLock();
            RefreshQuickTravelLabels();
        }

        private string GetCampaignDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "White": return "Survivor";
                case "Yellow": return "Monk";
                case "Red": return "Hunter";
                case "Gourmand": return "Gourmand";
                case "Artificer": return "Artificer";
                case "Rivulet": return "Rivulet";
                case "Spearmaster": return "Spearmaster";
                case "Saint": return "Saint";
                case "Watcher": return "Watcher";
                default: return enumName;
            }
        }

        private string GetRegionDisplayName(string enumName)
        {
            string selectedCampaign = availableCampaigns != null && availableCampaigns.Count > 0
                ? availableCampaigns[selectedCampaignIndex]
                : WallpaperMod.Options?.SelectedCampaign.Value;

            return WallpaperRegionCatalog.GetDisplayName(enumName, selectedCampaign);
        }

        private bool IsSelectedCampaignWatcher()
        {
            return availableCampaigns != null &&
                availableCampaigns.Count > 0 &&
                string.Equals(availableCampaigns[selectedCampaignIndex], "Watcher", StringComparison.OrdinalIgnoreCase);
        }

        private string GetCameraModeDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "RandomExploration": return "Random Explore";
                case "Random": return "Single Random";
                case "Sequential": return "All Angles";
                case "FirstOnly": return "First Only";
                default: return enumName;
            }
        }

        private string GetRotStateDisplayName(string enumName)
        {
            switch (enumName)
            {
                case "Natural": return "Natural Mix";
                case "Normal": return "Clean";
                case "Rotted": return "Rotted";
                case "Karma": return "Karma Flowers";
                default: return enumName;
            }
        }
    }
}
