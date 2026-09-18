using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WandEnhancer.Core.Models;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class LaunchModeController
    {
        private readonly PatchModeController _patchController;
        private readonly ILogger _logger;

        public LaunchModeController(PatchModeController patchController, ILogger logger)
        {
            _patchController = patchController ?? throw new ArgumentNullException(nameof(patchController));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RunAsync(string configuredPath, string[] wandArgs, ProgressWindow window)
        {
            var success = await _patchController.RunAsync(configuredPath, null, window);
            if (!success)
            {
                _logger.Error("Launch aborted because patch failed.");
                // Even though patching failed, the window is already showing failure
                // controls. The caller / event handlers are responsible for launching
                // or retrying; do not close the window here.
                return;
            }

            window?.SetStatus("Starting Wand...");
            var locator = new WeModLocator(WandEnhancer.Core.Extensions.PathExtensions.CheckWeModPath, allowManualFallback: false);
            var info = await locator.LocateAsync(configuredPath);
            if (info == null)
            {
                window?.ShowFailure("Could not find Wand.exe to launch.");
                return;
            }

            var launchPath = ResolveLaunchPath(info);
            var workingDirectory = info.RootPath ?? info.BasePath;
            LaunchWand(launchPath, workingDirectory, wandArgs, window);
        }

        private static string ResolveLaunchPath(WeModInfo info)
        {
            // Launch the root stub when available (WeMod uses a launcher in the
            // parent folder that delegates to the latest app-* payload folder).
            var rootPath = info.RootPath ?? info.BasePath;
            var rootStubNames = new[] { "WeMod.exe", "Wand.exe" };
            foreach (var name in rootStubNames)
            {
                var stubPath = Path.Combine(rootPath, name);
                if (File.Exists(stubPath))
                    return stubPath;
            }

            return info.ExecutablePath;
        }

        private static void LaunchWand(string launchPath, string workingDirectory, string[] wandArgs, ProgressWindow window)
        {
            var startInfo = new ProcessStartInfo(launchPath)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDirectory
            };
            if (wandArgs != null && wandArgs.Length > 0)
            {
                startInfo.Arguments = string.Join(" ", wandArgs.Select(EscapeArgument));
            }

            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                window?.ShowFailure($"Failed to start Wand: {ex.Message}");
                return;
            }

            window?.SafeClose();
        }

        private static string EscapeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg) || arg.All(c => !char.IsWhiteSpace(c) && c != '"'))
                return arg;
            return "\"" + arg.Replace("\"", "\\\"") + "\"";
        }
    }
}
