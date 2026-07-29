using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using WandEnhancer.Utils.Win32;

namespace WandEnhancer.Services
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
            if (string.IsNullOrWhiteSpace(wandPath))
                throw new ArgumentException("Wand path cannot be empty.", nameof(wandPath));
            if (!Directory.Exists(wandPath))
                throw new DirectoryNotFoundException($"Wand path not found: {wandPath}");
            if (!File.Exists(autoPatchExePath))
                throw new FileNotFoundException("Auto-patch executable not found.", autoPatchExePath);

            foreach (var shortcutPath in FindWandShortcuts(wandPath))
            {
                var original = Shortcut.LoadShortcut(shortcutPath);
                var backupPath = shortcutPath + OriginalExtension;
                var backup = new ShortcutBackup
                {
                    TargetPath = original.TargetPath,
                    Arguments = original.Arguments,
                    WorkingDirectory = original.WorkingDirectory,
                    Description = original.Description,
                    IconPath = original.IconPath
                };
                File.WriteAllText(backupPath, JsonConvert.SerializeObject(backup, Formatting.Indented));

                var launchArgs = BuildLaunchArguments(wandPath, original.Arguments);
                Shortcut.CreateShortcut(
                    shortcutPath,
                    autoPatchExePath,
                    launchArgs,
                    wandPath,
                    original.Description,
                    original.IconPath);
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

        private static string BuildLaunchArguments(string wandPath, string originalArguments)
        {
            var args = $"--launch \"{wandPath}\"";
            if (!string.IsNullOrWhiteSpace(originalArguments))
                args += " " + originalArguments;
            return args;
        }

        private static List<string> FindWandShortcuts(string wandPath)
        {
            var result = new List<string>();
            var wandExePath = Path.Combine(wandPath, "Wand.exe");
            foreach (var directory in GetShortcutSearchDirectories())
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

                        if (IsSameTarget(shortcut.TargetPath, wandExePath) ||
                            shortcut.TargetPath.EndsWith("Wand.exe", StringComparison.OrdinalIgnoreCase))
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
