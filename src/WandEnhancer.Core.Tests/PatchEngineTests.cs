using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using WandEnhancer.Core;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Tests
{
    internal static class PatchEngineTests
    {
        public static void RunAll()
        {
            // Test against both fixture versions
            PatchAllTypes_AgainstFixtures("v12.45.0");
            PatchAllTypes_AgainstFixtures("v12.45.1");
        }

        private static string GetFixturesRoot()
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // When running from bin/Release, fixtures are at ../../../WandEnhancer.Core.Tests/Fixtures
            var candidate = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "WandEnhancer.Core.Tests", "Fixtures"));
            if (Directory.Exists(candidate)) return candidate;
            // When running from the test project directory directly
            candidate = Path.Combine(assemblyDir, "Fixtures");
            if (Directory.Exists(candidate)) return candidate;
            // Try relative to current directory
            candidate = Path.GetFullPath("Fixtures");
            if (Directory.Exists(candidate)) return candidate;
            throw new DirectoryNotFoundException("Could not find Fixtures directory. Looked in: " + assemblyDir + ", " + Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "WandEnhancer.Core.Tests", "Fixtures")));
        }

        private static void PatchAllTypes_AgainstFixtures(string version)
        {
            var fixturesRoot = GetFixturesRoot();
            var versionDir = Path.Combine(fixturesRoot, version);
            if (!Directory.Exists(versionDir))
                throw new Exception($"Fixture directory not found: {versionDir}");

            // Copy fixtures to a temp dir so we don't modify the originals
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

                if (!result.AllPatchesApplied)
                {
                    var failed = string.Join(", ", result.RemainingPatches.Select(p => p.ToString()));
                    throw new Exception($"[{version}] Failed to apply patches: {failed}. The Wand bundle version may not be supported by the current regex patterns.");
                }

                // Verify ActivatePro patches left markers in the output
                var allOutput = string.Join("\n", result.PatchedFiles.Values);
                if (!allOutput.Contains("subscription") && !allOutput.Contains("period:\"yearly\""))
                    throw new Exception($"[{version}] ActivatePro patch did not inject subscription marker into output");

                // Verify RemoteWebPanelPreview patches left markers
                if (!allOutput.Contains("__wandRemoteBridge") && !allOutput.Contains("wand-remote"))
                    throw new Exception($"[{version}] RemoteWebPanelPreview patch did not inject bridge markers into output");

                // Verify DevToolsOnF12 patch left markers
                if (!result.PatchedFiles.ContainsKey("index.js") || !result.PatchedFiles["index.js"].Contains("before-input-event"))
                    throw new Exception($"[{version}] DevToolsOnF12 patch did not inject before-input-event hook into index.js");

                // Verify DisableUpdates patch left markers
                if (!result.PatchedFiles.ContainsKey("index.js") || !result.PatchedFiles["index.js"].Contains("expectUpdateFeedUrl"))
                    throw new Exception($"[{version}] DisableUpdates patch did not modify index.js");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
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
