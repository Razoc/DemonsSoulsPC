using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace DemonsSoulsPC.App.Services
{
    // Everything Demon’s Souls PC-specific in one place: where it updates from, which runtime it installs, and the small
    // per-user file that remembers the install folder and the language.
    public static class DspcInfo
    {
        public const string Repo = "Razoc/DemonsSoulsPC";
        public const string RepoUrl = "https://github.com/" + Repo;
        public const string LatestReleaseApi = "https://api.github.com/repos/" + Repo + "/releases/latest";
        public const string LauncherAssetName = "DemonsSoulsPC.exe";
        public const string ExeName = "DemonsSoulsPC.exe";

        // The runtime (Build\Engine: the KytyPS5 engine tuned for Demon's Souls, chenxiao07/KytyPS5, GPL-2.0) is
        // Demon’s Souls PC's own package (Tools\release\make_runtime.ps1), pinned by version and SHA-256. A new
        // Demon’s Souls PC version can move the pin; installed copies then offer to update the runtime.
        public const string RuntimeVersion = "0.2.2";
        public const string RuntimeUrl = "https://github.com/" + Repo + "/releases/download/v0.2.2/DemonsSoulsPC-runtime-0.2.2.zip";
        public const string RuntimeSha256 = "07f3a7937150d017aa2fc3457183b9c61847a2f04d17b9ecd064c108528596d1";
        public const long RuntimeZipBytes = 55_058_441;
        public const long NeededBytes = 16L * 1024 * 1024 * 1024; // engine + shader caches (4.5-5.5 GB precompiled, 2 GB translated) + the precompile's ~12 GB at its end
        public const string RuntimeVersionFile = "dspc-runtime.txt";

        // Launcher files and scripts at the zip root: Demon’s Souls PC replaces them (it starts the game itself).
        public static bool IsUpstreamLauncherFile(string zipEntry)
        {
            if (zipEntry.Contains("/") || zipEntry.Contains("\\")) return false;
            var name = zipEntry.ToLowerInvariant();
            return name == "demonssoulspc.exe" || name == "demonssoulspc.exe.config" || name.EndsWith(".dll") ||
                name.EndsWith(".bat") || name.EndsWith(".cmd") || name.EndsWith(".sh");
        }

        public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        public static string CurrentVersionText => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

        public static string UserDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DemonsSoulsPC");

        public static string DefaultInstallDirectory()
        {
            var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
            return Path.Combine(root, "DemonsSoulsPC");
        }

        public static bool IsInstallFolder(string? dir) =>
            !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "Build", "Engine", "kyty_emulator.exe")) &&
            File.Exists(Path.Combine(dir, "Build", "Engine", "run-windows.ps1"));

        public static string InstalledRuntimeVersion(string installDir)
        {
            try { return File.ReadAllText(Path.Combine(installDir, RuntimeVersionFile)).Trim(); }
            catch { return ""; }
        }

        public class UserData
        {
            public string? InstallDir { get; set; }
            public string? Language { get; set; }
        }

        private static string UserDataFile => Path.Combine(UserDataDirectory, "dspc.json");

        public static UserData LoadUserData()
        {
            try
            {
                if (File.Exists(UserDataFile))
                    return JsonSerializer.Deserialize<UserData>(File.ReadAllText(UserDataFile)) ?? new UserData();
            }
            catch { }
            return new UserData();
        }

        public static void SaveUserData(UserData data)
        {
            Directory.CreateDirectory(UserDataDirectory);
            var tmp = UserDataFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(UserDataFile)) File.Delete(UserDataFile);
            File.Move(tmp, UserDataFile);
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = Loc.Language == Loc.French ? new[] { "o", "Ko", "Mo", "Go", "To" } : new[] { "B", "KB", "MB", "GB", "TB" };
            double size = bytes; int i = 0;
            while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
            return $"{size:0.#} {units[i]}";
        }
    }
}
