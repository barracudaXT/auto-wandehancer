using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using WandEnhancer.Core.Utils.Win32;

namespace WandEnhancer.Core.Services
{
    public class ShortcutRegistrar : IShortcutRegistrar
    {
        private const string OriginalExtension = ".original";

        private class ShortcutBackup
        {
            public string TargetPath { get; set; }
            public string Arguments { get; set; }
            public string WorkingDirectory { get; set; }
            public string Description { get; set; }
            public string IconPath { get; set; }
        }

        public void Register(string wandPath, string autoPatchExePath)
        {
            Register(wandPath, autoPatchExePath, GetShortcutSearchDirectories());
        }

        public void Register(string wandPath, string autoPatchExePath, IEnumerable<string> searchDirectories)
        {
            if (string.IsNullOrWhiteSpace(wandPath))
                throw new ArgumentException("Wand path cannot be empty.", nameof(wandPath));
            if (!Directory.Exists(wandPath))
                throw new DirectoryNotFoundException($"Wand path not found: {wandPath}");
            if (!File.Exists(autoPatchExePath))
                throw new FileNotFoundException("Auto-patch executable not found.", autoPatchExePath);

            foreach (var shortcutPath in FindWandShortcuts(wandPath, searchDirectories))
            {
                var original = Shortcut.LoadShortcut(shortcutPath);
                var backupPath = shortcutPath + OriginalExtension;

                // Only create a backup if one does not already exist, so re-registration
                // after Squirrel (or anything else) overwrites the shortcut does not destroy
                // the original target we need for uninstall/restore.
                if (!File.Exists(backupPath))
                {
                    var backup = new ShortcutBackup
                    {
                        TargetPath = original.TargetPath,
                        Arguments = original.Arguments,
                        WorkingDirectory = original.WorkingDirectory,
                        Description = original.Description,
                        IconPath = original.IconPath
                    };
                    File.WriteAllText(backupPath, JsonConvert.SerializeObject(backup, Formatting.Indented));
                }

                var launchArgs = BuildLaunchArguments(wandPath, original.Arguments);
                var iconPath = original.IconPath;
                if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath.Split(',')[0]))
                {
                    iconPath = original.TargetPath;
                }
                Shortcut.CreateShortcut(
                    shortcutPath,
                    autoPatchExePath,
                    launchArgs,
                    wandPath,
                    original.Description,
                    iconPath);
            }
        }

        public void Unregister()
        {
            foreach (var backupPath in FindOriginalBackups())
            {
                var shortcutPath = backupPath.Substring(0, backupPath.Length - OriginalExtension.Length);
                if (!File.Exists(backupPath))
                    continue;

                var json = File.ReadAllText(backupPath);
                var backup = JsonConvert.DeserializeObject<ShortcutBackup>(json);
                if (backup == null)
                    continue;

                Shortcut.CreateShortcut(
                    shortcutPath,
                    backup.TargetPath,
                    backup.Arguments,
                    backup.WorkingDirectory,
                    backup.Description,
                    backup.IconPath);

                File.Delete(backupPath);
            }
        }

        public bool IsRegistered()
        {
            return FindOriginalBackups().Count > 0;
        }

        private static string BuildLaunchArguments(string wandPath, string originalArguments)
        {
            var args = $"--launch \"{wandPath}\"";
            if (!string.IsNullOrWhiteSpace(originalArguments))
                args += " " + originalArguments;
            return args;
        }

        private static List<string> FindWandShortcuts(string wandPath, IEnumerable<string> searchDirectories)
        {
            var result = new List<string>();
            var candidateNames = new[] { "Wand.exe", "WeMod.exe" };
            var candidatePaths = candidateNames
                .Select(name => Path.Combine(wandPath, name))
                .Where(File.Exists)
                .ToList();

            foreach (var directory in searchDirectories)
            {
                if (!Directory.Exists(directory))
                    continue;

                foreach (var shortcutPath in Directory.EnumerateFiles(directory, "*.lnk", SearchOption.AllDirectories))
                {
                    try
                    {
                        var shortcut = Shortcut.LoadShortcut(shortcutPath);
                        if (string.IsNullOrWhiteSpace(shortcut.TargetPath))
                            continue;

                        if (candidatePaths.Any(candidate => IsSameTarget(shortcut.TargetPath, candidate)) ||
                            candidateNames.Any(name => shortcut.TargetPath.EndsWith(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            result.Add(shortcutPath);
                        }
                    }
                    catch
                    {
                        // Ignore shortcuts that cannot be read.
                    }
                }
            }
            return result;
        }

        private static List<string> FindOriginalBackups()
        {
            var result = new List<string>();
            foreach (var directory in GetShortcutSearchDirectories())
            {
                if (!Directory.Exists(directory))
                    continue;

                foreach (var backupPath in Directory.EnumerateFiles(directory, "*.lnk" + OriginalExtension, SearchOption.AllDirectories))
                {
                    result.Add(backupPath);
                }
            }
            return result;
        }

        private static IEnumerable<string> GetShortcutSearchDirectories()
        {
            yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        }

        private static bool IsSameTarget(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
    }
}
