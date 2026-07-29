using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    internal static class SettingsStoreTests
    {
        public static void RunAll()
        {
            SaveAndLoad_RoundTripsConfig();
            Load_MissingFile_ReturnsDefaults();
            Save_CreatesDirectoryAndFile();
        }

        private static void SaveAndLoad_RoundTripsConfig()
        {
            var tempDir = CreateFakeWandDir();
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            try
            {
                var store = new SettingsStore(path);
                var config = new PatchConfig
                {
                    PatchTypes = new HashSet<EPatchType>
                    {
                        EPatchType.ActivatePro,
                        EPatchType.DisableUpdates,
                        EPatchType.DevToolsOnF12
                    },
                    CustomScriptPaths = new List<string> { "script.js" },
                    AutoApplyPatches = true,
                    Path = tempDir
                };
                store.Save(config);
                var loaded = store.Load();

                if (loaded.PatchTypes == null || loaded.PatchTypes.Count != 3)
                    throw new Exception("Expected PatchTypes to round-trip");
                if (!loaded.PatchTypes.Contains(EPatchType.ActivatePro) ||
                    !loaded.PatchTypes.Contains(EPatchType.DisableUpdates) ||
                    !loaded.PatchTypes.Contains(EPatchType.DevToolsOnF12))
                    throw new Exception("Expected PatchTypes values to round-trip");
                if (loaded.CustomScriptPaths.Count != 1 || loaded.CustomScriptPaths[0] != "script.js")
                    throw new Exception("Expected CustomScriptPaths to round-trip");
                if (!loaded.AutoApplyPatches)
                    throw new Exception("Expected AutoApplyPatches to be true");
                if (loaded.Path != tempDir)
                    throw new Exception($"Expected Path={tempDir}, got {loaded.Path}");
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
                File.Delete(path);
                File.Delete(path + ".backup");
                File.Delete(path + ".tmp");
            }
        }

        private static void Load_MissingFile_ReturnsDefaults()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            try
            {
                var store = new SettingsStore(path);
                var loaded = store.Load();
                if (loaded.PatchTypes != null && loaded.PatchTypes.Any())
                    throw new Exception("Expected empty PatchTypes by default");
                if (loaded.AutoApplyPatches)
                    throw new Exception("Expected AutoApplyPatches default false");
                if (loaded.Path != null)
                    throw new Exception($"Expected null Path, got {loaded.Path}");
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".backup");
                File.Delete(path + ".tmp");
            }
        }

        private static void Save_CreatesDirectoryAndFile()
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "nested");
            var path = Path.Combine(dir, "settings.json");
            try
            {
                var store = new SettingsStore(path);
                store.Save(new PatchConfig
                {
                    PatchTypes = new HashSet<EPatchType> { EPatchType.ActivatePro }
                });
                if (!File.Exists(path))
                    throw new Exception("Expected settings file to be created");
            }
            finally
            {
                if (Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.Delete(Path.GetDirectoryName(path), recursive: true);
            }
        }

        private static string CreateFakeWandDir()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "Wand.exe"), "fake");
            Directory.CreateDirectory(Path.Combine(path, "resources"));
            File.WriteAllText(Path.Combine(path, "app.asar"), "fake");
            return path;
        }
    }
}
