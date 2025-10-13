using BepInEx;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    [BepInPlugin("com.vrmakes.wallpapermod", "Rain World Wallpaper Mode", "1.0.2")]
    public class WallpaperMod : BaseUnityPlugin
    {
        public static WallpaperMod Instance;
        public static WallpaperModOptions Options;

        private bool pendingWallpaperLaunch;
        private WallpaperController activeController;
        private string requestedStartRegion = "SU";

        private static readonly Dictionary<string, string> RegionStartRooms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SU", "SU_A01" },
            { "HI", "HI_A01" },
            { "CC", "CC_A01" },
            { "GW", "GW_A01" },
            { "SH", "SH_A01" },
            { "DS", "DS_A01" },
            { "SL", "SL_A01" },
            { "SI", "SI_A01" },
            { "LF", "LF_A01" },
            { "UW", "UW_A01" },
            { "SS", "SU_A01" },
            { "SB", "SB_A01" },
            { "LM", "LM_A01" },
            { "RM", "RM_A01" },
            { "DM", "DM_A01" },
            { "LC", "LC_A01" },
            { "MS", "MS_A01" },
            { "VS", "VS_A01" },
            { "CL", "CL_A01" },
            { "OE", "OE_A01" }
        };

        public static ManualLogSource Log { get; private set; }

        public void OnEnable()
        {
            Instance = this;
            Log = Logger;
            Log?.LogInfo("Rain World Wallpaper Mod V2.0 loaded!");

            // Don't initialize options here - wait for OnModsInit
            // Options will be created by Remix when needed

            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
            On.ProcessManager.RequestMainProcessSwitch_ProcessID += ProcessManager_RequestMainProcessSwitch;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.RainWorldGame.Update += RainWorldGame_Update;
            On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
            On.RoomCamera.Update += RoomCamera_Update;
            On.Player.Update += Player_Update;

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
                MachineConnector.SetRegisteredOI("vrmakes_wallpapermod", Options);
                Log?.LogInfo("Wallpaper Mod: Remix options registered with ID 'vrmakes_wallpapermod'");
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

        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (activeController != null)
            {
                // Block slugcat updates entirely during wallpaper mode
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

        public void OnDisable()
        {
            On.RainWorld.OnModsInit -= RainWorld_OnModsInit;
            On.ProcessManager.RequestMainProcessSwitch_ProcessID -= ProcessManager_RequestMainProcessSwitch;
            On.RainWorldGame.ctor -= RainWorldGame_ctor;
            On.RainWorldGame.Update -= RainWorldGame_Update;
            On.RainWorldGame.ShutDownProcess -= RainWorldGame_ShutDownProcess;
            On.RoomCamera.Update -= RoomCamera_Update;
            On.Player.Update -= Player_Update;

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
            TrySetField(menuSetup, "playerCharacter", SlugcatStats.Name.White);

            rainWorld?.progression?.ClearOutSaveStateFromMemory();
            if (rainWorld?.progression?.miscProgressionData != null)
            {
                TrySetField(rainWorld.progression.miscProgressionData, "currentlySelectedSinglePlayerSlugcat", SlugcatStats.Name.White);
            }

        }

        private string ResolveStartRoom(string regionCode)
        {
            if (string.IsNullOrEmpty(regionCode))
            {
                return "SU_A01";
            }

            if (RegionStartRooms.TryGetValue(regionCode, out var room))
            {
                return room;
            }

            return regionCode + "_A01";
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
            // Try to use the config value first
            if (Options != null && !string.IsNullOrEmpty(Options.StartRegion.Value))
            {
                return Options.StartRegion.Value.ToUpperInvariant();
            }
            return requestedStartRegion ?? "SU";
        }
    }
}
