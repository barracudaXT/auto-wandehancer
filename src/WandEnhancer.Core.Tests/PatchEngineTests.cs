using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WandEnhancer.Core;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class PatchEngineTests
    {
        [TestCase("v12.45.0")]
        [TestCase("v12.45.1")]
        public void PatchAllTypes_AgainstFixtures(string version)
        {
            var fixturesRoot = GetFixturesRoot();
            var versionDir = Path.Combine(fixturesRoot, version);
            Assert.IsTrue(Directory.Exists(versionDir), $"Fixture directory not found: {versionDir}");

            var tempDir = Path.Combine(Path.GetTempPath(), "wand-enhancer-test-" + version + "-" + Path.GetRandomFileName());
            try
            {
                CopyDir(versionDir, tempDir);

                var patchTypes = new HashSet<EPatchType>
                {
                    EPatchType.ActivatePro,
                    EPatchType.DisableUpdates,
                    EPatchType.DevToolsOnF12,
                    EPatchType.RemoteWebPanelPreview
                };

                var result = PatchEngine.ApplyPatches(tempDir, patchTypes);

                Assert.IsTrue(result.AllPatchesApplied,
                    $"[{version}] Failed to apply patches: {string.Join(", ", result.RemainingPatches.Select(p => p.ToString()))}");

                var allOutput = string.Join("\n", result.PatchedFiles.Values);
                Assert.IsTrue(allOutput.Contains("subscription") || allOutput.Contains("period:\"yearly\""),
                    $"[{version}] ActivatePro patch did not inject subscription marker");

                Assert.IsTrue(allOutput.Contains("__wandRemoteBridge") || allOutput.Contains("wand-remote"),
                    $"[{version}] RemoteWebPanelPreview patch did not inject bridge markers");

                Assert.IsTrue(result.PatchedFiles.ContainsKey("index.js") && result.PatchedFiles["index.js"].Contains("before-input-event"),
                    $"[{version}] DevToolsOnF12 patch did not inject before-input-event hook into index.js");

                Assert.IsTrue(result.PatchedFiles.ContainsKey("index.js") && result.PatchedFiles["index.js"].Contains("expectUpdateFeedUrl"),
                    $"[{version}] DisableUpdates patch did not modify index.js");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
        }

        private static string GetFixturesRoot()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var candidate = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "WandEnhancer.Core.Tests", "Fixtures"));
            if (Directory.Exists(candidate)) return candidate;
            candidate = Path.Combine(assemblyDir, "Fixtures");
            if (Directory.Exists(candidate)) return candidate;
            candidate = Path.GetFullPath("Fixtures");
            if (Directory.Exists(candidate)) return candidate;
            throw new DirectoryNotFoundException("Could not find Fixtures directory. Looked in: " + assemblyDir);
        }

        private static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            }
        }
    }
}
