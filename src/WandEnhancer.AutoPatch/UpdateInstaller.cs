using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WandEnhancer.Core.Services;

namespace WandEnhancer.AutoPatch
{
    public class UpdateInstaller
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        private readonly ILogger _logger;
        private readonly INotificationService _notification;

        public UpdateInstaller(ILogger logger, INotificationService notification)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notification = notification ?? throw new ArgumentNullException(nameof(notification));
        }

        public async Task<bool> DownloadAndInstallAsync(UpdateInfo update, CancellationToken token, IProgress<int> progress = null)
        {
            if (update == null || string.IsNullOrEmpty(update.DownloadUrl))
                return false;

            var tempDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WandEnhancer", "updates");
            Directory.CreateDirectory(tempDir);

            var installerPath = Path.Combine(tempDir, "WandEnhancerSetup.exe");

            try
            {
                _logger.Info($"Downloading update {update.TagName} from {update.DownloadUrl}");
                _notification.ShowInfo("WandEnhancer", $"Downloading update {update.TagName}...");

                using (var response = await HttpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        long bytesRead = 0;
                        int read;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read, token);
                            bytesRead += read;

                            if (progress != null && totalBytes.HasValue && totalBytes.Value > 0)
                            {
                                var pct = (int)(bytesRead * 100 / totalBytes.Value);
                                progress.Report(pct);
                            }
                        }
                    }
                }

                if (token.IsCancellationRequested)
                    return false;

                _logger.Info($"Download complete. Running installer: {installerPath}");
                progress?.Report(-1);
                _notification.ShowInfo("WandEnhancer", "Installing update...");

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
            catch (OperationCanceledException)
            {
                _logger.Info("Update download was cancelled.");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"Update download failed: {ex.Message}");
                _notification.ShowError("WandEnhancer", "Update download failed. Will retry later.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"Update install failed: {ex.Message}");
                _notification.ShowError("WandEnhancer", $"Update failed: {ex.Message}");
                return false;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "WandEnhancer-AutoUpdate");
            return client;
        }
    }
}
