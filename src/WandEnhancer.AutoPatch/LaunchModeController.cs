using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
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

            var startInfo = new ProcessStartInfo(info.ExecutablePath)
            {
                UseShellExecute = true,
                WorkingDirectory = info.BasePath
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
                _logger.Error($"Failed to start Wand: {ex}");
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
