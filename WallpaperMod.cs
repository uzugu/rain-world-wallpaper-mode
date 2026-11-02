using BepInEx;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    [BepInPlugin("com.uzugu.wallpapermod", "Rain World Wallpaper Mode", "1.1.6")]
    public class WallpaperMod : BaseUnityPlugin
    {
        public static WallpaperMod Instance;
        public static WallpaperModOptions Options;

        private bool pendingWallpaperLaunch;
        private bool autoLaunchWallpaperRequested;
        private bool autoLaunchWallpaperConsumed;
        private WallpaperController activeController;
        private bool constructingWallpaperGame;
        private SlugcatStats.Name slugcatBeforeWallpaper;
        private bool hasSlugcatBeforeWallpaper;
        private string requestedStartRegion = "SU";
        private string requestedStartRoom = "SU_A01";
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
            On.PlayerProgression.SaveDeathPersistentDataOfCurrentState += PlayerProgression_SaveDeathPersistentDataOfCurrentState;
            On.PlayerProgression.SaveProgression += PlayerProgression_SaveProgression;

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

                // Remix and some framework mods may discover LoadOI before OnModsInit.
                // Avoid replacing an already-registered identical interface instance.
                if (MachineConnector.GetRegisteredOI("uzugu_wallpapermod") != Options)
                {
                    MachineConnector.SetRegisteredOI("uzugu_wallpapermod", Options);
                }
                Log?.LogInfo("Wallpaper Mod: Remix options registered with ID 'uzugu_wallpapermod'");
            }
            catch (Exception ex)
            {
                Log?.LogError($"Failed to initialize and register Remix options: {ex}");
            }

            Log?.LogInfo($"Wallpaper Mod initialized with game. Active mods: {GetActiveModSummary()}");
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
            // The story session pre-saves inside this constructor, before activeController exists.
            constructingWallpaperGame = pendingWallpaperLaunch;
            try
            {
                orig(self, manager);
            }
            catch (Exception ex)
            {
                Log?.LogError(
                    $"RainWorldGame construction failed while wallpaper launch was {pendingWallpaperLaunch}. " +
                    $"Campaign='{ResolveCampaignString()}', region='{requestedStartRegion}', " +
                    $"startRoom='{requestedStartRoom}', activeMods=[{GetActiveModSummary()}].\n{ex}");
                // A failed launch must not turn the next normal game into a wallpaper session.
                pendingWallpaperLaunch = false;
                throw;
            }
            finally
            {
                constructingWallpaperGame = false;
            }

            if (pendingWallpaperLaunch)
            {
                Log?.LogInfo("RainWorldGame created for wallpaper mode");
                activeController = new WallpaperController(self, requestedStartRegion);
                pendingWallpaperLaunch = false;
            }
        }

        private void RainWorldGame_Update(On.RainWorldGame.orig_Update orig, RainWorldGame self)
        {
            bool wasPaused = self.pauseMenu != null;
            orig(self);

            if (activeController != null && activeController.Game == self)
            {
                // The pause button (Start on controllers, Escape on keyboard) also opens the wallpaper overlay or exits,
                // so close the vanilla pause menu it just opened. A pause opened by the Steam overlay is left alone.
                if (!wasPaused && self.pauseMenu != null && IsPauseButtonDown())
                {
                    self.ContinuePaused();
                }

                // Update runs once per game tick, not once per rendered frame, so each call is 1/framesPerSecond real seconds.
                activeController.Update(1f / Math.Max(1, self.framesPerSecond));
            }
        }

        // RWInput is internal, so bind its pause check once; this keeps the fix on the player's own pause binding.
        private static readonly Func<int, bool, bool> CheckPauseButton = CreatePauseButtonCheck();

        private static Func<int, bool, bool> CreatePauseButtonCheck()
        {
            try
            {
                MethodInfo method = typeof(RainWorldGame).Assembly.GetType("RWInput")?.GetMethod(
                    "CheckPauseButton",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(int), typeof(bool) },
                    null);
                return method == null
                    ? null
                    : (Func<int, bool, bool>)Delegate.CreateDelegate(typeof(Func<int, bool, bool>), method);
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"Could not bind RWInput.CheckPauseButton, falling back to Escape/Start: {ex.Message}");
                return null;
            }
        }

        private static bool IsPauseButtonDown()
        {
            if (CheckPauseButton != null)
            {
                return CheckPauseButton(0, false);
            }

            return Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.JoystickButton7);
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
                if (self.isNPC || self.Template.type.ToString() == "SlugNPC")
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

        private bool IsWallpaperSessionActive => constructingWallpaperGame || activeController != null;

        // Wallpaper sessions start with RegionSelect, so the game builds a blank save state and pre-saves it
        // "as a death" over the selected campaign's real save. Nothing in a wallpaper session should be saved.
        private void PlayerProgression_SaveDeathPersistentDataOfCurrentState(On.PlayerProgression.orig_SaveDeathPersistentDataOfCurrentState orig, PlayerProgression self, bool saveAsIfPlayerDied, bool saveAsIfPlayerQuit)
        {
            if (IsWallpaperSessionActive)
            {
                Log?.LogInfo("Skipped death-persistent save during wallpaper mode to protect the campaign save");
                return;
            }

            orig(self, saveAsIfPlayerDied, saveAsIfPlayerQuit);
        }

        private bool PlayerProgression_SaveProgression(On.PlayerProgression.orig_SaveProgression orig, PlayerProgression self, bool saveMaps, bool saveMiscProg)
        {
            if (IsWallpaperSessionActive)
            {
                Log?.LogInfo("Skipped progression save during wallpaper mode");
                return false;
            }

            return orig(self, saveMaps, saveMiscProg);
        }

        private void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            try
            {
                activeController?.Shutdown();
            }
            catch (Exception ex)
            {
                // Always let the game shut its own process down, even if wallpaper cleanup fails.
                Log?.LogError($"Wallpaper controller shutdown failed: {ex}");
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
            On.PlayerProgression.SaveDeathPersistentDataOfCurrentState -= PlayerProgression_SaveDeathPersistentDataOfCurrentState;
            On.PlayerProgression.SaveProgression -= PlayerProgression_SaveProgression;

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

            var rainWorld = manager.rainWorld;

            manager.menuSetup.startGameCondition = ProcessManager.MenuSetup.StoryGameInitCondition.RegionSelect;
            requestedStartRoom = ResolveStartRoom(regionCode);
            manager.menuSetup.regionSelectRoom = requestedStartRoom;

            // The story session plays as progression.PlayingAsSlugcat, which falls back to the selected slugcat once the
            // in-memory save state is cleared. Remember the player's own choice so the main menu can put it back.
            var slugcatName = ResolveSlugcatName();
            rainWorld?.progression?.ClearOutSaveStateFromMemory();
            var miscProgression = rainWorld?.progression?.miscProgressionData;
            if (miscProgression != null)
            {
                if (!hasSlugcatBeforeWallpaper)
                {
                    slugcatBeforeWallpaper = miscProgression.currentlySelectedSinglePlayerSlugcat;
                    hasSlugcatBeforeWallpaper = true;
                }

                miscProgression.currentlySelectedSinglePlayerSlugcat = slugcatName;
            }
        }

        private string ResolveStartRoom(string regionCode)
        {
            if (string.IsNullOrEmpty(regionCode))
            {
                Log?.LogInfo("ResolveStartRoom: Empty region code, defaulting to SU_A01");
                return "SU_A01";
            }

            string room = WallpaperRegionCatalog.GetStartRoom(regionCode, ResolveCampaignString());
            Log?.LogInfo($"ResolveStartRoom: Region '{regionCode}' -> Room '{room}'");
            return room;
        }

        // Called before the main menu is built, so the player's slugcat choice is back before anything saves it.
        internal void RestoreSelectedSlugcat(ProcessManager manager)
        {
            if (!hasSlugcatBeforeWallpaper)
            {
                return;
            }

            hasSlugcatBeforeWallpaper = false;
            var miscProgression = manager?.rainWorld?.progression?.miscProgressionData;
            if (miscProgression != null)
            {
                miscProgression.currentlySelectedSinglePlayerSlugcat = slugcatBeforeWallpaper;
            }
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

        private static string GetActiveModSummary()
        {
            try
            {
                if (ModManager.ActiveMods == null || ModManager.ActiveMods.Count == 0)
                {
                    return "<none>";
                }

                return string.Join(", ", ModManager.ActiveMods.Select(mod =>
                    $"{mod.id}@{(string.IsNullOrWhiteSpace(mod.version) ? "unknown" : mod.version)}"));
            }
            catch (Exception ex)
            {
                return "<unavailable: " + ex.Message + ">";
            }
        }
    }
}
