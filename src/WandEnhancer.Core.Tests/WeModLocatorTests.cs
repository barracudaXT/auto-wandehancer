using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using WandEnhancer.Core.Extensions;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class WeModLocatorTests
    {
        [Test]
        public async Task LocateAsync_WithConfiguredPath_ReturnsInfo()
        {
            var tempDir = CreateFakeWandDir();
            try
            {
                var locator = new WeModLocator(PathExtensions.CheckWeModPath, allowManualFallback: false);
                var info = await locator.LocateAsync(tempDir);
                Assert.IsNotNull(info, "Expected non-null WeModInfo");
                Assert.AreEqual(tempDir, info.BasePath);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Test]
        public async Task LocateAsync_WithInvalidConfiguredPath_FallsBackOrReturnsNull()
        {
            var locator = new WeModLocator(PathExtensions.CheckWeModPath, allowManualFallback: false);
            var bogusPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "Wand");
            var info = await locator.LocateAsync(bogusPath);
            if (info != null)
                Assert.AreNotEqual(bogusPath, info.BasePath, "Locator returned the invalid configured path as a valid install");
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
