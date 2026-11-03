using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RainWorldWallpaperMod
{
    /// <summary>
    /// Adds a ready-made "Rain World Wallpaper Mode" application wallpaper to Wallpaper Engine's library: a project in its
    /// projects\myprojects folder that points at this game's executable, with the mod's thumbnail as the preview.
    /// Wallpaper Engine then starts the game itself, and the mod recognises it as the launcher (WallpaperHostMode), so
    /// no shortcut or launch arguments are needed. Only that one project folder is written; running it again updates it.
    /// </summary>
    internal static class WallpaperEngineSetup
    {
        internal const string WallpaperTitle = "Rain World Wallpaper Mode";
        private const string ProjectFolderName = "rainworld_wallpaper_mode";
        private const string PreviewFileName = "preview.png";

        /// <summary>
        /// Writes the project. Returns a sentence for the player either way.
        /// </summary>
        internal static bool TryAddProject(out string message)
        {
            try
            {
                string engineFolder = FindWallpaperEngineFolder();
                if (engineFolder == null)
                {
                    message = "Wallpaper Engine was not found in your Steam libraries.";
                    return false;
                }

                string gameExecutable = GetGameExecutablePath();
                string projectFolder = Path.Combine(Path.Combine(Path.Combine(engineFolder, "projects"), "myprojects"), ProjectFolderName);
                Directory.CreateDirectory(projectFolder);

                bool hasPreview = CopyPreview(projectFolder);
                File.WriteAllText(Path.Combine(projectFolder, "project.json"), BuildProjectJson(gameExecutable, hasPreview),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                WallpaperMod.Log?.LogInfo($"Wallpaper Engine project written to {projectFolder} (starts {gameExecutable})");
                message = $"Added \"{WallpaperTitle}\" to Wallpaper Engine. Pick it there; restart Wallpaper Engine if it is not listed yet.";
                return true;
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogError($"Could not add the Wallpaper Engine project: {ex}");
                message = $"Could not add it to Wallpaper Engine: {ex.Message}";
                return false;
            }
        }

        // Wallpaper Engine (Steam app 431960) lives in steamapps\common\wallpaper_engine of one of the Steam libraries:
        // the Steam folder itself, the libraries listed in its libraryfolders.vdf, or the library this game is in.
        private static string FindWallpaperEngineFolder()
        {
            foreach (string library in GetSteamLibraries())
            {
                string folder = Path.Combine(Path.Combine(Path.Combine(library, "steamapps"), "common"), "wallpaper_engine");
                if (File.Exists(Path.Combine(folder, "wallpaper64.exe")) || File.Exists(Path.Combine(folder, "wallpaper32.exe")))
                {
                    return folder;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetSteamLibraries()
        {
            var libraries = new List<string>();

            string steamRoot = null;
            try
            {
                steamRoot = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Could not read the Steam folder from the registry: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(steamRoot))
            {
                libraries.Add(steamRoot);
                string libraryList = Path.Combine(Path.Combine(steamRoot, "steamapps"), "libraryfolders.vdf");
                if (File.Exists(libraryList))
                {
                    // Lines look like:  "path"		"E:\\SteamLibrary"
                    foreach (Match match in Regex.Matches(File.ReadAllText(libraryList), "\"path\"\\s+\"([^\"]+)\""))
                    {
                        libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
                    }
                }
            }

            // The library this game is installed in: <library>\steamapps\common\Rain World\RainWorld_Data.
            DirectoryInfo gameFolder = Directory.GetParent(Application.dataPath);
            DirectoryInfo gameLibrary = gameFolder?.Parent?.Parent?.Parent;
            if (gameLibrary != null)
            {
                libraries.Add(gameLibrary.FullName);
            }

            return libraries
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => Path.GetFullPath(path))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string GetGameExecutablePath()
        {
            try
            {
                string path = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                WallpaperMod.Log?.LogWarning($"Could not read the game's executable path: {ex.Message}");
            }

            // Unity keeps the data next to the executable: RainWorld_Data belongs to RainWorld.exe.
            string dataFolder = Application.dataPath;
            string executableName = Path.GetFileName(dataFolder).Replace("_Data", string.Empty) + ".exe";
            return Path.Combine(Path.GetDirectoryName(dataFolder), executableName);
        }

        // The mod's own thumbnail (next to its plugins folder) doubles as the wallpaper's preview.
        private static bool CopyPreview(string projectFolder)
        {
            string pluginsFolder = Path.GetDirectoryName(typeof(WallpaperEngineSetup).Assembly.Location);
            string thumbnail = Path.Combine(Path.GetDirectoryName(pluginsFolder) ?? string.Empty, "thumbnail.png");
            if (!File.Exists(thumbnail))
            {
                return false;
            }

            File.Copy(thumbnail, Path.Combine(projectFolder, PreviewFileName), overwrite: true);
            return true;
        }

        private static string BuildProjectJson(string gameExecutable, bool hasPreview)
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("\t\"description\" : \"Rain World touring its own regions, run by the Rain World Wallpaper Mode mod.\",\n");
            json.Append($"\t\"file\" : \"{EscapeJson(gameExecutable)}\",\n");
            json.Append("\t\"general\" :\n\t{\n\t\t\"properties\" :\n\t\t{\n");
            json.Append("\t\t\t\"schemecolor\" :\n\t\t\t{\n");
            json.Append("\t\t\t\t\"order\" : 0,\n\t\t\t\t\"text\" : \"ui_browse_properties_scheme_color\",\n");
            json.Append("\t\t\t\t\"type\" : \"color\",\n\t\t\t\t\"value\" : \"0 0 0\"\n");
            json.Append("\t\t\t}\n\t\t}\n\t},\n");
            if (hasPreview)
            {
                json.Append($"\t\"preview\" : \"{PreviewFileName}\",\n");
            }

            json.Append($"\t\"title\" : \"{EscapeJson(WallpaperTitle)}\",\n");
            json.Append("\t\"type\" : \"application\"\n");
            json.Append("}\n");
            return json.ToString();
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
