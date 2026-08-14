using System;
using System.IO;
using System.Threading.Tasks;
using WandEnhancer.AutoPatch;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    internal static class UpdateDetectionTests
    {
        public static void RunAll()
        {
            VersionChange_TriggersRepatch().GetAwaiter().GetResult();
        }

        private static async Task VersionChange_TriggersRepatch()
        {
            var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var v1Path = Path.Combine(root, "app-12.43.1");
            var v2Path = Path.Combine(root, "app-12.44.0");

            Directory.CreateDirectory(v1Path);
            Directory.CreateDirectory(v2Path);

            try
            {
                CreateFakePayload(v1Path, withBackup: true);  // already patched
                CreateFakePayload(v2Path, withBackup: false); // updated, not yet patched

                // Simulate a Wand install at app-12.43.1 that was already patched.
                var config = new PatchConfig
                {
                    Path = root,
                    LastPatchedPayloadPath = v1Path,
                    LastPatchedVersion = "12.43.1",
                    PatchingCompleted = true
                };

                var settingsStore = new FakeSettingsStore(config);
                var patcher = new FakePatcher();

                // First call: same version — should SKIP (patcher not called).
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
                if (!result1) throw new Exception("First RunAsync should return true (already patched)");
                if (patcher.CallCount != 0) throw new Exception("Patcher should NOT be called when version matches");

                // Second call: Wand updated to app-12.44.0 — should RE-PATCH.
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
                if (!result2) throw new Exception("Second RunAsync should return true (patch succeeded)");
                if (patcher.CallCount != 1) throw new Exception("Patcher should be called exactly once after version change — this is the update-detection regression test");

                // Verify the recorded identity was updated to the new version.
                if (config.LastPatchedPayloadPath != v2Path)
                    throw new Exception("LastPatchedPayloadPath should be updated to the new version folder");
                if (config.LastPatchedVersion != "12.44.0")
                    throw new Exception("LastPatchedVersion should be updated to 12.44.0");
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
