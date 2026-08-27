using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using WandEnhancer.Core.Services;
using WandEnhancer.Core.Utils.Win32;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class ShortcutReRegistrationTests
    {
        [Test]
        public void ReRegister_AfterSquirrelOverwrite_RestoresRedirect()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "wand-test-" + Path.GetRandomFileName());
            var wandDir = Path.Combine(tempRoot, "WeMod");
            var autoPatchDir = Path.Combine(tempRoot, "AutoPatch");
            var shortcutDir = Path.Combine(tempRoot, "StartMenu");
            var autoPatchExe = Path.Combine(autoPatchDir, "WandEnhancer.AutoPatch.exe");
            var wandExe = Path.Combine(wandDir, "Wand.exe");
            var shortcutPath = Path.Combine(shortcutDir, "Wand.lnk");
            var backupPath = shortcutPath + ".original";

            try
            {
                Directory.CreateDirectory(wandDir);
                Directory.CreateDirectory(autoPatchDir);
                Directory.CreateDirectory(shortcutDir);
                File.WriteAllText(wandExe, "fake");
                File.WriteAllText(autoPatchExe, "fake");

                Shortcut.CreateShortcut(shortcutPath, wandExe, "", wandDir, "Wand", wandExe + ",0");

                var shortcut = Shortcut.LoadShortcut(shortcutPath);
                Assert.That(shortcut.TargetPath, Is.EqualTo(wandExe).IgnoreCase,
                    "Setup: shortcut should initially point to Wand.exe");

                var registrar = new ShortcutRegistrar();
                registrar.Register(wandDir, autoPatchExe, new[] { shortcutDir });

                shortcut = Shortcut.LoadShortcut(shortcutPath);
                Assert.That(shortcut.TargetPath, Is.EqualTo(autoPatchExe).IgnoreCase,
                    "After first Register, shortcut should point to AutoPatch.exe");
                Assert.IsTrue(shortcut.Arguments.Contains("--launch"),
                    "After first Register, shortcut args should contain --launch");

                Shortcut.CreateShortcut(shortcutPath, wandExe, "", wandDir, "Wand", wandExe + ",0");

                shortcut = Shortcut.LoadShortcut(shortcutPath);
                Assert.That(shortcut.TargetPath, Is.EqualTo(wandExe).IgnoreCase,
                    "Squirrel simulation: shortcut should be back to Wand.exe");

                registrar.Register(wandDir, autoPatchExe, new[] { shortcutDir });

                shortcut = Shortcut.LoadShortcut(shortcutPath);
                Assert.That(shortcut.TargetPath, Is.EqualTo(autoPatchExe).IgnoreCase,
                    "After re-Register, shortcut should point to AutoPatch.exe again");
                Assert.IsTrue(shortcut.Arguments.Contains("--launch"),
                    "After re-Register, shortcut args should contain --launch");

                Assert.IsTrue(File.Exists(backupPath), "Backup .lnk.original file should exist after re-register");
                var backupJson = File.ReadAllText(backupPath);
                var backupTarget = JObject.Parse(backupJson)["TargetPath"].ToString();
                Assert.That(backupTarget, Is.EqualTo(wandExe).IgnoreCase,
                    "Backup should contain the original Wand.exe path, not AutoPatch.exe");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
