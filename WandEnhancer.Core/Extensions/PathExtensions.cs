using System.IO;
using System.Linq;

namespace WandEnhancer.Core.Extensions
{
    public static class PathExtensions
    {
        private static readonly string[] ExecutableNames = { "Wand.exe", "WeMod.exe" };

        // Returns the name of the WeMod/Wand executable inside the given folder,
        // or null if neither Wand.exe nor WeMod.exe exists.
        public static string GetWeModExecutableName(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return null;

            foreach (var name in ExecutableNames)
            {
                if (File.Exists(Path.Combine(path, name)))
                    return name;
            }

            return null;
        }

        // Validates a folder that contains the actual Wand payload.
        // Note: WeMod stores app.asar inside the resources directory, not at the root.
        public static bool CheckWeModPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!Directory.Exists(path)) return false;

            return GetWeModExecutableName(path) != null &&
                   Directory.Exists(Path.Combine(path, "resources")) &&
                   File.Exists(Path.Combine(path, "resources", "app.asar"));
        }

        // WeMod installs under a versioned app-* subfolder (e.g. app-12.43.1)
        // while the parent folder only contains a stub Wand.exe/WeMod.exe.
        // This resolves the parent to the latest valid app-* subfolder.
        public static string ResolveWeModPayloadPath(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath)) return null;
            if (!Directory.Exists(basePath)) return null;
            if (CheckWeModPath(basePath)) return basePath;

            var latest = Directory.EnumerateDirectories(basePath, "app-*")
                .Where(CheckWeModPath)
                .OrderByDescending(d =>
                {
                    var versionPart = Path.GetFileName(d).Substring(4);
                    var parts = versionPart.Split('.');
                    long score = 0;
                    foreach (var part in parts)
                    {
                        if (long.TryParse(part, out var value))
                            score = score * 10000 + value;
                        else
                            score = score * 10000;
                    }
                    return score;
                })
                .FirstOrDefault();

            return latest;
        }

        public static bool IsAlreadyPatched(string payloadPath)
        {
            if (string.IsNullOrWhiteSpace(payloadPath)) return false;
            if (!Directory.Exists(payloadPath)) return false;

            var resourcesPath = Path.Combine(payloadPath, "resources");
            return File.Exists(Path.Combine(resourcesPath, "app.asar.backup")) ||
                   Directory.Exists(Path.Combine(resourcesPath, "app.asar.unpacked.backup"));
        }
    }
}
