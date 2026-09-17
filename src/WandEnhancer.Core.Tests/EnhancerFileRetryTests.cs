using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using WandEnhancer.Core;
using WandEnhancer.Core.Models;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class EnhancerFileRetryTests
    {
        [Test]
        public void Patch_RetriesWhenAsarIsLockedByAnotherProcess()
        {
            var dir = TestHelpers.CreateFakeWandDir();
            var asar = Path.Combine(dir, "resources", "app.asar");

            // Hold the archive open exclusively. This reproduces the real production failure:
            // "The process cannot access the file ... because it is being used by another
            // process." Before the retry wrapper, Patch() gave up on the first attempt and
            // the whole auto-patch run failed.
            using (var locked = new FileStream(asar, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var config = new PatchConfig
                {
                    PatchTypes = new HashSet<EPatchType> { EPatchType.ActivatePro }
                };
                config.Path = dir;

                var messages = new List<string>();
                var enhancer = new Enhancer(config.AppProps, (message, type) => messages.Add(message), config);

                Assert.Catch<IOException>(() => enhancer.Patch());

                // Assert the retry loop actually engaged, rather than inferring it from
                // elapsed time. A revert to a single File.Copy attempt produces zero of
                // these lines and fails here.
                var retries = messages.Count(m => m.Contains("blocked by another process"));
                Assert.GreaterOrEqual(retries, 9,
                    $"Expected ~9 retry attempts before giving up, saw {retries}. Messages: {string.Join(" | ", messages)}");

                Assert.IsTrue(messages.Any(m => m.Contains("Creating app.asar backup")),
                    $"Retry should name the operation it is retrying. Messages: {string.Join(" | ", messages)}");

                // A failed backup must never leave a file that the next run would try to
                // restore over the live archive — that is the data-loss path.
                Assert.IsFalse(File.Exists(asar + ".backup"),
                    "A failed backup attempt must not leave an app.asar.backup behind");
            }

            try { Directory.Delete(dir, recursive: true); } catch { }
        }

        [Test]
        public void Patch_CompletedBackupIsNotLeftAsStagingFile()
        {
            var dir = TestHelpers.CreateFakeWandDir();
            var asar = Path.Combine(dir, "resources", "app.asar");

            // Unlocked source: the staged copy must end up at the real backup path, with
            // no .part left over that a later run could trip over.
            var config = new PatchConfig
            {
                PatchTypes = new HashSet<EPatchType> { EPatchType.ActivatePro }
            };
            config.Path = dir;

            var enhancer = new Enhancer(config.AppProps, (message, type) => { }, config);

            // Patch() continues past the backup step and fails on the fake archive; only
            // the backup behaviour is under test here.
            Assert.Catch(() => enhancer.Patch());

            Assert.IsTrue(File.Exists(asar + ".backup"), "Backup was not created");
            Assert.IsFalse(File.Exists(asar + ".backup.part"), "Staging file was left behind");
            Assert.AreEqual("fake", File.ReadAllText(asar + ".backup"), "Backup content does not match the source");

            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }
}
