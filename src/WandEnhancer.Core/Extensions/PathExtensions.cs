using System.IO;
using System.Linq;

namespace WandEnhancer.Core.Extensions
{
    public static class PathExtensions
    {
        public static bool CheckWeModPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!Directory.Exists(path)) return false;

            var requiredFiles = new[] { "Wand.exe", "resources", "app.asar" };
            return requiredFiles.All(f =>
                File.Exists(Path.Combine(path, f)) || Directory.Exists(Path.Combine(path, f)));
        }
    }
}
