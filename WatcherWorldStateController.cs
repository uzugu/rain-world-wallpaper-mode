using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RWCustom;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Drives Watcher-specific room states through reflection instead of compiling against Watcher internals.
    /// The Watcher types ship in Assembly-CSharp on every install (ModManager itself references them), so this
    /// is not about keeping the DLL loadable. It decouples the mod from members that can change between game
    /// versions; any member that can no longer be found is reported once in the log and its effect is skipped.
    /// </summary>
    internal sealed class WatcherWorldStateController
    {
        private const float RottedIntensity = 1f;
        private const float KarmaPatchRadius = 520f;
        private const float NaturalSeedInterval = 45f;
        private const int NoSeedAttempt = -1;
        private const int MaxLoggedFailures = 32;

        private static readonly BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private readonly HashSet<string> naturalInjectedRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> loggedFailures = new HashSet<string>();
        private string naturalWorldName;
        private int naturalSeedGeneration;
        // Generation the seeding pass last ran for in naturalWorldName; reset whenever the seed is cleared.
        private int naturalSeedAttemptedGeneration = NoSeedAttempt;
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
                    case WallpaperModOptions.RotState.RottedKarma:
                        ClearNaturalSeed(room);
                        AddSentientRotInfection(room, RottedIntensity);
                        AddRegionRotState(room, RottedIntensity);
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
                else if (state == WallpaperModOptions.RotState.RottedKarma)
                {
                    InitializeRotPresence(room, RottedIntensity);
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
                if (state == WallpaperModOptions.RotState.Rotted ||
                    state == WallpaperModOptions.RotState.RottedKarma ||
                    state == WallpaperModOptions.RotState.Natural)
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
            bool available;
            try
            {
                available = ModCompatibility.IsWatcherEnabled &&
                    WatcherReflection.RoomEffectType != null &&
                    game != null &&
                    room != null &&
                    room.game == game &&
                    room.roomSettings != null;
            }
            catch (Exception ex)
            {
                // These checks scan assemblies by reflection and run before TryWatcherAction, from Update every
                // tick and from the Room.Loaded hook. A failure means Watcher effects are unavailable, not a crash.
                LogFailureOnce(ex);
                available = false;
            }

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
                naturalSeedAttemptedGeneration = NoSeedAttempt;
            }

            if (rebuildSeed)
            {
                ClearNaturalSeed(room);
            }
            else if (naturalInjectedRooms.Count > 0 || naturalSeedAttemptedGeneration == naturalSeedGeneration)
            {
                // Update calls this every tick. Once the pass has run for this world and generation, even
                // if it seeded nothing, it is not repeated until the seed is rebuilt (refresh timer) or cleared.
                return;
            }

            // Recorded up front so a pass that seeds nothing, or fails part-way, is not retried every tick.
            naturalSeedAttemptedGeneration = naturalSeedGeneration;

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
            // The seed is going away, so the next EnsureNaturalSeed has to run its pass again.
            naturalSeedAttemptedGeneration = NoSeedAttempt;

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
            MethodInfo method = ReflectionCache.GetStaticBoolMethod(typeof(Region), "HasSentientRotResistance");
            return method != null && (bool)method.Invoke(null, new object[] { worldName });
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
            Type stateType = ReflectionCache.GetNestedType(typeof(RegionState), "SentientRotState");
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

            MethodInfo method = ReflectionCache.GetInstanceMethod(target.GetType(), methodName);
            method?.Invoke(target, args);
        }

        private static T GetMemberValue<T>(object target, string memberName, T fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            InstanceMember member = ReflectionCache.GetInstanceMember(target.GetType(), memberName);
            if (member == null)
            {
                return fallback;
            }

            if (member.Field != null && member.Field.GetValue(target) is T fieldValue)
            {
                return fieldValue;
            }

            if (member.Property != null && member.Property.GetValue(target, null) is T propertyValue)
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

            InstanceMember member = ReflectionCache.GetInstanceMember(target.GetType(), memberName);
            if (member == null)
            {
                return;
            }

            if (member.Field != null)
            {
                member.Field.SetValue(target, value);
                return;
            }

            if (member.Property != null && member.Property.CanWrite)
            {
                member.Property.SetValue(target, value, null);
            }
        }

        private void TryWatcherAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogFailureOnce(ex);
            }
        }

        // Update runs its action every tick, so a failure that repeats is only logged the first time. Keyed on
        // the full text, not the message: reflection wraps game exceptions in a TargetInvocationException with
        // a constant message, and unrelated NullReferenceExceptions share theirs. The cap stops a message
        // that changes every tick from flooding the log.
        private void LogFailureOnce(Exception ex)
        {
            string details = ex.ToString();
            bool lastAllowed;
            lock (loggedFailures)
            {
                if (loggedFailures.Count >= MaxLoggedFailures || !loggedFailures.Add(details))
                {
                    return;
                }

                lastAllowed = loggedFailures.Count >= MaxLoggedFailures;
            }

            WallpaperMod.Log?.LogWarning(
                $"Watcher world-state effect failed and was skipped (repeats of this failure are not logged again):\n{details}" +
                (lastAllowed ? "\nFurther distinct Watcher failures will not be logged this session." : string.Empty));
        }

        private sealed class InstanceMember
        {
            public readonly FieldInfo Field;
            public readonly PropertyInfo Property;

            private InstanceMember(FieldInfo field, PropertyInfo property)
            {
                Field = field;
                Property = property;
            }

            // Null when the type has neither a field nor a property with that name.
            public static InstanceMember Find(Type type, string name)
            {
                FieldInfo field = type.GetField(name, InstanceFlags);
                PropertyInfo property = type.GetProperty(name, InstanceFlags);
                return field != null || property != null ? new InstanceMember(field, property) : null;
            }
        }

        /// <summary>
        /// Caches reflection lookups per type and member name, including lookups that found nothing, so a
        /// member is searched for once instead of on every call. A member that cannot be found is reported
        /// once, because otherwise the effect that needs it would silently stop working.
        /// Only the MemberInfo is cached. Values such as the static enum fields are re-read on every use,
        /// since the game re-creates them when mods are toggled.
        /// </summary>
        private static class ReflectionCache
        {
            private static readonly MemberTable<InstanceMember> InstanceMembers = new MemberTable<InstanceMember>(
                "field or property", InstanceMember.Find);
            private static readonly MemberTable<MethodInfo> InstanceMethods = new MemberTable<MethodInfo>(
                "method", (type, name) => type.GetMethod(name, InstanceFlags));
            private static readonly MemberTable<MethodInfo> StaticBoolMethods = new MemberTable<MethodInfo>(
                "static method returning bool", FindStaticBoolMethod);
            private static readonly MemberTable<FieldInfo> StaticFields = new MemberTable<FieldInfo>(
                "static field", (type, name) => type.GetField(name, StaticFlags));
            private static readonly MemberTable<Type> NestedTypes = new MemberTable<Type>(
                "nested type", (type, name) => type.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic));

            public static InstanceMember GetInstanceMember(Type type, string name)
            {
                return InstanceMembers.Get(type, name);
            }

            public static MethodInfo GetInstanceMethod(Type type, string name)
            {
                return InstanceMethods.Get(type, name);
            }

            public static MethodInfo GetStaticBoolMethod(Type type, string name)
            {
                return StaticBoolMethods.Get(type, name);
            }

            public static FieldInfo GetStaticField(Type type, string name)
            {
                return StaticFields.Get(type, name);
            }

            public static Type GetNestedType(Type type, string name)
            {
                return NestedTypes.Get(type, name);
            }

            private static MethodInfo FindStaticBoolMethod(Type type, string name)
            {
                MethodInfo method = type.GetMethod(name, StaticFlags);
                return method != null && method.ReturnType == typeof(bool) ? method : null;
            }

            private sealed class MemberTable<T> where T : class
            {
                private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, T>> byType =
                    new ConcurrentDictionary<Type, ConcurrentDictionary<string, T>>();
                private readonly string description;
                private readonly Func<Type, string, T> find;

                public MemberTable(string description, Func<Type, string, T> find)
                {
                    this.description = description;
                    this.find = find;
                }

                public T Get(Type type, string name)
                {
                    ConcurrentDictionary<string, T> members;
                    if (!byType.TryGetValue(type, out members))
                    {
                        members = byType.GetOrAdd(type, key => new ConcurrentDictionary<string, T>());
                    }

                    T member;
                    if (members.TryGetValue(name, out member))
                    {
                        return member;
                    }

                    member = find(type, name);
                    if (members.TryAdd(name, member) && member == null)
                    {
                        WallpaperMod.Log?.LogWarning(
                            $"Watcher world-state: could not find {description} '{type.FullName}.{name}'; " +
                            "effects that depend on it will be skipped (the game may have renamed or removed it).");
                    }

                    return member;
                }
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
                // A null type was already reported by FindType.
                if (type == null)
                {
                    return null;
                }

                return ReflectionCache.GetStaticField(type, fieldName)?.GetValue(null) as T;
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

                // Runs once per type (static readonly fields above), so this warns once.
                WallpaperMod.Log?.LogWarning(
                    $"Watcher world-state: could not find type '{fullName}'; effects that depend on it will be skipped " +
                    "(the game may have renamed or removed it).");
                return null;
            }
        }
    }
}
