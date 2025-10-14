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
        private readonly FLabel instructionsLabel;
        private readonly FLabel closeLabel;

        // Quick travel UI
        private readonly FLabel quickTravelTitle;
        private readonly FLabel campaignLabel;
        private readonly FLabel regionLabel;
        private readonly FLabel travelInstructions;

        private List<string> availableCampaigns;
        private List<string> availableRegions;
        private int selectedCampaignIndex;
        private int selectedRegionIndex;

        // Focus tracking: 0 = campaign, 1 = region
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
            instructionsLabel = CreateLabel(100f, 420f, "Left/Right -> adjust +/- 1 min | Shift+Left/Right -> adjust +/- 5 min | H -> toggle HUD");
            instructionsLabel.scale = 0.9f;
            instructionsLabel.color = new Color(0.7f, 0.85f, 1f, 0.85f);

            closeLabel = CreateLabel(100f, 390f, "Press F1 or Tab to close");
            closeLabel.scale = 0.9f;
            closeLabel.color = new Color(0.7f, 0.85f, 1f, 0.65f);

            // Quick travel UI
            quickTravelTitle = CreateLabel(100f, 350f, "=== Quick Travel ===");
            quickTravelTitle.scale = 1.1f;
            quickTravelTitle.color = new Color(1f, 0.85f, 0f, 1f);

            campaignLabel = CreateLabel(100f, 320f, string.Empty);
            regionLabel = CreateLabel(100f, 290f, string.Empty);

            travelInstructions = CreateLabel(100f, 250f, "Up/Down -> select | Left/Right -> cycle | Enter/G -> travel");
            travelInstructions.scale = 0.9f;
            travelInstructions.color = new Color(0.7f, 0.85f, 1f, 0.65f);

            container.AddChild(titleLabel);
            container.AddChild(durationLabel);
            container.AddChild(hudModeLabel);
            container.AddChild(instructionsLabel);
            container.AddChild(closeLabel);
            container.AddChild(quickTravelTitle);
            container.AddChild(campaignLabel);
            container.AddChild(regionLabel);
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

            // Update quick travel labels
            RefreshQuickTravelLabels();
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

            // Get all regions from enum
            availableRegions = Enum.GetNames(typeof(WallpaperModOptions.RegionChoice)).ToList();
            selectedRegionIndex = 0;

            // Try to match current region
            if (controller?.RegionMgr != null)
            {
                string currentRegion = controller.RegionMgr.GetCurrentRegion();
                int index = availableRegions.FindIndex(r => string.Equals(r, currentRegion, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    selectedRegionIndex = index;
                }
            }
        }

        public void CycleFocus(int direction)
        {
            currentFocus = (currentFocus + direction + 2) % 2;
            RefreshQuickTravelLabels();
        }

        public void CycleCurrentSelection(int direction)
        {
            if (currentFocus == 0)
            {
                CycleCampaign(direction);
            }
            else
            {
                CycleRegion(direction);
            }
        }

        private void CycleCampaign(int direction)
        {
            if (availableCampaigns == null || availableCampaigns.Count == 0) return;

            selectedCampaignIndex = (selectedCampaignIndex + direction + availableCampaigns.Count) % availableCampaigns.Count;
            RefreshQuickTravelLabels();
        }

        private void CycleRegion(int direction)
        {
            if (availableRegions == null || availableRegions.Count == 0) return;

            selectedRegionIndex = (selectedRegionIndex + direction + availableRegions.Count) % availableRegions.Count;
            RefreshQuickTravelLabels();
        }

        public void ApplyTravel()
        {
            if (availableCampaigns == null || availableRegions == null) return;

            string selectedCampaign = availableCampaigns[selectedCampaignIndex];
            string selectedRegion = availableRegions[selectedRegionIndex];

            WallpaperMod.Log?.LogInfo($"Quick Travel: Campaign={selectedCampaign}, Region={selectedRegion}");

            // Update the config
            if (WallpaperMod.Options != null)
            {
                WallpaperMod.Options.SelectedCampaign.Value = selectedCampaign;
                WallpaperMod.Options.StartRegion.Value = selectedRegion;
            }

            // Trigger region change through the controller
            controller?.RequestRegionChange(selectedRegion);
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
                default: return enumName;
            }
        }

        private string GetRegionDisplayName(string enumName)
        {
            switch (enumName)
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
                case "RM": return "Waterfront Facility";
                case "DM": return "Metropolis";
                case "LC": return "Outer Expanse";
                case "MS": return "Submerged Superstructure";
                case "VS": return "Pipeyard";
                case "CL": return "The Rot";
                case "OE": return "Rubicon";
                default: return enumName;
            }
        }
    }
}
