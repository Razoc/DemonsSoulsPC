using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DemonsSoulsPC.App.Services
{
    // What the game build is, written by GameSetup next to the game (Build\RunHybrid\dspc-game.json).
    public class GameFeatures
    {
        public string TitleId { get; set; } = "";
        public string Version { get; set; } = "";
        // The game code was converted for CPUs without AMD-only instructions (relinker --to-intel).
        public bool IntelConversion { get; set; }
        public DateTime SetupAt { get; set; }

        public const string FileName = "dspc-game.json";
        // The version Demon’s Souls PC is tested with (PPSA01341, Europe).
        public const string TestedVersion = "01.007.000";

        public static GameFeatures? Load(string runHybrid)
        {
            try
            {
                var path = Path.Combine(runHybrid, FileName);
                return File.Exists(path) ? JsonSerializer.Deserialize<GameFeatures>(File.ReadAllText(path)) : null;
            }
            catch
            {
                return null;
            }
        }
    }

    // Builds Build\RunHybrid's game files from the user's own decrypted dump with the shipped runtime:
    //  1. app0: a folder of junctions to the dump's folders plus hard links (or copies) of its top-level files, a
    //     real sce_module folder (copies of the original modules) and a real misc folder whose rendering configs
    //     (renderconfig_ps5_*.txt) are copies the launcher rewrites for the chosen quality; nothing is ever
    //     written into the dump;
    //  2. eboot.bin and sce_module\*.prx unwrapped from their SELF containers (SelfUnwrapper, no keys);
    //  3. AnyPS5's relinker (tools\relinker.exe) turns the game code into DemonsSouls.exe (it also writes the
    //     *.guest.prx modules into app0\sce_module).
    public class GameSetup
    {
        public const string GameExe = "DemonsSouls.exe";
        private const string RenderConfigPattern = "renderconfig_ps5_*.txt";
        // Hard links to the dump's original rendering configs, the source of the copies in app0\misc.
        private const string RenderConfigOriginals = "renderconfig-original";

        private readonly string _root;
        private readonly string _runHybrid;
        private readonly string _relinker;

        public GameSetup(string rootDirectory)
        {
            _root = rootDirectory;
            _runHybrid = Path.Combine(rootDirectory, "Build", "RunHybrid");
            _relinker = Path.Combine(rootDirectory, "tools", "relinker.exe");
        }

        // Logs are written from the setup thread and from progress handlers on the UI thread: one lock, a few
        // retries if another program holds the file, and never an exception (a lost log line must not stop a setup).
        private static readonly object LogLock = new object();

        public static void AppendLog(string path, string text)
        {
            lock (LogLock)
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try { File.AppendAllText(path, text); return; }
                    catch (IOException) { System.Threading.Thread.Sleep(20); }
                    catch { return; }
                }
            }
        }

        public bool IsReleaseLayout => File.Exists(_relinker) && File.Exists(Path.Combine(_runHybrid, "kyty_gpu.dll"));

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

        public async Task<GameFeatures> RunAsync(string dump, IProgress<(int Percent, string Message)>? progress = null)
        {
            return await Task.Run(() => Run(dump, progress));
        }

        private GameFeatures Run(string dump, IProgress<(int Percent, string Message)>? progress)
        {
            void Report(int percent, string message) => progress?.Report((percent, message));
            if (!IsReleaseLayout)
                throw new InvalidOperationException(Loc.T("The runtime files are missing (tools\\relinker.exe or Build\\RunHybrid\\kyty_gpu.dll). Extract the whole download again."));

            var (titleId, version) = ReadParam(dump);
            var features = new GameFeatures { TitleId = titleId, Version = version, SetupAt = DateTime.Now };
            var logPath = Path.Combine(_root, "Logs", "setup.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            void Log(string line) => AppendLog(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}\r\n");
            Log($"setup from {dump}: {titleId} {version}");

            Report(5, Loc.T("Linking the game folder (nothing is copied into or changed in your dump)..."));
            BuildApp0(dump, Path.Combine(_runHybrid, "app0"), Log);
            ApplyRenderConfig(_root, false);

            var work = Path.Combine(_root, "Build", "work");
            DeleteTree(work);
            Directory.CreateDirectory(Path.Combine(work, "sce_module"));
            try
            {
                Report(15, Loc.T("Unwrapping the game code (eboot.bin)..."));
                SelfUnwrapper.Unwrap(Path.Combine(dump, "eboot.bin"), Path.Combine(work, "eboot.elf"));
                foreach (var module in OriginalModules(dump))
                {
                    Report(25, $"Unwrapping {Path.GetFileName(module)}...");
                    SelfUnwrapper.Unwrap(module, Path.Combine(work, "sce_module", Path.GetFileName(module)));
                }

                Report(35, Loc.T("Converting the game code to a Windows program (about a minute)..."));
                var exe = Path.Combine(_runHybrid, GameExe);
                var stem = Path.GetFileNameWithoutExtension(GameExe);
                foreach (var old in Directory.GetFiles(_runHybrid, stem + "*.exe").Concat(Directory.GetFiles(_runHybrid, stem + "*.json")))
                    File.Delete(old);
                // CPUs other than AMD ones: --to-intel replaces AMD-only instructions (SSE4a EXTRQ, and VRSQRTPS /
                // VRCPPS whose results differ elsewhere) with equivalent code; an AMD CPU keeps the original code.
                features.IntelConversion = NeedsIntelConversion();
                Log($"CPU: {CpuVendor()}, Intel conversion: {features.IntelConversion}");
                var options = features.IntelConversion ? "--windows --registry --to-intel" : "--windows --registry";
                var (code, output) = RunProcess(_relinker, $"{options} \"{Path.Combine(work, "eboot.elf")}\" \"{exe}\"", _runHybrid);
                File.WriteAllText(Path.Combine(_root, "Logs", "setup-relinker.log"), output);
                if (code != 0 || !File.Exists(exe))
                    throw new InvalidOperationException($"The relinker failed (exit {code}); see Logs\\setup-relinker.log.");
                Log($"relinker: exit {code}, {new FileInfo(exe).Length} bytes");
                // Converted modules belong in app0\sce_module; the relinker may leave some beside the game.
                var modules = Path.Combine(_runHybrid, "app0", "sce_module");
                foreach (var guest in Directory.GetFiles(_runHybrid, "*.guest.prx"))
                {
                    File.Copy(guest, Path.Combine(modules, Path.GetFileName(guest)), true);
                    File.Delete(guest);
                }
                foreach (var guest in Directory.GetFiles(work, "*.guest.prx", SearchOption.AllDirectories))
                    File.Copy(guest, Path.Combine(modules, Path.GetFileName(guest)), true);
                Log($"app0\\sce_module: {Directory.GetFiles(modules, "*.guest.prx").Length} converted modules");
            }
            finally
            {
                DeleteTree(work);
            }

            File.WriteAllText(Path.Combine(_runHybrid, GameFeatures.FileName),
                JsonSerializer.Serialize(features, new JsonSerializerOptions { WriteIndented = true }));
            InstallationManager.SaveManifest(_root,
                InstallationManager.CreateManifest(_root, dump, version, titleId, features.SetupAt));
            Report(100, Loc.T("Done."));
            return features;
        }

        // Writes app0\misc\renderconfig_ps5_*.txt from the dump's originals: quality = the game's own settings (4K
        // Fidelity mode); otherwise the PS5 Performance mode settings in every file, with "+r_Wants4K = WantsPerf"
        // (no 4K rendering) and each file's own frame-rate target kept. Called by the setup and before each launch.
        public static void ApplyRenderConfig(string root, bool quality)
        {
            var runHybrid = Path.Combine(root, "Build", "RunHybrid");
            var originals = Path.Combine(runHybrid, RenderConfigOriginals);
            var misc = Path.Combine(runHybrid, "app0", "misc");
            if (!Directory.Exists(originals) || !Directory.Exists(misc)) return;
            var perfFile = Path.Combine(originals, "renderconfig_ps5_perf.txt");
            var perf = File.Exists(perfFile) ? File.ReadAllLines(perfFile) : null;
            foreach (var original in Directory.GetFiles(originals, RenderConfigPattern))
            {
                var target = Path.Combine(misc, Path.GetFileName(original));
                string[] lines;
                if (quality || perf == null)
                {
                    lines = File.ReadAllLines(original);
                }
                else
                {
                    var targetFps = File.ReadAllLines(original).Where(l => l.Contains("TargetFramesPerSecond"));
                    lines = perf.Where(l => !l.Contains("TargetFramesPerSecond"))
                        .Select(l => l.TrimStart().StartsWith("+r_Wants4K") ? "+r_Wants4K = WantsPerf" : l)
                        .Concat(targetFps).ToArray();
                }
                // A real file, never a hard link into the dump: delete first, then write.
                if (File.Exists(target)) { File.SetAttributes(target, FileAttributes.Normal); File.Delete(target); }
                File.WriteAllLines(target, lines, new UTF8Encoding(false));
            }
        }

        public static string CpuVendor()
        {
            try
            {
                return Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "VendorIdentifier", null) as string ?? "";
            }
            catch
            {
                return "";
            }
        }

        // DSPC_SETUP_TO_INTEL=1 / 0 forces the choice; otherwise every CPU that is not an AMD one.
        public static bool NeedsIntelConversion()
        {
            var forced = Environment.GetEnvironmentVariable("DSPC_SETUP_TO_INTEL");
            if (forced == "1") return true;
            if (forced == "0") return false;
            return CpuVendor().IndexOf("AMD", StringComparison.OrdinalIgnoreCase) < 0;
        }

        // Demon’s Souls (PS5 remake) in any region: the concept ID shared by its regional releases (10000368 for
        // PPSA01341), or the title name in param.json.
        public const string DemonsSoulsConceptId = "10000368";

        public static bool IsDemonsSouls(string dump)
        {
            try
            {
                var path = Path.Combine(dump, "sce_sys", "param.json");
                if (!File.Exists(path)) return false;
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                if (root.TryGetProperty("conceptId", out var concept) &&
                    (concept.ValueKind == JsonValueKind.Number ? concept.GetRawText() : concept.ValueKind == JsonValueKind.String ? concept.GetString() : "") == DemonsSoulsConceptId)
                    return true;
                if (root.TryGetProperty("localizedParameters", out var localized) && localized.ValueKind == JsonValueKind.Object)
                    foreach (var language in localized.EnumerateObject())
                        if (language.Value.ValueKind == JsonValueKind.Object && language.Value.TryGetProperty("titleName", out var name) &&
                            name.ValueKind == JsonValueKind.String &&
                            new string((name.GetString() ?? "").Where(char.IsLetter).ToArray()).ToUpperInvariant().Contains("DEMONSSOULS"))
                            return true;
            }
            catch { }
            return false;
        }

        public static (string TitleId, string Version) ReadParam(string dump)
        {
            var path = Path.Combine(dump, "sce_sys", "param.json");
            if (!File.Exists(path)) return ("", "");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            string Get(string name) => document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
            return (Get("titleId"), Get("contentVersion"));
        }

        private static IEnumerable<string> OriginalModules(string dump)
        {
            var folder = Path.Combine(dump, "sce_module");
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.prx").Where(f => !f.EndsWith(".guest.prx", StringComparison.OrdinalIgnoreCase))
                : Enumerable.Empty<string>();
        }

        private void BuildApp0(string dump, string app0, Action<string> log)
        {
            DeleteTree(app0);
            Directory.CreateDirectory(app0);
            foreach (var dir in Directory.GetDirectories(dump))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals("sce_module", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("misc", StringComparison.OrdinalIgnoreCase)) { BuildMisc(dir, Path.Combine(app0, name), log); continue; }
                Junction(dir, Path.Combine(app0, name), log);
            }
            foreach (var file in Directory.GetFiles(dump))
                LinkOrCopy(file, Path.Combine(app0, Path.GetFileName(file)));
            var modules = Path.Combine(app0, "sce_module");
            Directory.CreateDirectory(modules);
            foreach (var module in OriginalModules(dump))
                File.Copy(module, Path.Combine(modules, Path.GetFileName(module)), true);
            log($"app0: {Directory.GetDirectories(app0).Length} folders, {Directory.GetFiles(app0).Length} files");
        }

        // misc holds the rendering configs the launcher rewrites: a real folder with junctions for its subfolders,
        // hard links for its other files, and the original configs kept aside (hard links) in renderconfig-original.
        private void BuildMisc(string source, string misc, Action<string> log)
        {
            Directory.CreateDirectory(misc);
            foreach (var dir in Directory.GetDirectories(source))
                Junction(dir, Path.Combine(misc, Path.GetFileName(dir)), log);
            var originals = Path.Combine(_runHybrid, RenderConfigOriginals);
            DeleteTree(originals);
            Directory.CreateDirectory(originals);
            int configs = 0;
            foreach (var file in Directory.GetFiles(source))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("renderconfig_ps5_", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    LinkOrCopy(file, Path.Combine(originals, name));
                    configs++;
                }
                else
                {
                    LinkOrCopy(file, Path.Combine(misc, name));
                }
            }
            log($"app0\\misc: {configs} rendering configs kept aside for the quality setting");
        }

        private static void Junction(string target, string link, Action<string> log)
        {
            var folder = Path.GetDirectoryName(link)!;
            var (code, output) = RunProcess("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"", folder);
            if (code == 0) return;
            // Folder junctions cannot point to a network drive; a directory symbolic link can, when Windows allows
            // this user to make one (Developer Mode, or the launcher run as administrator).
            var (linkCode, _) = RunProcess("cmd.exe", $"/c mklink /D \"{link}\" \"{target}\"", folder);
            if (linkCode != 0)
            {
                throw new InvalidOperationException(
                    $"Could not link the dump's '{Path.GetFileName(target)}' folder ({output.Trim()}). If the dump is on a network drive, turn on Developer Mode " +
                    "(Windows Settings > System > For developers) or run Demon’s Souls PC once as administrator, then press Install again; " +
                    "or copy the dump to a local drive.");
            }
            log($"{Path.GetFileName(link)}: symbolic link (junction refused: {output.Trim()})");
        }

        private static void LinkOrCopy(string source, string target)
        {
            if (!CreateHardLink(target, source, IntPtr.Zero))
                File.Copy(source, target, true);
        }

        // Deletes a folder this setup made. Junctions are removed as links, never followed into the dump.
        public static void DeleteTree(string path)
        {
            if (!Directory.Exists(path)) return;
            var info = new DirectoryInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(path, false);
                return;
            }
            foreach (var dir in info.GetDirectories()) DeleteTree(dir.FullName);
            foreach (var file in info.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
                file.Delete();
            }
            Directory.Delete(path, false);
        }

        private static (int Code, string Output) RunProcess(string file, string arguments, string workingDirectory)
        {
            var psi = new ProcessStartInfo(file, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(psi)!;
            var output = new StringBuilder();
            process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return (process.ExitCode, output.ToString());
        }
    }
}
