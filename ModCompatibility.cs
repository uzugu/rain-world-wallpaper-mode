using System;
using System.Reflection;

namespace RainWorldWallpaperMod
{
    internal static class ModCompatibility
    {
        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        public static bool IsDownpourEnabled => GetModManagerFlag("MSC");

        public static bool IsWatcherEnabled => GetModManagerFlag("Watcher") && HasType("Watcher.WatcherEnums");

        private static bool GetModManagerFlag(string name)
        {
            try
            {
                Type type = typeof(ModManager);

                FieldInfo field = type.GetField(name, StaticFlags);
                if (field != null && field.FieldType == typeof(bool))
                {
                    return (bool)field.GetValue(null);
                }

                PropertyInfo property = type.GetProperty(name, StaticFlags);
                if (property != null && property.PropertyType == typeof(bool))
                {
                    return (bool)property.GetValue(null, null);
                }
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Could not read ModManager.{name}: {ex.Message}");
            }

            return false;
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
    }
}
