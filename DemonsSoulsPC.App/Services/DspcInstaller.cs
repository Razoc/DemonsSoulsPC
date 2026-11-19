using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DemonsSoulsPC.App.Services
{
    // First-run install and runtime updates: downloads the pinned Demon’s Souls PC runtime, checks its SHA-256,
    // extracts it into the install folder (without the upstream launcher), copies DemonsSoulsPC.exe there and makes
    // the shortcuts. Never touches the game dump.
    public class DspcInstaller
    {
        public static async Task InstallRuntimeAsync(string installDir, IProgress<(int Percent, string Message)> progress, CancellationToken cancel)
        {
            Directory.CreateDirectory(installDir);
            var zip = Path.Combine(Path.GetTempPath(), $"dspc-runtime-{DspcInfo.RuntimeVersion}.zip");
            // DSPC_RUNTIME_ZIP=<local zip>: the same package from disk instead of GitHub (tests before a release);
            // it must still match the pinned SHA-256.
            var local = Environment.GetEnvironmentVariable("DSPC_RUNTIME_ZIP");
            try
            {
                if (!string.IsNullOrEmpty(local) && File.Exists(local))
                    await Task.Run(() => File.Copy(local, zip, true), cancel);
                else
                    await DownloadAsync(DspcInfo.RuntimeUrl, zip, DspcInfo.RuntimeZipBytes, progress, 0, 70, cancel);

                progress.Report((72, Loc.T("Install.Verifying")));
                var hash = await Task.Run(() => Sha256(zip), cancel);
                if (!hash.Equals(DspcInfo.RuntimeSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(Loc.T("Install.HashMismatch"));

                progress.Report((75, Loc.T("Install.Extracting")));
                await Task.Run(() => Extract(zip, installDir, progress), cancel);
                File.WriteAllText(Path.Combine(installDir, DspcInfo.RuntimeVersionFile), DspcInfo.RuntimeVersion);
            }
            finally
            {
                try { File.Delete(zip); } catch { }
            }
        }

        public static async Task DownloadAsync(string url, string destination, long expectedBytes,
            IProgress<(int Percent, string Message)>? progress, int from, int to, CancellationToken cancel)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DemonsSoulsPC/" + DspcInfo.CurrentVersionText);
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? expectedBytes;
            using var input = await response.Content.ReadAsStreamAsync();
            using var output = File.Create(destination);
            var buffer = new byte[1 << 16];
            long read = 0; int n; int lastPercent = -1;
            while ((n = await input.ReadAsync(buffer, 0, buffer.Length, cancel)) > 0)
            {
                await output.WriteAsync(buffer, 0, n, cancel);
                read += n;
                if (progress != null && total > 0)
                {
                    var percent = from + (int)((to - from) * Math.Min(1.0, (double)read / total));
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress.Report((percent, Loc.T("Install.Downloading", DspcInfo.FormatBytes(read), DspcInfo.FormatBytes(total))));
                    }
                }
            }
        }

        private static void Extract(string zip, string installDir, IProgress<(int Percent, string Message)> progress)
        {
            var root = Path.GetFullPath(installDir).TrimEnd('\\') + "\\";
            using var archive = ZipFile.OpenRead(zip);
            int done = 0, count = archive.Entries.Count;
            foreach (var entry in archive.Entries)
            {
                done++;
                if (DspcInfo.IsUpstreamLauncherFile(entry.FullName)) continue;
                var target = Path.GetFullPath(Path.Combine(installDir, entry.FullName));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    continue; // path traversal guard
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, true);
                if (done % 50 == 0)
                    progress.Report((75 + 20 * done / Math.Max(1, count), Loc.T("Install.Extracting")));
            }
        }

        public static string Sha256(string file)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(file);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        // Copies the running DemonsSoulsPC.exe into the install folder (unless it already runs from there).
        public static string CopySelf(string installDir)
        {
            var self = Assembly.GetExecutingAssembly().Location;
            var target = Path.Combine(installDir, DspcInfo.ExeName);
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, target, true);
            return target;
        }

        public static void CreateShortcuts(string exe)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "DemonsSoulsPC");
            Directory.CreateDirectory(startMenu);
            CreateShortcut(Path.Combine(desktop, "Demon’s Souls PC.lnk"), exe);
            CreateShortcut(Path.Combine(startMenu, "Demon’s Souls PC.lnk"), exe);
        }

        // WScript.Shell through late binding: no extra reference needed.
        private static void CreateShortcut(string lnk, string exe)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            var shell = Activator.CreateInstance(shellType);
            var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            var t = shortcut!.GetType();
            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(exe)! });
            t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { exe + ",0" });
            t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Demon’s Souls PC" });
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        // Writable, not OneDrive, enough space. Returns an error text or null.
        public static string? CheckFolder(string dir)
        {
            try
            {
                var full = Path.GetFullPath(dir);
                if (full.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0)
                    return Loc.T("Install.OneDrive");
                Directory.CreateDirectory(full);
                var probe = Path.Combine(full, ".dspc-write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return null;
            }
            catch (Exception ex)
            {
                return Loc.T("Install.BadFolder", ex.Message);
            }
        }

        public static long FreeSpace(string dir)
        {
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(dir))!).AvailableFreeSpace; }
            catch { return -1; }
        }
    }
}
