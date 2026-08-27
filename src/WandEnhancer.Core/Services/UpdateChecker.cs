using System;
using System.Net.Http;
using System.Reflection;
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
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        var name = asset["name"]?.ToString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            downloadUrl = asset["browser_download_url"]?.ToString();
                            break;
                        }
                    }
                }

                return new UpdateInfo
                {
                    LatestVersion = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = release["body"]?.ToString(),
                    TagName = tagName
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

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "WandEnhancer-AutoUpdate");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            return client;
        }
    }
}
