using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    /// <summary>
    /// Installer-facing setup commands, owned by the auto-patch executable so the
    /// setup path does not depend on the desktop app's command-line surface. The
    /// desktop app is upstream's and only understands its own launcher flags.
    /// </summary>
    internal static class AutoPatchSetup
    {
        public static int Enable(string wandPath, ILogger logger)
        {
            Log($"--enable-autopatch invoked with path: {wandPath}");

            if (string.IsNullOrWhiteSpace(wandPath))
                return Fail("No Wand folder was supplied.", logger);

            var payloadPath = WandEnhancer.Core.Extensions.PathExtensions.ResolveWeModPayloadPath(wandPath);
            if (payloadPath == null)
                return Fail($"The selected folder is not a valid Wand directory:\n{wandPath}", logger);

            var autoPatchPath = Path.GetFullPath(Environment.GetCommandLineArgs()[0]);
            Log($"AutoPatch executable: {autoPatchPath}");

            // Shortcuts point at the watcher, which patches and then launches Wand.
            // Each step is independently guarded: uninstall may run unelevated, where
            // touching the scheduled task is denied, and that must be reported rather
            // than crashing the uninstaller.
            TryStep("register shortcuts",
                () => new ShortcutRegistrar().Register(wandPath, autoPatchPath), logger);

            TryStep("create the scheduled task",
                () => new ScheduledTaskRegistrar().Create(wandPath, autoPatchPath), logger);

            // Replace any watcher already running so the tray does not end up duplicated.
            StopWatchers();

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = autoPatchPath,
                    Arguments = $"--watch \"{wandPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(autoPatchPath)
                });
                Log("Watcher process started.");
            }
            catch (Exception ex)
            {
                // Non-fatal: the scheduled task starts it at next logon.
                Log($"Failed to start watcher: {ex.Message}");
            }

            logger.Info("Auto-patch enabled.");
            return 0;
        }

        public static int Disable(ILogger logger)
        {
            Log("--disable-autopatch invoked.");

            StopWatchers();
            TryStep("unregister shortcuts", () => new ShortcutRegistrar().Unregister(), logger);
            TryStep("delete the scheduled task", () => new ScheduledTaskRegistrar().Delete(), logger);

            Log("Auto-patch disabled.");
            logger.Info("Auto-patch disabled.");
            return 0;
        }

        /// <summary>
        /// Runs one setup step, logging and swallowing failure so a denied or
        /// already-absent resource cannot abort the rest of the setup/uninstall.
        /// </summary>
        private static void TryStep(string what, Action step, ILogger logger)
        {
            try
            {
                step();
                Log($"OK: {what}");
            }
            catch (Exception ex)
            {
                Log($"FAILED to {what}: {ex.Message}");
                logger.Error($"Could not {what}: {ex.Message}");
            }
        }

        private static void StopWatchers()
        {
            var selfId = Process.GetCurrentProcess().Id;
            foreach (var proc in Process.GetProcessesByName("WandEnhancer.AutoPatch")
                         .Where(p => p.Id != selfId))
            {
                try
                {
                    if (!proc.CloseMainWindow() || !proc.WaitForExit(3000))
                    {
                        proc.Kill();
                        proc.WaitForExit(2000);
                    }
                }
                catch
                {
                    // Already gone.
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }

        private static int Fail(string message, ILogger logger)
        {
            Log(message);
            logger.Error(message);
            return 1;
        }

        private static void Log(string message)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WandEnhancer", "logs", "setup.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging is best-effort.
            }
        }
    }
}
