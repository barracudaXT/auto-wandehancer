using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
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
            Save_ConcurrentProcesses_PreservesData();
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
                    Path = tempDir,
                    LastPatchedPayloadPath = tempDir,
                    LastPatchedVersion = "12.44.0",
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
                if (loaded.LastPatchedPayloadPath != tempDir)
                    throw new Exception("Expected LastPatchedPayloadPath to round-trip");
                if (loaded.LastPatchedVersion != "12.44.0")
                    throw new Exception("Expected LastPatchedVersion to round-trip");
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
                if (loaded.PatchTypes == null || loaded.PatchTypes.Count != 2)
                    throw new Exception("Expected default PatchTypes to contain two entries");
                if (!loaded.PatchTypes.Contains(EPatchType.ActivatePro))
                    throw new Exception("Expected ActivatePro to be enabled by default");
                if (!loaded.PatchTypes.Contains(EPatchType.RemoteWebPanelPreview))
                    throw new Exception("Expected RemoteWebPanelPreview to be enabled by default");
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

        private static void Save_ConcurrentProcesses_PreservesData()
        {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "concurrent.json");
            var store = new SettingsStore(path);
            store.Save(new PatchConfig
            {
                PatchTypes = new HashSet<EPatchType> { EPatchType.ActivatePro },
                LastPatchedVersion = "0.0.0"
            });

            var tasks = new List<System.Threading.Tasks.Task>();
            var exceptions = new List<Exception>();
            var lockObj = new object();
            int completed = 0;

            try
            {
                for (int i = 0; i < 8; i++)
                {
                    int id = i;
                    tasks.Add(System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            for (int j = 0; j < 10; j++)
                            {
                                var cfg = store.Load();
                                cfg.LastPatchedVersion = $"{id}.{j}";
                                cfg.PatchTypes = new HashSet<EPatchType> { EPatchType.ActivatePro, EPatchType.RemoteWebPanelPreview };
                                store.Save(cfg);
                            }
                            System.Threading.Interlocked.Increment(ref completed);
                        }
                        catch (Exception ex)
                        {
                            lock (lockObj)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }));
                }

                System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

                if (exceptions.Count > 0)
                    throw new Exception($"Concurrent saves produced {exceptions.Count} exceptions: {exceptions[0].Message}", exceptions[0]);

                if (completed != 8)
                    throw new Exception($"Expected all 8 writers to complete, only {completed} did.");

                var final = store.Load();
                if (final.PatchTypes == null || final.PatchTypes.Count != 2)
                    throw new Exception("Expected final PatchTypes to contain two entries after concurrent writes.");

                var json = File.ReadAllText(path);
                var parsed = JsonConvert.DeserializeObject<PatchConfig>(json);
                if (parsed == null)
                    throw new Exception("Final settings file was empty or invalid after concurrent writes.");
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
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
