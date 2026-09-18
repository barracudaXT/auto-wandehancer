using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WandEnhancer.Core.Services
{
    public class UpdateInfo
    {
        public Version LatestVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public string TagName { get; set; }

        /// <summary>Lower-case hex SHA-256 published by GitHub for the asset, or null.</summary>
        public string Sha256 { get; set; }
    }

    public class UpdateChecker
    {
        private const string ReleasesApiUrl =
            "https://api.github.com/repos/barracudaXT/auto-wandehancer/releases/latest";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        private readonly ILogger _logger;

        public UpdateChecker(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Version GetCurrentVersion()
        {
            return (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;
        }

        public async Task<UpdateInfo> CheckForUpdateAsync()
        {
            try
            {
                var json = await HttpClient.GetStringAsync(ReleasesApiUrl);

                var release = JObject.Parse(json);
                var tagName = release["tag_name"]?.ToString();
                if (string.IsNullOrEmpty(tagName))
                    return null;

                var versionString = tagName.TrimStart('v', 'V');
                if (!Version.TryParse(versionString, out var latestVersion))
                {
                    _logger.Error($"Could not parse version from tag: {tagName}");
                    return null;
                }

                var assets = release["assets"] as JArray;
                string downloadUrl = null;
                string digest = null;
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        var name = asset["name"]?.ToString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            downloadUrl = asset["browser_download_url"]?.ToString();
                            digest = asset["digest"]?.ToString();
                            break;
                        }
                    }
                }

                return new UpdateInfo
                {
                    LatestVersion = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = release["body"]?.ToString(),
                    TagName = tagName,
                    Sha256 = ParseDigest(digest)
                };
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"Update check failed (network): {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"Update check failed: {ex.Message}");
                return null;
            }
        }

        public bool IsUpdateAvailable(UpdateInfo info)
        {
            if (info?.LatestVersion == null || string.IsNullOrEmpty(info.DownloadUrl))
                return false;

            var current = GetCurrentVersion();
            return info.LatestVersion > current;
        }

        /// <summary>
        /// Downloads the installer, verifies it against the SHA-256 GitHub publishes for
        /// the release asset, and returns a read handle on the verified bytes. Returns
        /// null (refusing to install) when the digest is absent or does not match.
        ///
        /// The handle is opened with FileShare.Read and stays open for as long as the
        /// caller holds it. It permits the elevated installer process to read the file,
        /// while withholding the FILE_SHARE_DELETE a rename-based substitution needs.
        /// The handle is re-hashed after opening so that the bytes handed to the caller
        /// are the bytes that were verified, not merely a file that once matched.
        /// The caller owns the handle and must dispose it.
        /// </summary>
        public async Task<FileStream> DownloadAndVerifyAsync(UpdateInfo update, CancellationToken token, IProgress<int> progress = null)
        {
            if (update == null || string.IsNullOrEmpty(update.DownloadUrl))
                return null;

            if (string.IsNullOrEmpty(update.Sha256))
            {
                _logger.Error("Update publish has no SHA-256 digest; refusing to install an unverified executable.");
                return null;
            }

            var tempDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WandEnhancer", "updates");
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, "WandEnhancerSetup.exe");

            try
            {
                _logger.Info($"Downloading update {update.TagName} from {update.DownloadUrl}");

                string written;
                using (var response = await HttpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength;

                    // Hash from the handle that wrote the file, so the digest describes the
                    // bytes we actually wrote rather than a re-read of the path.
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 8192, true))
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
                                progress.Report((int)(bytesRead * 100 / totalBytes.Value));
                            }
                        }

                        fileStream.Flush(true);
                        written = ComputeSha256(fileStream);
                    }
                }

                if (token.IsCancellationRequested)
                    return null;

                if (!string.Equals(written, update.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error($"Update digest mismatch (expected {update.Sha256}, got {written}). Refusing to install.");
                    TryDelete(installerPath);
                    return null;
                }

                // Open the handle the caller keeps, and hash THAT stream. The exclusive
                // writing handle has to be released to open for shared read, so this
                // re-check is what guarantees the bytes handed on are the verified bytes:
                // if anything was swapped in that gap, the digest will not match again.
                var verified = new FileStream(installerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                string reopened;
                try
                {
                    reopened = ComputeSha256(verified);
                }
                catch
                {
                    verified.Dispose();
                    throw;
                }

                if (!string.Equals(reopened, update.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error($"Update digest changed after download (expected {update.Sha256}, got {reopened}). Refusing to install.");
                    verified.Dispose();
                    TryDelete(installerPath);
                    return null;
                }

                _logger.Info($"Update digest verified ({reopened}).");
                return verified;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Update download was cancelled.");
                return null;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"Update download failed: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"Update download/verify failed: {ex.Message}");
                TryDelete(installerPath);
                return null;
            }
        }

        /// <summary>"sha256:abc123" (GitHub's asset digest format) to "abc123".</summary>
        public static string ParseDigest(string digest)
        {
            if (string.IsNullOrWhiteSpace(digest)) return null;

            var value = digest.Trim();
            const string prefix = "sha256:";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(prefix.Length);

            return value.Length == 0 ? null : value.ToLowerInvariant();
        }

        public static string ComputeSha256(Stream stream)
        {
            if (stream.CanSeek) stream.Position = 0;
            using (var sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(stream));
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best effort */ }
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "WandEnhancer-AutoUpdate");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            return client;
        }
    }
}
