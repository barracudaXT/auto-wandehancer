using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Services
{
    public class WeModLocator : IWeModLocator
    {
        private readonly Func<string, bool> _pathValidator;
        private readonly bool _allowManualFallback;

        public WeModLocator(Func<string, bool> pathValidator, bool allowManualFallback = true)
        {
            _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
            _allowManualFallback = allowManualFallback;
        }

        public async Task<WeModInfo> LocateAsync(string configuredPath = null)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var info = TryBuildInfo(configuredPath);
                if (info != null) return info;
            }

            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Wand"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wand"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeMod"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wand"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Wand"),
            };

            foreach (var candidate in candidates)
            {
                var info = TryBuildInfo(candidate);
                if (info != null) return info;
            }

            var registryPath = FindInRegistry();
            if (!string.IsNullOrWhiteSpace(registryPath))
            {
                var info = TryBuildInfo(registryPath);
                if (info != null) return info;
            }

            if (_allowManualFallback)
            {
                return await PromptUserAsync();
            }

            return null;
        }

        private WeModInfo TryBuildInfo(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath)) return null;
            if (!Directory.Exists(basePath)) return null;

            // Accept a WeMod "root" folder that only has a stub Wand.exe, and resolve
            // it to the latest versioned app-* payload subfolder.
            var resolvedPath = PathExtensions.ResolveWeModPayloadPath(basePath);
            if (string.IsNullOrWhiteSpace(resolvedPath)) return null;

            if (!_pathValidator(resolvedPath)) return null;
            var exePath = Path.Combine(resolvedPath, "Wand.exe");
            if (!File.Exists(exePath)) return null;

            return new WeModInfo
            {
                BasePath = resolvedPath,
                RootPath = basePath,
                ExecutablePath = exePath,
                Version = FileVersionInfo.GetVersionInfo(exePath).FileVersion
            };
        }

        private string FindInRegistry()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                if (key == null) return null;
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using (var subKey = key.OpenSubKey(subKeyName))
                    {
                        var displayName = subKey?.GetValue("DisplayName") as string;
                        if (displayName == null) continue;
                        if (!displayName.Contains("Wand") && !displayName.Contains("WeMod")) continue;
                        var installLocation = subKey.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(installLocation)) return installLocation;
                    }
                }
            }
            return null;
        }

        private Task<WeModInfo> PromptUserAsync()
        {
            return Task.Run(() =>
            {
                using (var dialog = new FolderBrowserDialog { Description = "Select your WeMod/Wand folder (or the parent app-* folder)" })
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        return TryBuildInfo(dialog.SelectedPath);
                    }
                }
                return null;
            });
        }
    }
}
