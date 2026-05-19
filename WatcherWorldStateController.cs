using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RWCustom;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Drives Watcher-specific room states without hard-linking the plugin to Watcher-only types.
    /// This keeps the Workshop DLL loadable on base/Downpour installs.
    /// </summary>
    internal sealed class WatcherWorldStateController
    {
        private const float RottedIntensity = 1f;
        private const float KarmaPatchRadius = 520f;
        private const float NaturalSeedInterval = 45f;

        private static readonly BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private readonly HashSet<string> naturalInjectedRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string naturalWorldName;
        private int naturalSeedGeneration;
        private float naturalSeedTimer;
        private bool warnedUnavailable;

        public void ApplyBeforeRoomLoaded(RainWorldGame game, Room room, WallpaperModOptions.RotState state)
        {
            if (!CanUseWatcherState(game, room, state))
            {
                return;
            }

            TryWatcherAction(() =>
            {
                switch (state)
                {
                    case WallpaperModOptions.RotState.Natural:
                        ClearInjectedRoomState(room);
                        EnsureNaturalSeed(room, rebuildSeed: false);
                        break;
                    case WallpaperModOptions.RotState.Normal:
                        ClearInjectedRoomState(room);
                        ClearNaturalSeed(room);
                        ClearRegionRotState(room);
                        break;
                    case WallpaperModOptions.RotState.Rotted:
                        ClearNaturalSeed(room);
                        AddSentientRotInfection(room, RottedIntensity);
                        AddRegionRotState(room, RottedIntensity);
                        break;
                    case WallpaperModOptions.RotState.Karma:
                        ClearInjectedRotEffects(room);
                        ClearNaturalSeed(room);
                        ClearRegionRotState(room);
                        AddFlowerWind(room);
                        AddKarmaFlowerPatches(room);
                        break;
                }
            });
        }

        public void ApplyAfterRoomLoaded(RainWorldGame game, Room room, WallpaperModOptions.RotState state)
        {
            if (!CanUseWatcherState(game, room, state))
            {
                return;
            }

            TryWatcherAction(() =>
            {
                if (state == WallpaperModOptions.RotState.Rotted)
                {
                    InitializeRotPresence(room, RottedIntensity);
                }
                else if (state == WallpaperModOptions.RotState.Natural)
                {
                    InitializeNaturalPresence(room);
                }
                else if (state == WallpaperModOptions.RotState.Karma)
                {
                    EnsureKarmaFlowerRenderer(room);
                }
            });
        }

        public void Update(RainWorldGame game, WallpaperModOptions.RotState state, float dt)
        {
            if (state != WallpaperModOptions.RotState.Natural)
            {
                naturalSeedTimer = 0f;
                return;
            }

            Room room = game?.cameras != null && game.cameras.Length > 0 ? game.cameras[0]?.room : null;
            if (!CanUseWatcherState(game, room, state))
            {
                return;
            }

            TryWatcherAction(() =>
            {
                EnsureNaturalSeed(room, rebuildSeed: false);
                naturalSeedTimer += Mathf.Max(0f, dt);
                if (naturalSeedTimer < NaturalSeedInterval)
                {
                    return;
                }

                naturalSeedTimer = 0f;
                naturalSeedGeneration++;
                EnsureNaturalSeed(room, rebuildSeed: true);
                InitializeNaturalPresence(room);
                WallpaperMod.Log?.LogDebug($"Watcher natural rot seed refreshed for {room.world.name} (generation {naturalSeedGeneration})");
            });
        }

        public void ClearSessionState(RainWorldGame game)
        {
            Room room = game?.cameras != null && game.cameras.Length > 0 ? game.cameras[0]?.room : null;
            TryWatcherAction(() => ClearNaturalSeed(room));
        }

        public void ApplyToCurrentRoom(RainWorldGame game, WallpaperModOptions.RotState state)
        {
            Room room = game?.cameras != null && game.cameras.Length > 0 ? game.cameras[0]?.room : null;
            ApplyBeforeRoomLoaded(game, room, state);
            ApplyAfterRoomLoaded(game, room, state);

            if (!CanUseWatcherState(game, room, state))
            {
                return;
            }

            TryWatcherAction(() =>
            {
                if (state == WallpaperModOptions.RotState.Rotted || state == WallpaperModOptions.RotState.Natural)
                {
                    InvokeInstance(room, "UpdateSentientRotEffect");
                }
                else if (state == WallpaperModOptions.RotState.Normal || state == WallpaperModOptions.RotState.Karma)
                {
                    UpdateRotMode(game, room, 0f);
                }
            });
        }

        private bool CanUseWatcherState(RainWorldGame game, Room room, WallpaperModOptions.RotState state)
        {
            bool available = ModCompatibility.IsWatcherEnabled &&
                WatcherReflection.RoomEffectType != null &&
                game != null &&
                room != null &&
                room.game == game &&
                room.roomSettings != null;

            if (!available && state != WallpaperModOptions.RotState.Natural && !warnedUnavailable)
            {
                warnedUnavailable = true;
                WallpaperMod.Log?.LogWarning("Watcher world-state effects are unavailable on this install; ignoring Watcher-specific room effects.");
            }

            return available;
        }

        private static void AddRegionRotState(Room room, float intensity)
        {
            object regionState = room.world?.regionState;
            if (regionState == null || room.abstractRoom == null)
            {
                return;
            }

            InvokeInstance(regionState, "InfectRegionRoomWithSentientRot", intensity, room.abstractRoom.name);
        }

        private void EnsureNaturalSeed(Room room, bool rebuildSeed)
        {
            object regionState = room.world?.regionState;
            IDictionary progression = GetSentientRotProgression(regionState);
            if (progression == null || room.world?.abstractRooms == null)
            {
                return;
            }

            string worldName = room.world.name ?? string.Empty;
            if (!string.Equals(naturalWorldName, worldName, StringComparison.OrdinalIgnoreCase))
            {
                naturalWorldName = worldName;
                naturalInjectedRooms.Clear();
                naturalSeedTimer = 0f;
                naturalSeedGeneration = StableHash(worldName) & 1023;
            }

            if (rebuildSeed)
            {
                ClearNaturalSeed(room);
            }
            else if (naturalInjectedRooms.Count > 0)
            {
                return;
            }

            if (HasSentientRotResistance(worldName))
            {
                return;
            }

            var candidates = room.world.abstractRooms
                .Where(IsNaturalSeedCandidate)
                .OrderBy(abstractRoom => StableHash($"{worldName}|{abstractRoom.name}|order|{naturalSeedGeneration}"))
                .ToList();

            if (candidates.Count == 0)
            {
                return;
            }

            float chance = GetNaturalSeedChance(worldName);
            int seeded = 0;
            int minimumSeeded = Mathf.Clamp(Mathf.CeilToInt(candidates.Count * 0.08f), 1, 8);
            foreach (AbstractRoom abstractRoom in candidates)
            {
                if (progression.Contains(abstractRoom.name))
                {
                    continue;
                }

                bool shouldSeed = seeded < minimumSeeded ||
                    StableUnit($"{worldName}|{abstractRoom.name}|pick|{naturalSeedGeneration}") < chance;
                if (!shouldSeed)
                {
                    continue;
                }

                object state = CreateSentientRotState(GetNaturalIntensity(worldName, abstractRoom.name, naturalSeedGeneration));
                if (state == null)
                {
                    return;
                }

                progression[abstractRoom.name] = state;
                naturalInjectedRooms.Add(abstractRoom.name);
                seeded++;
            }
        }

        private void ClearNaturalSeed(Room room)
        {
            IDictionary progression = GetSentientRotProgression(room?.world?.regionState);
            if (progression == null ||
                naturalInjectedRooms.Count == 0 ||
                !string.Equals(room?.world?.name, naturalWorldName, StringComparison.OrdinalIgnoreCase))
            {
                naturalInjectedRooms.Clear();
                return;
            }

            foreach (string roomName in naturalInjectedRooms.ToList())
            {
                progression.Remove(roomName);
            }

            naturalInjectedRooms.Clear();
        }

        private static void ClearRegionRotState(Room room)
        {
            IDictionary progression = GetSentientRotProgression(room.world?.regionState);
            if (progression == null || room.abstractRoom == null)
            {
                return;
            }

            progression.Remove(room.abstractRoom.name);
            if (progression.Count > 0 || room.game?.IsStorySession != true)
            {
                return;
            }

            string regionName = room.world.name.ToLowerInvariant();
            var misc = room.game.GetStorySession.saveState.miscWorldSaveData;
            RemoveStringFromMemberList(misc, "regionsInfectedBySentientRot", regionName);
            RemoveStringFromMemberList(misc, "regionsInfectedBySentientRotSpread", regionName);
        }

        private static void AddSentientRotInfection(Room room, float amount)
        {
            RoomSettings.RoomEffect.Type effectType = WatcherReflection.GetRoomEffectType("SentientRotInfection");
            if (effectType == null)
            {
                return;
            }

            RoomSettings.RoomEffect effect = room.roomSettings.GetEffect(effectType);
            if (effect == null)
            {
                room.roomSettings.effects.Add(new RoomSettings.RoomEffect(effectType, amount, inherited: false)
                {
                    save = false
                });
                return;
            }

            effect.amount = Mathf.Max(effect.amount, amount);
        }

        private static void InitializeRotPresence(Room room, float amount)
        {
            if (room.aimap == null)
            {
                return;
            }

            if (!GetMemberValue(room, "rotPresenceInitialized", false))
            {
                InvokeInstance(room, "InitializeSentientRotPresenceInRoom", amount);
            }

            InvokeInstance(room, "UpdateSentientRotEffect");
        }

        private static void InitializeNaturalPresence(Room room)
        {
            float amount = GetNaturalRotAmount(room);
            if (amount > 0f)
            {
                InitializeRotPresence(room, amount);
                return;
            }

            InvokeInstance(room, "UpdateSentientRotEffect");
            UpdateRotMode(room.game, room, 0f);
        }

        private static float GetNaturalRotAmount(Room room)
        {
            if (room == null)
            {
                return 0f;
            }

            float baseAmount = 0f;
            RoomSettings.RoomEffect.Type infectionType = WatcherReflection.GetRoomEffectType("SentientRotInfection");
            RoomSettings.RoomEffect authoredEffect = infectionType == null ? null : room.roomSettings?.GetEffect(infectionType);
            if (authoredEffect != null)
            {
                baseAmount = Mathf.Clamp01(authoredEffect.amount);
            }

            float progressionAmount = 0f;
            IDictionary progression = GetSentientRotProgression(room.world?.regionState);
            if (progression != null && room.abstractRoom != null && progression.Contains(room.abstractRoom.name))
            {
                progressionAmount = Mathf.Clamp01(GetMemberValue(progression[room.abstractRoom.name], "rotIntensity", 0f));
            }

            return Mathf.Clamp01(baseAmount + (1f - baseAmount) * progressionAmount);
        }

        private static void ClearInjectedRoomState(Room room)
        {
            ClearInjectedRotEffects(room);
            ClearInjectedKarmaState(room);
        }

        private static void ClearInjectedRotEffects(Room room)
        {
            RoomSettings.RoomEffect.Type infectionType = WatcherReflection.GetRoomEffectType("SentientRotInfection");
            RoomSettings.RoomEffect.Type particlesType = WatcherReflection.GetRoomEffectType("SentientRotParticles");

            room.roomSettings.effects.RemoveAll(effect =>
                !effect.save &&
                (effect.type == infectionType || effect.type == particlesType));
        }

        private static void ClearInjectedKarmaState(Room room)
        {
            RoomSettings.RoomEffect.Type flowerWindType = WatcherReflection.GetRoomEffectType("FlowerWind");
            PlacedObject.Type karmaPatchType = WatcherReflection.GetPlacedObjectType("KarmaFlowerPatch");

            room.roomSettings.effects.RemoveAll(effect => !effect.save && effect.type == flowerWindType);
            room.roomSettings.placedObjects.RemoveAll(obj => !obj.save && obj.type == karmaPatchType);

            if (room.roomSettings.placedObjects.Any(obj => obj.active && obj.type == karmaPatchType))
            {
                return;
            }

            foreach (UpdatableAndDeletable patchRenderer in room.updateList?.Where(IsKarmaFlowerPatch).ToList() ?? Enumerable.Empty<UpdatableAndDeletable>())
            {
                patchRenderer.Destroy();
                room.RemoveObject(patchRenderer);
            }
        }

        private static bool IsNaturalSeedCandidate(AbstractRoom abstractRoom)
        {
            return abstractRoom != null &&
                !abstractRoom.gate &&
                !abstractRoom.shelter &&
                !string.IsNullOrWhiteSpace(abstractRoom.name);
        }

        private static float GetNaturalSeedChance(string worldName)
        {
            switch ((worldName ?? string.Empty).ToUpperInvariant())
            {
                case "SU":
                case "HI":
                case "CC":
                case "LF":
                case "SH":
                    return 0.14f;
                case "WMPA":
                case "WAUA":
                    return 0.10f;
                case "WRFA":
                case "WRFB":
                case "WRRA":
                case "WPTA":
                case "WBLA":
                    return 0.28f;
                default:
                    return 0.22f;
            }
        }

        private static float GetNaturalIntensity(string worldName, string roomName, int generation)
        {
            float roll = StableUnit($"{worldName}|{roomName}|intensity|{generation}");
            float amount;
            if (roll < 0.56f)
            {
                amount = 0.25f;
            }
            else if (roll < 0.84f)
            {
                amount = 0.5f;
            }
            else if (roll < 0.97f)
            {
                amount = 0.75f;
            }
            else
            {
                amount = 1f;
            }

            if (IsWatcherModifiedBaseRegion(worldName))
            {
                amount = Mathf.Min(amount, 0.5f);
            }

            return amount;
        }

        private static bool IsWatcherModifiedBaseRegion(string worldName)
        {
            switch ((worldName ?? string.Empty).ToUpperInvariant())
            {
                case "SU":
                case "HI":
                case "CC":
                case "LF":
                case "SH":
                    return true;
                default:
                    return false;
            }
        }

        private static float StableUnit(string value)
        {
            uint hash = unchecked((uint)StableHash(value));
            return (hash & 0x00FFFFFF) / 16777216f;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < (value?.Length ?? 0); i++)
                {
                    hash = hash * 31 + value[i];
                }

                return hash;
            }
        }

        private static void AddFlowerWind(Room room)
        {
            RoomSettings.RoomEffect.Type effectType = WatcherReflection.GetRoomEffectType("FlowerWind");
            if (effectType == null)
            {
                return;
            }

            RoomSettings.RoomEffect effect = room.roomSettings.GetEffect(effectType);
            if (effect == null)
            {
                effect = new RoomSettings.RoomEffect(effectType, 0.35f, inherited: false)
                {
                    save = false
                };
                room.roomSettings.effects.Add(effect);
            }

            effect.amount = Mathf.Clamp(effect.amount, 0.2f, 0.45f);
            if (effect.extraAmounts == null || effect.extraAmounts.Length < 2)
            {
                effect.extraAmounts = new[] { 0.85f, 0.25f };
            }
            else
            {
                effect.extraAmounts[0] = Mathf.Max(effect.extraAmounts[0], 0.85f);
                effect.extraAmounts[1] = Mathf.Max(effect.extraAmounts[1], 0.25f);
            }
        }

        private static void AddKarmaFlowerPatches(Room room)
        {
            PlacedObject.Type patchType = WatcherReflection.GetPlacedObjectType("KarmaFlowerPatch");
            Type patchDataType = WatcherReflection.KarmaFlowerPatchDataType;
            if (patchType == null || patchDataType == null)
            {
                return;
            }

            if (room.roomSettings.placedObjects.Any(obj => !obj.save && obj.type == patchType))
            {
                return;
            }

            int patchCount = Mathf.Clamp(room.cameraPositions?.Length ?? 1, 1, 4);
            for (int i = 0; i < patchCount; i++)
            {
                Vector2 center = GetPatchCenter(room, i, patchCount);
                object data = Activator.CreateInstance(patchDataType, new object[] { null });
                SetMemberValue(data, "handlePos", Custom.DegToVec(35f + 73f * i) * KarmaPatchRadius);
                SetMemberValue(data, "tilt", 0.45f + 0.1f * (i % 2));
                SetMemberValue(data, "glowStrength", 0.35f);
                SetMemberValue(data, "glowRadius", 0.75f);

                var placedObject = new PlacedObject(patchType, (PlacedObject.Data)data)
                {
                    pos = center,
                    save = false
                };
                SetMemberValue(data, "owner", placedObject);
                room.roomSettings.placedObjects.Add(placedObject);
            }
        }

        private static Vector2 GetPatchCenter(Room room, int index, int patchCount)
        {
            if (room.cameraPositions != null && room.cameraPositions.Length > 0)
            {
                Vector2 cameraPosition = room.cameraPositions[Math.Min(index, room.cameraPositions.Length - 1)];
                return ClampToRoom(room, cameraPosition + new Vector2(683f, 384f));
            }

            float t = (index + 1f) / (patchCount + 1f);
            return ClampToRoom(room, new Vector2(room.PixelWidth * t, room.PixelHeight * 0.45f));
        }

        private static Vector2 ClampToRoom(Room room, Vector2 pos)
        {
            return new Vector2(
                Mathf.Clamp(pos.x, 80f, Mathf.Max(80f, room.PixelWidth - 80f)),
                Mathf.Clamp(pos.y, 80f, Mathf.Max(80f, room.PixelHeight - 80f)));
        }

        private static void EnsureKarmaFlowerRenderer(Room room)
        {
            if (WatcherReflection.KarmaFlowerPatchType == null)
            {
                return;
            }

            UpdatableAndDeletable patchRenderer = room.updateList?.FirstOrDefault(IsKarmaFlowerPatch);
            if (patchRenderer == null)
            {
                patchRenderer = Activator.CreateInstance(WatcherReflection.KarmaFlowerPatchType) as UpdatableAndDeletable;
                if (patchRenderer == null)
                {
                    return;
                }

                room.AddObject(patchRenderer);
            }

            if (room.shortCutsReady)
            {
                InvokeInstance(patchRenderer, "ShortcutsReady");
            }
        }

        private static bool IsKarmaFlowerPatch(UpdatableAndDeletable item)
        {
            return item != null &&
                WatcherReflection.KarmaFlowerPatchType != null &&
                WatcherReflection.KarmaFlowerPatchType.IsInstanceOfType(item);
        }

        private static bool HasSentientRotResistance(string worldName)
        {
            MethodInfo method = typeof(Region).GetMethod("HasSentientRotResistance", StaticFlags);
            return method != null && method.ReturnType == typeof(bool) && (bool)method.Invoke(null, new object[] { worldName });
        }

        private static IDictionary GetSentientRotProgression(object regionState)
        {
            return GetMemberValue<IDictionary>(regionState, "sentientRotProgression", null);
        }

        private static void RemoveStringFromMemberList(object target, string memberName, string value)
        {
            IList list = GetMemberValue<IList>(target, memberName, null);
            list?.Remove(value);
        }

        private static object CreateSentientRotState(float intensity)
        {
            Type stateType = typeof(RegionState).GetNestedType("SentientRotState", BindingFlags.Public | BindingFlags.NonPublic);
            if (stateType == null)
            {
                return null;
            }

            object state = Activator.CreateInstance(stateType);
            SetMemberValue(state, "rotIntensity", intensity);
            return state;
        }

        private static void UpdateRotMode(RainWorldGame game, Room room, float amount)
        {
            RoomCamera camera = game?.cameras != null && game.cameras.Length > 0 ? game.cameras[0] : null;
            InvokeInstance(camera, "UpdateRotMode", room, amount);
        }

        private static void InvokeInstance(object target, string methodName, params object[] args)
        {
            if (target == null)
            {
                return;
            }

            MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
            method?.Invoke(target, args);
        }

        private static T GetMemberValue<T>(object target, string memberName, T fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            Type type = target.GetType();
            FieldInfo field = type.GetField(memberName, InstanceFlags);
            if (field != null && field.GetValue(target) is T fieldValue)
            {
                return fieldValue;
            }

            PropertyInfo property = type.GetProperty(memberName, InstanceFlags);
            if (property != null && property.GetValue(target, null) is T propertyValue)
            {
                return propertyValue;
            }

            return fallback;
        }

        private static void SetMemberValue(object target, string memberName, object value)
        {
            if (target == null)
            {
                return;
            }

            Type type = target.GetType();
            FieldInfo field = type.GetField(memberName, InstanceFlags);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = type.GetProperty(memberName, InstanceFlags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value, null);
            }
        }

        private static void TryWatcherAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Watcher world-state effect failed and was skipped: {ex.Message}");
            }
        }

        private static class WatcherReflection
        {
            public static readonly Type RoomEffectType = FindType("Watcher.WatcherEnums+RoomEffectType");
            public static readonly Type PlacedObjectType = FindType("Watcher.WatcherEnums+PlacedObjectType");
            public static readonly Type KarmaFlowerPatchType = FindType("Watcher.KarmaFlowerPatch");
            public static readonly Type KarmaFlowerPatchDataType = FindType("Watcher.KarmaFlowerPatch+KarmaFlowerPatchData");

            public static RoomSettings.RoomEffect.Type GetRoomEffectType(string fieldName)
            {
                return GetStaticField<RoomSettings.RoomEffect.Type>(RoomEffectType, fieldName);
            }

            public static PlacedObject.Type GetPlacedObjectType(string fieldName)
            {
                return GetStaticField<PlacedObject.Type>(PlacedObjectType, fieldName);
            }

            private static T GetStaticField<T>(Type type, string fieldName) where T : class
            {
                return type?.GetField(fieldName, StaticFlags)?.GetValue(null) as T;
            }

            private static Type FindType(string fullName)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = assembly.GetType(fullName, throwOnError: false);
                    if (type != null)
                    {
                        return type;
                    }
                }

                return null;
            }
        }
    }
}
