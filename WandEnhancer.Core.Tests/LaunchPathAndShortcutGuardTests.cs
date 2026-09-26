using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using WandEnhancer.AutoPatch;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;
using WandEnhancer.Core.Utils.Win32;

namespace WandEnhancer.Core.Tests
{
    [TestFixture]
    public class LaunchPathAndShortcutGuardTests
    {
        // --launch resolved WeMod.exe first. On a patched install that file is
        // Wand-Migrator, which exits without launching anything, so Wand never
        // appeared and the launcher exited 0. Wand.exe is the launcher.
        [Test]
        public void ResolveLaunchPath_PrefersWandExe_WhenBothCandidatesExist()
        {
            var root = Path.Combine(Path.GetTempPath(), "awh-launch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var wand = Path.Combine(root, "Wand.exe");
                File.WriteAllText(wand, "x");
                File.WriteAllText(Path.Combine(root, "WeMod.exe"), "x");

                var info = new WeModInfo
                {
                    RootPath = root,
                    BasePath = root,
                    ExecutablePath = wand,
                    Version = "12.57.0"
                };

                var method = typeof(LaunchModeController).GetMethod(
                    "ResolveLaunchPath", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(method, "LaunchModeController.ResolveLaunchPath was not found.");

                var resolved = (string)method.Invoke(null, new object[] { info });

                Assert.That(resolved, Is.EqualTo(wand).IgnoreCase,
                    "Launch must run Wand.exe. WeMod.exe on a patched install is Wand-Migrator " +
                    "and exits without starting Wand, which makes the launch fail silently.");
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }

        // Re-registering for a synthetic Wand path rewrote the user's real Start Menu
        // shortcut to point at a test build and a temporary payload folder. The search
        // is confined to the directories passed in, so this drives those and never
        // touches the real Desktop or Start Menu: a regression can only affect these
        // temporary shortcuts, not the machine.
        [Test]
        public void Register_OnlyRewritesShortcuts_PointingIntoTheGivenWandDirectory()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "awh-guard-" + Guid.NewGuid().ToString("N"));
            var wandDir = Path.Combine(tempRoot, "WeMod");
            var otherInstall = Path.Combine(tempRoot, "other-install");
            var shortcutDir = Path.Combine(tempRoot, "StartMenu");
            var autoPatchExe = Path.Combine(tempRoot, "WandEnhancer.AutoPatch.exe");

            Directory.CreateDirectory(wandDir);
            Directory.CreateDirectory(otherInstall);
            Directory.CreateDirectory(shortcutDir);
            File.WriteAllText(Path.Combine(wandDir, "Wand.exe"), "x");
            File.WriteAllText(Path.Combine(otherInstall, "Wand.exe"), "x");
            File.WriteAllText(autoPatchExe, "x");

            var foreignShortcut = Path.Combine(shortcutDir, "Foreign (WeMod).lnk");
            var ownShortcut = Path.Combine(shortcutDir, "Wand (WeMod).lnk");
            var foreignTarget = Path.Combine(otherInstall, "Wand.exe");
            var ownTarget = Path.Combine(wandDir, "Wand.exe");

            try
            {
                Shortcut.CreateShortcut(foreignShortcut, foreignTarget, "", otherInstall, "Foreign", foreignTarget + ",0");
                Shortcut.CreateShortcut(ownShortcut, ownTarget, "", wandDir, "Wand", ownTarget + ",0");

                new ShortcutRegistrar().Register(wandDir, autoPatchExe, new[] { shortcutDir });

                var foreign = Shortcut.LoadShortcut(foreignShortcut);
                Assert.That(foreign.TargetPath, Is.EqualTo(foreignTarget).IgnoreCase,
                    "A shortcut pointing at a DIFFERENT Wand installation must not be rewritten. " +
                    "Rewriting any shortcut that merely ends in Wand.exe is what repointed the " +
                    "user's real Start Menu shortcut at a temporary test payload.");

                var own = Shortcut.LoadShortcut(ownShortcut);
                Assert.That(own.TargetPath, Is.EqualTo(autoPatchExe).IgnoreCase,
                    "Positive control: a shortcut pointing into the given Wand directory must " +
                    "still be rewritten, or legitimate registration has been broken.");
                Assert.IsTrue(own.Arguments.Contains("--launch"),
                    "Positive control: the rewritten shortcut must carry --launch.");
            }
            finally
            {
                try { Directory.Delete(tempRoot, recursive: true); } catch { }
            }
        }

        // An unrelated *.lnk.original must not stop the genuine backups being restored.
        // The search covers every such file under the real Desktop and Start Menu, so one
        // unreadable foreign file previously threw and aborted the whole restore pass.
        [Test]
        public void Unregister_RestoresGenuineBackup_WhenAnotherBackupIsUnreadable()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "awh-backup-" + Guid.NewGuid().ToString("N"));
            var wandDir = Path.Combine(tempRoot, "WeMod");
            var otherInstall = Path.Combine(tempRoot, "other-install");
            var shortcutDir = Path.Combine(tempRoot, "StartMenu");
            var autoPatchExe = Path.Combine(tempRoot, "WandEnhancer.AutoPatch.exe");

            Directory.CreateDirectory(wandDir);
            Directory.CreateDirectory(otherInstall);
            Directory.CreateDirectory(shortcutDir);
            File.WriteAllText(Path.Combine(wandDir, "Wand.exe"), "x");
            File.WriteAllText(autoPatchExe, "x");

            var originalTarget = Path.Combine(otherInstall, "Wand.exe");
            Shortcut.CreateShortcut(Path.Combine(shortcutDir, "Wand (WeMod).lnk"), originalTarget, "", otherInstall, "Wand", originalTarget + ",0");

            try
            {
                new ShortcutRegistrar().Register(wandDir, autoPatchExe, new[] { shortcutDir });

                // A foreign, unreadable backup sitting beside the genuine one.
                File.WriteAllText(Path.Combine(shortcutDir, "Foreign Program.lnk.original"), "not json at all");

                new ShortcutRegistrar().Unregister();

                var restored = Shortcut.LoadShortcut(Path.Combine(shortcutDir, "Wand (WeMod).lnk"));
                Assert.That(restored.TargetPath, Is.EqualTo(originalTarget).IgnoreCase,
                    "An unreadable foreign backup must be skipped, not abort the whole restore, " +
                    "or the user's shortcut is left pointing at the auto-patch executable.");
            }
            finally
            {
                try { Directory.Delete(tempRoot, recursive: true); } catch { }
            }
        }
    }
}
