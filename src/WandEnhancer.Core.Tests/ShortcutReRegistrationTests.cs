using System;
using System.IO;
using Newtonsoft.Json.Linq;
using WandEnhancer.Core.Services;
using WandEnhancer.Core.Utils.Win32;

namespace WandEnhancer.Core.Tests
{
    internal static class ShortcutReRegistrationTests
    {
        public static void RunAll()
        {
            ReRegister_AfterSquirrelOverwrite_RestoresRedirect();
        }

        private static void ReRegister_AfterSquirrelOverwrite_RestoresRedirect()
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

                // 1. Create a shortcut pointing to Wand.exe (original state)
                Shortcut.CreateShortcut(shortcutPath, wandExe, "", wandDir, "Wand", wandExe + ",0");

                var shortcut = Shortcut.LoadShortcut(shortcutPath);
                if (!shortcut.TargetPath.Equals(wandExe, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Setup: shortcut should initially point to Wand.exe");

                // 2. Register — redirects to AutoPatch.exe --launch
                var registrar = new ShortcutRegistrar();
                registrar.Register(wandDir, autoPatchExe, new[] { shortcutDir });

                shortcut = Shortcut.LoadShortcut(shortcutPath);
                if (!shortcut.TargetPath.Equals(autoPatchExe, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("After first Register, shortcut should point to AutoPatch.exe");
                if (!shortcut.Arguments.Contains("--launch"))
                    throw new Exception("After first Register, shortcut args should contain --launch");

                // 3. Simulate Squirrel overwriting the shortcut back to Wand.exe
                Shortcut.CreateShortcut(shortcutPath, wandExe, "", wandDir, "Wand", wandExe + ",0");

                shortcut = Shortcut.LoadShortcut(shortcutPath);
                if (!shortcut.TargetPath.Equals(wandExe, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Squirrel simulation: shortcut should be back to Wand.exe");

                // 4. Re-register — should restore the redirect
                registrar.Register(wandDir, autoPatchExe, new[] { shortcutDir });

                shortcut = Shortcut.LoadShortcut(shortcutPath);
                if (!shortcut.TargetPath.Equals(autoPatchExe, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("After re-Register, shortcut should point to AutoPatch.exe again");
                if (!shortcut.Arguments.Contains("--launch"))
                    throw new Exception("After re-Register, shortcut args should contain --launch");

                // 5. Verify the backup file still correctly points to Wand.exe (not AutoPatch.exe)
                if (!File.Exists(backupPath))
                    throw new Exception("Backup .lnk.original file should exist after re-register");
                var backupJson = File.ReadAllText(backupPath);
                var backupTarget = JObject.Parse(backupJson)["TargetPath"].ToString();
                if (!backupTarget.Equals(wandExe, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Backup should contain the original Wand.exe path, not AutoPatch.exe");
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
