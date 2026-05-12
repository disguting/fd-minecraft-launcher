using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace fd_launcher.Core
{
    public class GitHubRelease
    {
        public string? tag_name { get; set; }
        public string? name { get; set; }
        public string? html_url { get; set; }
        public GitHubAsset[]? assets { get; set; }
    }

    public class GitHubAsset
    {
        public string? name { get; set; }
        public string? browser_download_url { get; set; }
    }

    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string? LatestVersion { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ReleasePageUrl { get; set; }
    }

    public class UpdateService
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/papirosa1312/fd-minecraft-launcher/releases/latest";

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.UserAgent.ParseAdd("FD-Launcher-Updater");

            var json = await client.GetStringAsync(LatestReleaseApi);

            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release?.tag_name == null)
                return new UpdateInfo { IsUpdateAvailable = false };

            var currentVersion = GetCurrentVersion();
            var latestVersion = NormalizeVersion(release.tag_name);

            var isNewer = IsNewerVersion(latestVersion, currentVersion);

            var asset = release.assets?
                .FirstOrDefault(a =>
                    a.name != null &&
                    (
                        a.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        a.name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                        a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    ));

            return new UpdateInfo
            {
                IsUpdateAvailable = isNewer,
                LatestVersion = latestVersion,
                DownloadUrl = asset?.browser_download_url,
                ReleasePageUrl = release.html_url
            };
        }

        private string GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "0.0.0";
    }

    private string NormalizeVersion(string version)
    {
        return version
            .Replace("FD Launcher", "", StringComparison.OrdinalIgnoreCase)
            .Replace("v", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVersion) &&
            Version.TryParse(current, out var currentVersion))
        {
            return latestVersion > currentVersion;
        }

        return false;
    }

        public async Task DownloadAndOpenInstallerAsync(string downloadUrl)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FD-Launcher-Updater");

            var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            var bytes = await client.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(tempPath, bytes);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
    }
}