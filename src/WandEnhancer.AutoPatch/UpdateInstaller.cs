using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class UpdateInstaller
    {
        private readonly ILogger _logger;
        private readonly INotificationService _notification;
        private readonly UpdateChecker _checker;

        public UpdateInstaller(ILogger logger, INotificationService notification)
            : this(logger, notification, new UpdateChecker(logger))
        {
        }

        public UpdateInstaller(ILogger logger, INotificationService notification, UpdateChecker checker)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notification = notification ?? throw new ArgumentNullException(nameof(notification));
            _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        }

        public async Task<bool> DownloadAndInstallAsync(UpdateInfo update, CancellationToken token, IProgress<int> progress = null)
        {
            if (update == null || string.IsNullOrEmpty(update.DownloadUrl))
                return false;

            _notification.ShowInfo("WandEnhancer", $"Downloading update {update.TagName}...");

            // DownloadAndVerifyAsync refuses to hand back a path whose SHA-256 does not
            // match the digest GitHub publishes for the asset.
            var installerPath = await _checker.DownloadAndVerifyAsync(update, token, progress);
            if (string.IsNullOrEmpty(installerPath))
            {
                _notification.ShowError("WandEnhancer",
                    "Update could not be verified and was not installed. Will retry later.");
                return false;
            }

            progress?.Report(-1);
            _notification.ShowInfo("WandEnhancer", "Installing update...");
            _logger.Info($"Running verified installer: {installerPath}");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Update install failed: {ex.Message}");
                _notification.ShowError("WandEnhancer", $"Update failed: {ex.Message}");
                return false;
            }
        }
    }
}
