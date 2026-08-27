using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class SettingsStoreTests
    {
        [Test]
        public void SaveAndLoad_RoundTripsConfig()
        {
            var tempDir = TestHelpers.CreateFakeWandDir();
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

                Assert.IsNotNull(loaded.PatchTypes);
                Assert.AreEqual(3, loaded.PatchTypes.Count, "Expected PatchTypes to round-trip");
                Assert.IsTrue(loaded.PatchTypes.Contains(EPatchType.ActivatePro));
                Assert.IsTrue(loaded.PatchTypes.Contains(EPatchType.DisableUpdates));
                Assert.IsTrue(loaded.PatchTypes.Contains(EPatchType.DevToolsOnF12));
                Assert.AreEqual(1, loaded.CustomScriptPaths.Count);
                Assert.AreEqual("script.js", loaded.CustomScriptPaths[0]);
                Assert.IsTrue(loaded.AutoApplyPatches);
                Assert.AreEqual(tempDir, loaded.Path);
                Assert.AreEqual(tempDir, loaded.LastPatchedPayloadPath);
                Assert.AreEqual("12.44.0", loaded.LastPatchedVersion);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
                File.Delete(path);
                File.Delete(path + ".backup");
                File.Delete(path + ".tmp");
            }
        }

        [Test]
        public void Load_MissingFile_ReturnsDefaults()
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            try
            {
                var store = new SettingsStore(path);
                var loaded = store.Load();
                Assert.IsNotNull(loaded.PatchTypes);
                Assert.AreEqual(2, loaded.PatchTypes.Count, "Expected default PatchTypes to contain two entries");
                Assert.IsTrue(loaded.PatchTypes.Contains(EPatchType.ActivatePro));
                Assert.IsTrue(loaded.PatchTypes.Contains(EPatchType.RemoteWebPanelPreview));
                Assert.IsFalse(loaded.AutoApplyPatches);
                Assert.IsNull(loaded.Path);
            }
            finally
            {
                File.Delete(path);
                File.Delete(path + ".backup");
                File.Delete(path + ".tmp");
            }
        }

        [Test]
        public void Save_CreatesDirectoryAndFile()
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
                Assert.IsTrue(File.Exists(path), "Expected settings file to be created");
            }
            finally
            {
                if (Directory.Exists(Path.GetDirectoryName(path)))
                    Directory.Delete(Path.GetDirectoryName(path), recursive: true);
            }
        }

        [Test]
        public void Save_ConcurrentProcesses_PreservesData()
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

                Assert.IsEmpty(exceptions, $"Concurrent saves produced {exceptions.Count} exceptions");
                Assert.AreEqual(8, completed, "Expected all 8 writers to complete");

                var final = store.Load();
                Assert.IsNotNull(final.PatchTypes);
                Assert.AreEqual(2, final.PatchTypes.Count, "Expected final PatchTypes to contain two entries after concurrent writes");

                var json = File.ReadAllText(path);
                var parsed = JsonConvert.DeserializeObject<PatchConfig>(json);
                Assert.IsNotNull(parsed, "Final settings file was empty or invalid after concurrent writes");
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

    }
}
