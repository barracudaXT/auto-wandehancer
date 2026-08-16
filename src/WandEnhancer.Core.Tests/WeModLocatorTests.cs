using System;
using System.IO;
using System.Threading.Tasks;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                LocateAsync_WithConfiguredPath_ReturnsInfo().GetAwaiter().GetResult();
                LocateAsync_WithInvalidConfiguredPath_FallsBackOrReturnsNull().GetAwaiter().GetResult();
                PatchDecisionTests.RunAll();
                UpdateDetectionTests.RunAll();
                SettingsStoreTests.RunAll();
                ProcessManagerTests.RunAll();
                PatchEngineTests.RunAll();
                ShortcutReRegistrationTests.RunAll();
                Console.WriteLine("All tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed: {ex}");
                return 1;
            }
        }

        private static async Task LocateAsync_WithConfiguredPath_ReturnsInfo()
        {
            var tempDir = CreateFakeWandDir();
            try
            {
                var locator = new WeModLocator(PathExtensions.CheckWeModPath, allowManualFallback: false);
                var info = await locator.LocateAsync(tempDir);
                if (info == null) throw new Exception("Expected non-null WeModInfo");
                if (info.BasePath != tempDir) throw new Exception($"Expected BasePath={tempDir}, got {info.BasePath}");
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        private static async Task LocateAsync_WithInvalidConfiguredPath_FallsBackOrReturnsNull()
        {
            var locator = new WeModLocator(PathExtensions.CheckWeModPath, allowManualFallback: false);
            // Use a path that is not itself a valid Wand payload. The locator may
            // still fall back to auto-detecting a real install on this machine.
            var bogusPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Wand");
            var info = await locator.LocateAsync(bogusPath);
            if (info != null && string.Equals(info.BasePath, bogusPath, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Locator returned the invalid configured path as a valid install");
        }

        private static string CreateFakeWandDir()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "Wand.exe"), "fake");
            Directory.CreateDirectory(Path.Combine(path, "resources"));
            File.WriteAllText(Path.Combine(path, "resources", "app.asar"), "fake");
            return path;
        }
    }
}
