using System.IO;
using System.Linq;

namespace WandEnhancer.Core.Extensions
{
    public static class PathExtensions
    {
        // Validates a folder that contains the actual Wand payload.
        public static bool CheckWeModPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!Directory.Exists(path)) return false;

            var requiredFiles = new[] { "Wand.exe", "resources", "app.asar" };
            return requiredFiles.All(f =>
                File.Exists(Path.Combine(path, f)) || Directory.Exists(Path.Combine(path, f)));
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
    }
}
