using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using WandEnhancer.AutoPatch;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class UpdateDetectionTests
    {
        [Test]
        public async Task VersionChange_TriggersRepatch()
        {
            var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var v1Path = Path.Combine(root, "app-12.43.1");
            var v2Path = Path.Combine(root, "app-12.44.0");

            Directory.CreateDirectory(v1Path);
            Directory.CreateDirectory(v2Path);

            try
            {
                CreateFakePayload(v1Path, withBackup: true);
                CreateFakePayload(v2Path, withBackup: false);

                var config = new PatchConfig
                {
                    Path = root,
                    LastPatchedPayloadPath = v1Path,
                    LastPatchedVersion = "12.43.1",
                    PatchingCompleted = true
                };

                var settingsStore = new FakeSettingsStore(config);
                var patcher = new FakePatcher();

                var infoV1 = new WeModInfo
                {
                    BasePath = v1Path,
                    RootPath = root,
                    ExecutablePath = Path.Combine(v1Path, "Wand.exe"),
                    Version = "12.43.1"
                };
                var locatorV1 = new FakeWeModLocator(infoV1);
                var controllerV1 = new PatchModeController(
                    settingsStore, locatorV1, new FakeProcessManager(),
                    patcher, new FakeLogger(), new FakeNotificationService());

                var result1 = await controllerV1.RunAsync(root, null, null);
                Assert.IsTrue(result1, "First RunAsync should return true (already patched)");
                Assert.AreEqual(0, patcher.CallCount, "Patcher should NOT be called when version matches");

                var infoV2 = new WeModInfo
                {
                    BasePath = v2Path,
                    RootPath = root,
                    ExecutablePath = Path.Combine(v2Path, "Wand.exe"),
                    Version = "12.44.0"
                };
                var locatorV2 = new FakeWeModLocator(infoV2);
                var controllerV2 = new PatchModeController(
                    settingsStore, locatorV2, new FakeProcessManager(),
                    patcher, new FakeLogger(), new FakeNotificationService());

                var result2 = await controllerV2.RunAsync(root, null, null);
                Assert.IsTrue(result2, "Second RunAsync should return true (patch succeeded)");
                Assert.AreEqual(1, patcher.CallCount, "Patcher should be called exactly once after version change");

                Assert.AreEqual(v2Path, config.LastPatchedPayloadPath, "LastPatchedPayloadPath should be updated");
                Assert.AreEqual("12.44.0", config.LastPatchedVersion, "LastPatchedVersion should be updated");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static void CreateFakePayload(string basePath, bool withBackup)
        {
            var resources = Path.Combine(basePath, "resources");
            Directory.CreateDirectory(resources);
            File.WriteAllText(Path.Combine(basePath, "Wand.exe"), "fake");
            File.WriteAllText(Path.Combine(resources, "app.asar"), "fake");
            if (withBackup)
                File.WriteAllText(Path.Combine(resources, "app.asar.backup"), "fake");
        }
    }
}
