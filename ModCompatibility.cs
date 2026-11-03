using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;

namespace RainWorldWallpaperMod
{
    internal static class ModCompatibility
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        // The ModManager members are looked up once per flag, but their values are read on every call:
        // ModManager.RefreshModsLists reassigns MSC and Watcher whenever the enabled mods change.
        private static readonly ConcurrentDictionary<string, ModManagerFlag> ModManagerFlags = new ConcurrentDictionary<string, ModManagerFlag>();
        private static readonly ConcurrentDictionary<string, bool> Warned = new ConcurrentDictionary<string, bool>();

        // Scanning every loaded assembly is slow and the answer does not change, so it is done once.
        // PublicationOnly means a scan that throws is not cached and is simply tried again.
        private static readonly Lazy<bool> WatcherEnumsPresent = new Lazy<bool>(() => HasType("Watcher.WatcherEnums"), LazyThreadSafetyMode.PublicationOnly);

        public static bool IsDownpourEnabled => GetModManagerFlag("MSC");

        public static bool IsWatcherEnabled => GetModManagerFlag("Watcher") && WatcherEnumsPresent.Value;

        private static bool GetModManagerFlag(string name)
        {
            try
            {
                ModManagerFlag flag = GetFlag(name);
                if (flag.Field != null)
                {
                    return (bool)flag.Field.GetValue(null);
                }

                if (flag.Property != null)
                {
                    return (bool)flag.Property.GetValue(null, null);
                }
            }
            catch (Exception ex)
            {
                WarnOnce("read " + name, $"Could not read ModManager.{name}: {ex}");
            }

            return false;
        }

        private static ModManagerFlag GetFlag(string name)
        {
            ModManagerFlag flag;
            if (ModManagerFlags.TryGetValue(name, out flag))
            {
                return flag;
            }

            Type type = typeof(ModManager);

            FieldInfo field = type.GetField(name, StaticFlags);
            if (field != null && field.FieldType != typeof(bool))
            {
                field = null;
            }

            PropertyInfo property = type.GetProperty(name, StaticFlags);
            if (property != null && property.PropertyType != typeof(bool))
            {
                property = null;
            }

            if (field == null && property == null)
            {
                WarnOnce("missing " + name, $"Could not find ModManager.{name} as a bool field or property; treating it as false.");
            }

            flag = new ModManagerFlag(field, property);
            ModManagerFlags.TryAdd(name, flag);
            return flag;
        }

        private static void WarnOnce(string key, string message)
        {
            if (Warned.TryAdd(key, true))
            {
                WallpaperMod.Log?.LogWarning(message);
            }
        }

        private static bool HasType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(fullName, throwOnError: false) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ModManagerFlag
        {
            public readonly FieldInfo Field;
            public readonly PropertyInfo Property;

            public ModManagerFlag(FieldInfo field, PropertyInfo property)
            {
                Field = field;
                Property = property;
            }
        }
    }
}
