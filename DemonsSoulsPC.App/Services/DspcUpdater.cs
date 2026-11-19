using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DemonsSoulsPC.App.Services
{
    // Self-update from the latest GitHub release of Razoc/DemonsSoulsPC: the release must carry an DemonsSoulsPC.exe asset,
    // and GitHub's own SHA-256 digest of that asset is required and checked. The running exe is renamed to
    // DemonsSoulsPC.exe.old (Windows allows renaming a running exe), the new one takes its name, and Demon’s Souls PC restarts.
    public static class DspcUpdater
    {
        public class Release
        {
            public Version Version { get; set; } = new Version(0, 0, 0);
            public string Tag { get; set; } = "";
            public string Notes { get; set; } = "";
            public string Url { get; set; } = "";
            public string? Sha256 { get; set; }
            public long Size { get; set; }
            public string VersionText => $"{Version.Major}.{Version.Minor}.{Version.Build}";
        }

        public static async Task<Release?> CheckAsync(CancellationToken cancel = default)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DemonsSoulsPC/" + DspcInfo.CurrentVersionText);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.GetAsync(DspcInfo.LatestReleaseApi, cancel);
            if (!response.IsSuccessStatusCode) return null; // no release yet, offline, rate limited
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;
            if (version.Build < 0) version = new Version(version.Major, version.Minor, 0);
            if (version <= Normalize(DspcInfo.CurrentVersion)) return null;

            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                if (!string.Equals(asset.GetProperty("name").GetString(), DspcInfo.LauncherAssetName, StringComparison.OrdinalIgnoreCase))
                    continue;
                string? digest = asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
                return new Release
                {
                    Version = version,
                    Tag = tag,
                    Notes = root.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() ?? "" : "",
                    Url = asset.GetProperty("browser_download_url").GetString() ?? "",
                    Sha256 = digest != null && digest.StartsWith("sha256:") ? digest.Substring(7) : null,
                    Size = asset.GetProperty("size").GetInt64(),
                };
            }
            return null;
        }

        private static Version Normalize(Version v) => new Version(v.Major, v.Minor, Math.Max(0, v.Build));

        public static async Task InstallAndRestartAsync(Release release, IProgress<(int Percent, string Message)>? progress)
        {
            if (string.IsNullOrEmpty(release.Sha256))
                throw new InvalidOperationException(Loc.T("Update.NoDigest"));
            var current = Assembly.GetExecutingAssembly().Location;
            var dir = Path.GetDirectoryName(current)!;
            var fresh = Path.Combine(dir, DspcInfo.ExeName + ".new");
            var old = Path.Combine(dir, DspcInfo.ExeName + ".old");

            await DspcInstaller.DownloadAsync(release.Url, fresh, release.Size, progress, 0, 95, CancellationToken.None);
            if (!DspcInstaller.Sha256(fresh).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(fresh);
                throw new InvalidOperationException(Loc.T("Install.HashMismatch"));
            }

            if (File.Exists(old)) File.Delete(old);
            File.Move(current, old);
            try
            {
                File.Move(fresh, current);
            }
            catch
            {
                File.Move(old, current); // put the working exe back
                throw;
            }
            Process.Start(new ProcessStartInfo(current) { UseShellExecute = true, WorkingDirectory = dir });
            System.Windows.Application.Current.Shutdown(0);
        }

        // Left over by the previous update.
        public static void CleanUpOldExe()
        {
            try
            {
                var old = Assembly.GetExecutingAssembly().Location + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch { }
        }
    }
}
