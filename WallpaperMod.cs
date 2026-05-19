using BepInEx;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    [BepInPlugin("com.uzugu.wallpapermod", "Rain World Wallpaper Mode", "1.1.4")]
    public class WallpaperMod : BaseUnityPlugin
    {
        public static WallpaperMod Instance;
        public static WallpaperModOptions Options;

        private bool pendingWallpaperLaunch;
        private bool autoLaunchWallpaperRequested;
        private bool autoLaunchWallpaperConsumed;
        private WallpaperController activeController;
        private string requestedStartRegion = "SU";
        private string commandLineStartRegion;
        private string commandLineCampaign;

        public static ManualLogSource Log { get; private set; }

        public void OnEnable()
        {
            Instance = this;
            Log = Logger;
            Log?.LogInfo("Rain World Wallpaper Mod V2.0 loaded!");

            ParseCommandLineArguments();

            // Don't initialize options here - wait for OnModsInit
            // Options will be created by Remix when needed

            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
            On.ProcessManager.RequestMainProcessSwitch_ProcessID += ProcessManager_RequestMainProcessSwitch;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.RainWorldGame.Update += RainWorldGame_Update;
            On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
            On.RoomCamera.Update += RoomCamera_Update;
            On.Room.Loaded += Room_Loaded;
            On.Player.Update += Player_Update;
            On.Overseer.Update += Overseer_Update;

            MenuIntegration.Initialize();
        }

        // Remix integration - this method is called by the Remix framework
        public static OptionInterface LoadOI()
        {
            if (Options == null)
            {
                Options = new WallpaperModOptions();
            }
            return Options;
        }

        public void BeginWallpaperMode(ProcessManager manager)
        {
            if (manager == null)
            {
                Log?.LogError("BeginWallpaperMode: ProcessManager is null");
                return;
            }

            if (pendingWallpaperLaunch)
            {
                Log?.LogInfo("Wallpaper mode launch already pending");
                return;
            }

            pendingWallpaperLaunch = true;
            requestedStartRegion = ResolveInitialRegion();

            ConfigureMenuSetupForRegion(manager, requestedStartRegion);

            Log?.LogInfo($"Wallpaper mode requested from menu, switching to game process in region {requestedStartRegion}");
            manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
        }

        internal void TryAutoLaunchFromMainMenu(ProcessManager manager)
        {
            if (!autoLaunchWallpaperRequested || autoLaunchWallpaperConsumed || manager == null || pendingWallpaperLaunch)
            {
                return;
            }

            autoLaunchWallpaperConsumed = true;
            Log?.LogInfo("Auto-launching wallpaper mode from command line arguments");
            BeginWallpaperMode(manager);
        }

        internal bool HasPendingAutoLaunchRequest()
        {
            return autoLaunchWallpaperRequested && !autoLaunchWallpaperConsumed && !pendingWallpaperLaunch;
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            // Initialize and register Remix options
            try
            {
                // Create options if not already created by LoadOI
                if (Options == null)
                {
                    Options = new WallpaperModOptions();
                }

                // Register with Remix using underscore format for mod ID
                MachineConnector.SetRegisteredOI("uzugu_wallpapermod", Options);
                Log?.LogInfo("Wallpaper Mod: Remix options registered with ID 'uzugu_wallpapermod'");
            }
            catch (Exception ex)
            {
                Log?.LogError($"Failed to initialize and register Remix options: {ex}");
            }

            Log?.LogInfo("Wallpaper Mod initialized with game");
        }

        private void ProcessManager_RequestMainProcessSwitch(On.ProcessManager.orig_RequestMainProcessSwitch_ProcessID orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            orig(self, ID);

            if (pendingWallpaperLaunch && ID == ProcessManager.ProcessID.Game)
            {
                Log?.LogInfo("Wallpaper mode launch acknowledged by ProcessManager");
            }
        }

        private void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            orig(self, manager);

            if (pendingWallpaperLaunch)
            {
                Log?.LogInfo("RainWorldGame created for wallpaper mode");
                activeController = new WallpaperController(self, requestedStartRegion);
                pendingWallpaperLaunch = false;
            }
        }

        private void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
        {
            orig(self);

            if (activeController != null && activeController.Game == self)
            {
                activeController.Update(Time.deltaTime);
            }
        }

        private void RoomCamera_Update(On.RoomCamera.orig_Update orig, RoomCamera self)
        {
            orig(self);

            if (activeController != null && activeController.Game != null)
            {
                activeController.OnCameraUpdate(self);
            }
        }

        private void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            if (activeController != null && activeController.Game == self?.game)
            {
                activeController.BeforeRoomLoaded(self);
            }

            orig(self);

            if (activeController != null && activeController.Game == self?.game)
            {
                activeController.AfterRoomLoaded(self);
            }
        }

        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (activeController != null)
            {
                // Allow SlugNPCs / Pups to update so they can move and exist
                if (self.isNPC || self.playerState.isPup || self.Template.type.ToString() == "SlugNPC")
                {
                     orig(self, eu);
                     return;
                }

                // Block player-controlled slugcat updates entirely during wallpaper mode
                return;
            }

            orig(self, eu);
        }

        private void Overseer_Update(On.Overseer.orig_Update orig, Overseer self, bool eu)
        {
            if (activeController != null)
            {
                try
                {
                    self.RemoveFromRoom();
                    self.Destroy();
                }
                catch (Exception ex)
                {
                    Log?.LogWarning($"Failed to remove overseer during wallpaper mode: {ex.Message}");
                }

                return;
            }

            orig(self, eu);
        }

        private void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            try
            {
                activeController?.Shutdown();
            }
            finally
            {
                activeController = null;
            }

            orig(self);
        }

        /// <summary>
        /// Advances to the next campaign in the list and triggers world reload
        /// Called when all regions in current campaign have been explored
        /// </summary>
        public void AdvanceToNextCampaign()
        {
            if (Options == null)
            {
                Log?.LogWarning("AdvanceToNextCampaign: Options is null, cannot cycle campaigns");
                return;
            }

            // Get all campaign choices (from enum)
            var allCampaigns = new List<string>
            {
                "White", "Yellow", "Red",           // Vanilla
                "Gourmand", "Artificer", "Rivulet", // Downpour
                "Spearmaster", "Saint",              // Downpour
                "Watcher"                            // The Watcher
            };

            string currentCampaign = Options.SelectedCampaign.Value;
            int currentIndex = allCampaigns.IndexOf(currentCampaign);

            if (currentIndex < 0)
            {
                currentIndex = 0; // Default to first if not found
            }

            // Advance to next campaign (wraps around)
            int nextIndex = (currentIndex + 1) % allCampaigns.Count;
            string nextCampaign = allCampaigns[nextIndex];

            Log?.LogInfo($"AdvanceToNextCampaign: {currentCampaign} -> {nextCampaign}");

            // Update config
            Options.SelectedCampaign.Value = nextCampaign;

            string startRegion = WallpaperRegionCatalog.GetDefaultStartRegionForCampaign(nextCampaign);

            Log?.LogInfo($"Starting new campaign '{nextCampaign}' in region '{startRegion}'");

            // Trigger world reload with new campaign and region
            if (activeController?.Game?.manager != null)
            {
                activeController?.RegionManager?.OnCampaignChange(startRegion);
                QueueRegionReload(activeController.Game.manager, startRegion);
            }
        }

        public void OnDisable()
        {
            On.RainWorld.OnModsInit -= RainWorld_OnModsInit;
            On.ProcessManager.RequestMainProcessSwitch_ProcessID -= ProcessManager_RequestMainProcessSwitch;
            On.RainWorldGame.ctor -= RainWorldGame_ctor;
            On.RainWorldGame.Update -= RainWorldGame_Update;
            On.RainWorldGame.ShutDownProcess -= RainWorldGame_ShutDownProcess;
            On.RoomCamera.Update -= RoomCamera_Update;
            On.Room.Loaded -= Room_Loaded;
            On.Player.Update -= Player_Update;
            On.Overseer.Update -= Overseer_Update;

            MenuIntegration.Cleanup();
            Log?.LogInfo("Rain World Wallpaper Mod unloaded");
            Log = null;
        }

        internal void QueueRegionReload(ProcessManager manager, string regionCode)
        {
            if (manager == null)
            {
                return;
            }

            requestedStartRegion = regionCode;
            ConfigureMenuSetupForRegion(manager, regionCode);

            bool alreadyPending = pendingWallpaperLaunch;
            pendingWallpaperLaunch = true;
            activeController?.PrepareForWorldReload();

            Log?.LogInfo($"Queueing wallpaper region reload to {regionCode}");

            if (!alreadyPending)
            {
                manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
            }
        }

        private void ConfigureMenuSetupForRegion(ProcessManager manager, string regionCode)
        {
            if (manager?.menuSetup == null)
            {
                return;
            }

            var menuSetup = manager.menuSetup;
            var rainWorld = manager.rainWorld;

            TrySetField(menuSetup, "startGameCondition", ProcessManager.MenuSetup.StoryGameInitCondition.RegionSelect);
            TrySetField(menuSetup, "loadGame", false);
            TrySetField(menuSetup, "fastTravel", false);
            TrySetField(menuSetup, "regionSelectRoom", ResolveStartRoom(regionCode));

            // Use the selected campaign from config
            var slugcatName = ResolveSlugcatName();
            TrySetField(menuSetup, "playerCharacter", slugcatName);

            rainWorld?.progression?.ClearOutSaveStateFromMemory();
            if (rainWorld?.progression?.miscProgressionData != null)
            {
                TrySetField(rainWorld.progression.miscProgressionData, "currentlySelectedSinglePlayerSlugcat", slugcatName);
            }

        }

        private string ResolveStartRoom(string regionCode)
        {
            if (string.IsNullOrEmpty(regionCode))
            {
                Log?.LogInfo("ResolveStartRoom: Empty region code, defaulting to SU_A01");
                return "SU_A01";
            }

            string room = WallpaperRegionCatalog.GetStartRoom(regionCode);
            Log?.LogInfo($"ResolveStartRoom: Region '{regionCode}' -> Room '{room}'");
            return room;
        }

        private static void TrySetField(object target, string fieldName, object value)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            var type = target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                return;
            }

            if (value == null && field.FieldType.IsValueType)
            {
                return;
            }

            if (value != null && !field.FieldType.IsAssignableFrom(value.GetType()))
            {
                return;
            }

            field.SetValue(target, value);
        }

        private string ResolveInitialRegion()
        {
            string campaign = ResolveCampaignString();
            if (!string.IsNullOrEmpty(commandLineStartRegion))
            {
                string normalizedRegion = WallpaperRegionCatalog.NormalizeRegionForCampaign(commandLineStartRegion, campaign);
                Log?.LogInfo($"ResolveInitialRegion: Command-line region override '{commandLineStartRegion}' resolved to '{normalizedRegion}' for campaign '{campaign}'");
                return normalizedRegion;
            }

            // Try to use the config value first
            if (Options != null)
            {
                string regionCode = WallpaperModOptions.GetRegionCode(Options.StartRegion.Value);
                string normalizedRegion = WallpaperRegionCatalog.NormalizeRegionForCampaign(regionCode, campaign);
                if (!string.Equals(regionCode, normalizedRegion, StringComparison.OrdinalIgnoreCase))
                {
                    Options.StartRegion.Value = normalizedRegion;
                }

                Log?.LogInfo($"ResolveInitialRegion: Config value = '{Options.StartRegion.Value}', resolved to '{normalizedRegion}' for campaign '{campaign}'");
                return normalizedRegion;
            }
            Log?.LogInfo($"ResolveInitialRegion: No options, defaulting to '{requestedStartRegion ?? "SU"}'");
            return WallpaperRegionCatalog.NormalizeRegionForCampaign(requestedStartRegion ?? "SU", campaign);
        }

        private string ResolveCampaignString()
        {
            if (!string.IsNullOrEmpty(commandLineCampaign))
            {
                return commandLineCampaign;
            }

            return Options?.SelectedCampaign.Value ?? WallpaperModOptions.CampaignChoice.White.ToString();
        }

        private SlugcatStats.Name ResolveSlugcatName()
        {
            if (!string.IsNullOrEmpty(commandLineCampaign))
            {
                return WallpaperModOptions.GetSlugcatName(commandLineCampaign);
            }

            // Use the selected campaign from config, fallback to White/Survivor
            if (Options != null)
            {
                return WallpaperModOptions.GetSlugcatName(Options.SelectedCampaign.Value);
            }
            return SlugcatStats.Name.White;
        }

        private void ParseCommandLineArguments()
        {
            string[] args;

            try
            {
                args = Environment.GetCommandLineArgs();
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"Failed to read command-line arguments: {ex.Message}");
                return;
            }

            for (int i = 1; i < args.Length; i++)
            {
                string current = args[i];
                if (string.IsNullOrWhiteSpace(current))
                {
                    continue;
                }

                string flag = current.Trim();
                string inlineValue = null;
                int separatorIndex = flag.IndexOf('=');

                if (separatorIndex > 0)
                {
                    inlineValue = flag.Substring(separatorIndex + 1);
                    flag = flag.Substring(0, separatorIndex);
                }

                switch (flag.ToLowerInvariant())
                {
                    case "-wallpaper":
                    case "--wallpaper":
                    case "-wallpapermode":
                    case "--wallpapermode":
                    case "--wallpaper-mode":
                    case "-wallpaperengine":
                    case "--wallpaperengine":
                    case "--wallpaper-engine":
                        autoLaunchWallpaperRequested = true;
                        break;

                    case "--wallpaper-region":
                    case "--wallpaper-start-region":
                        commandLineStartRegion = ParseRegionOverride(inlineValue ?? TryReadArgumentValue(args, ref i));
                        autoLaunchWallpaperRequested = true;
                        break;

                    case "--wallpaper-campaign":
                    case "--wallpaper-slugcat":
                        commandLineCampaign = ParseCampaignOverride(inlineValue ?? TryReadArgumentValue(args, ref i));
                        autoLaunchWallpaperRequested = true;
                        break;
                }
            }

            if (!autoLaunchWallpaperRequested)
            {
                return;
            }

            Log?.LogInfo(
                $"Wallpaper auto-launch enabled. Region override: {commandLineStartRegion ?? "<config>"}, " +
                $"campaign override: {commandLineCampaign ?? "<config>"}");
        }

        private string ParseRegionOverride(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Log?.LogWarning("Ignoring empty wallpaper region override");
                return null;
            }

            string normalized = value.Trim().Trim('"');

            if (Enum.TryParse(normalized, true, out WallpaperModOptions.RegionChoice regionChoice))
            {
                return regionChoice.ToString();
            }

            normalized = normalized.ToUpperInvariant();
            if (WallpaperRegionCatalog.IsKnownRegion(normalized))
            {
                return normalized;
            }

            Log?.LogWarning($"Ignoring unknown wallpaper region override '{value}'");
            return null;
        }

        private string ParseCampaignOverride(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Log?.LogWarning("Ignoring empty wallpaper campaign override");
                return null;
            }

            string normalized = value.Trim().Trim('"');

            if (Enum.TryParse(normalized, true, out WallpaperModOptions.CampaignChoice campaignChoice))
            {
                return campaignChoice.ToString();
            }

            Log?.LogWarning($"Ignoring unknown wallpaper campaign override '{value}'");
            return null;
        }

        private static string TryReadArgumentValue(string[] args, ref int index)
        {
            int nextIndex = index + 1;
            if (nextIndex >= args.Length)
            {
                return null;
            }

            string nextValue = args[nextIndex];
            if (string.IsNullOrWhiteSpace(nextValue) || nextValue.StartsWith("-", StringComparison.Ordinal))
            {
                return null;
            }

            index = nextIndex;
            return nextValue;
        }
    }
}
