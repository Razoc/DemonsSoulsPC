using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DemonsSoulsPC.App.Services
{
    // What the game set up is, written by GameSetup next to the engine (Build\Engine\dspc-game.json).
    public class GameFeatures
    {
        public string TitleId { get; set; } = "";
        public string Version { get; set; } = "";
        public string Dump { get; set; } = "";
        // Kept for files of 0.1.x (the relinker's --to-intel conversion); the 0.2 engine needs none.
        public bool IntelConversion { get; set; }
        public DateTime SetupAt { get; set; }

        public const string FileName = "dspc-game.json";
        // The versions the engine is tested with (Demon's Souls PPSA01341: 1.07 and 1.05).
        public const string TestedVersion = "01.007.000";
        public static readonly string[] TestedVersions = { "01.007.000", "01.005.000" };

        // The engine's game patches (idle wait, memory, texture streaming, render size...) only find their code in
        // 01.005 and 01.007: other versions run without them, far slower and with more stutter, so they are refused.
        public static bool IsSupportedVersion(string? version) => TestedVersions.Contains(version ?? "");

        // Null when the dump's version is supported, else the message to show.
        public static string? UnsupportedVersionMessage(string dump)
        {
            string version;
            try { version = GameSetup.ReadParam(dump).Version; } catch { version = ""; }
            if (IsSupportedVersion(version)) return null;
            return string.IsNullOrEmpty(version)
                ? Loc.T("The game version could not be read (sce_sys/param.json). Demon’s Souls PC needs version 01.005 or 01.007 of the game (01.007 recommended).")
                : Loc.T("Game version {0} is not supported. Demon’s Souls PC needs version 01.005 or 01.007 of the game (01.007 recommended): update your dump with the 01.007 patch, then choose it again.", version);
        }

        public static void EnsureSupportedVersion(string dump)
        {
            var message = UnsupportedVersionMessage(dump);
            if (message != null) throw new UnsupportedGameVersionException(message);
        }

        public static GameFeatures? Load(string engineDir)
        {
            try
            {
                var path = Path.Combine(engineDir, FileName);
                return File.Exists(path) ? JsonSerializer.Deserialize<GameFeatures>(File.ReadAllText(path)) : null;
            }
            catch
            {
                return null;
            }
        }
    }

    // The game version refusal: shown in a message box even by the windowless --prepare and --play.
    public class UnsupportedGameVersionException : InvalidOperationException
    {
        public UnsupportedGameVersionException(string message) : base(message) { }
    }

    // Since 0.2 the game runs on the KytyPS5 engine tuned for Demon's Souls (chenxiao07/KytyPS5, Build\Engine), which
    // reads the user's own decrypted dump where it is: nothing is converted, copied or written into the dump.
    // The setup checks the dump, brings the saves of 0.1.x (Build\RunHybrid\_sd) over to the engine's _SaveData,
    // removes the 0.1.x game build and remembers the dump. The engine prepares its shaders by itself on the first
    // launch; the full shader precompile (about an hour) is PrecompileAsync.
    public class GameSetup
    {
        public const string GameExe = "kyty_emulator.exe";
        public const string RunScript = "run-windows.ps1";
        public const string PrecompileScript = "precompile-windows.ps1";

        private readonly string _root;
        private readonly string _engine;

        public GameSetup(string rootDirectory)
        {
            _root = rootDirectory;
            _engine = EngineDirectory(rootDirectory);
        }

        public static string EngineDirectory(string root) => Path.Combine(root, "Build", "Engine");

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

        public bool IsReleaseLayout =>
            File.Exists(Path.Combine(_engine, GameExe)) && File.Exists(Path.Combine(_engine, RunScript));

        public async Task<GameFeatures> RunAsync(string dump, IProgress<(int Percent, string Message)>? progress = null)
        {
            return await Task.Run(() => Run(dump, progress));
        }

        private GameFeatures Run(string dump, IProgress<(int Percent, string Message)>? progress)
        {
            void Report(int percent, string message) => progress?.Report((percent, message));
            if (!IsReleaseLayout)
                throw new InvalidOperationException(Loc.T("The runtime files are missing (Build\\Engine\\kyty_emulator.exe). Install Demon’s Souls PC again."));
            if (!File.Exists(Path.Combine(dump, "eboot.bin")))
                throw new InvalidOperationException(Loc.T("eboot.bin not found in the game folder."));

            var (titleId, version) = ReadParam(dump);
            var features = new GameFeatures { TitleId = titleId, Version = version, Dump = dump, SetupAt = DateTime.Now };
            var logPath = Path.Combine(_root, "Logs", "setup.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            void Log(string line) => AppendLog(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}\r\n");
            Log($"setup from {dump}: {titleId} {version}, engine {EngineVersion(_root)}");
            if (!GameFeatures.IsSupportedVersion(version))
            {
                Log($"refused: version {version} is not supported ({string.Join(", ", GameFeatures.TestedVersions)})");
                GameFeatures.EnsureSupportedVersion(dump);
            }

            Report(20, Loc.T("Bringing your saves over..."));
            MigrateSaves(titleId, Log);

            Report(60, Loc.T("Removing the files of the previous version..."));
            RemoveOldBuild(Log);

            File.WriteAllText(Path.Combine(_engine, GameFeatures.FileName),
                JsonSerializer.Serialize(features, new JsonSerializerOptions { WriteIndented = true }));
            InstallationManager.SaveManifest(_root,
                InstallationManager.CreateManifest(_root, dump, version, titleId, features.SetupAt));
            Report(100, Loc.T("Done."));
            return features;
        }

        // 0.1.x kept the saves in Build\RunHybrid\_sd (SAVEDATA0PlayerProfile0\USR-DATA...); the engine reads the same
        // folders from _SaveData\<title>. Copied once, never over a save the engine already has; the old folder
        // is kept as Build\saves-0.1 (a backup).
        private void MigrateSaves(string titleId, Action<string> log)
        {
            var old = Path.Combine(_root, "Build", "RunHybrid", "_sd");
            if (!Directory.Exists(old)) return;
            var target = Path.Combine(_engine, "_SaveData", string.IsNullOrEmpty(titleId) ? "PPSA01341" : titleId);
            Directory.CreateDirectory(target);
            foreach (var slot in Directory.GetDirectories(old, "SAVEDATA*"))
            {
                var dest = Path.Combine(target, Path.GetFileName(slot));
                if (Directory.Exists(dest)) { log($"save {Path.GetFileName(slot)}: already in _SaveData, kept"); continue; }
                CopyDirectory(slot, dest);
                log($"save {Path.GetFileName(slot)}: copied from 0.1.x");
            }
            var backup = Path.Combine(_root, "Build", "saves-0.1");
            if (!Directory.Exists(backup))
            {
                CopyDirectory(old, backup);
                log($"0.1.x saves backed up in {backup}");
            }
        }

        // The 0.1.x game build (converted game, AnyPS5 libraries, relinker): about 200 MB plus its shader caches.
        private void RemoveOldBuild(Action<string> log)
        {
            // licenses: the 0.1.x runtime's (NVIDIA, AMD, fonts); the engine's are in Build\Engine\LICENSE(S).
            foreach (var dir in new[] { Path.Combine(_root, "Build", "RunHybrid"), Path.Combine(_root, "tools"), Path.Combine(_root, "licenses") })
            {
                if (!Directory.Exists(dir)) continue;
                try { DeleteTree(dir); log($"removed {dir}"); }
                catch (Exception ex) { log($"could not remove {dir}: {ex.Message}"); }
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }

        public static string EngineVersion(string root)
        {
            try { return File.ReadAllText(Path.Combine(EngineDirectory(root), "engine-version.txt")).Trim(); }
            catch { return ""; }
        }

        // Done once per engine and graphics driver: _PipelineCache\dspc-precompiled.txt holds the engine version and the
        // graphics cards' drivers the full precompile last finished for (the cache is the driver's: a new driver
        // compiles again). Missing or different: the next Play prepares the shaders first (ShaderPrepWindow).
        private const string PrecompileMarker = "dspc-precompiled.txt";

        private static string PrecompileMarkerPath(string root) => Path.Combine(EngineDirectory(root), "_PipelineCache", PrecompileMarker);

        public static string PrecompileKey(string root)
        {
            var drivers = new List<string>();
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController");
                foreach (var gpu in searcher.Get())
                {
                    var name = $"{gpu["Name"]}";
                    // Virtual displays (Parsec, Meta, remote desktop...) come and go: not the card the game runs on.
                    if (Regex.IsMatch(name, "Virtual|Parsec|Remote|Basic Display|Meta |SudoMaker|Indirect", RegexOptions.IgnoreCase)) continue;
                    drivers.Add($"{name} {gpu["DriverVersion"]}".Trim());
                }
            }
            catch { }
            // An NVIDIA card is the one the engine picks (the first discrete one): a processor's graphics beside it do not count.
            if (drivers.Any(d => d.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0))
                drivers.RemoveAll(d => d.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) < 0);
            drivers.Sort(StringComparer.Ordinal);
            // "full+translation": a marker of 0.2.0's quick run (-Quick) does not count any more (players' reports: a quarter
            // of the pipelines play met were missing, compiled in play with freezes of up to 18 s), nor one without the
            // engine's shader translation (PrecompileAsync's second step).
            return EngineVersion(root) + " | " + string.Join("; ", drivers) + " | full+translation";
        }

        public static bool PrecompileNeeded(string root)
        {
            try
            {
                if (!File.Exists(Path.Combine(EngineDirectory(root), PrecompileScript))) return false;
                var marker = PrecompileMarkerPath(root);
                return !File.Exists(marker) || File.ReadAllText(marker).Trim() != PrecompileKey(root);
            }
            catch { return false; }
        }

        public static void MarkPrecompiled(string root)
        {
            try
            {
                var marker = PrecompileMarkerPath(root);
                Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
                File.WriteAllText(marker, PrecompileKey(root) + Environment.NewLine);
            }
            catch { }
        }

        // The full shader precompile (the engine's precompile-windows.ps1, run hidden): every shader and pipeline of
        // the game compiled once for this graphics card, so nothing stutters the first time it appears. About an hour
        // with the CPU fully loaded; stopping it keeps what is done and the next run continues. Progress from its
        // "shards: 120 of 1096 done (10%)" lines; output in Logs\precompile.log.
        public static async Task<int> PrecompileAsync(string root, string dump, IProgress<(int Percent, string Message)>? progress,
            System.Threading.CancellationToken cancel = default)
        {
            var engine = EngineDirectory(root);
            var logs = Path.Combine(root, "Logs");
            Directory.CreateDirectory(logs);
            // Always the full run: every shader and pipeline of the game. (0.2.0's -Quick left about a quarter of the
            // pipelines play met, compiled in play with freezes of up to 18 s in players' reports.)
            var arguments = new List<string> { "-Game", dump };
            var psi = PowerShell(Path.Combine(engine, PrecompileScript), arguments, engine);
            using var log = new StreamWriter(Path.Combine(logs, "precompile.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            using var process = new Process { StartInfo = psi };
            var shards = new Regex(@"shards:\s*(\d+)\s+of\s+(\d+)\s+done\s*\((\d+)%\)");
            void Line(string? text)
            {
                if (text == null) return;
                lock (log) log.WriteLine(text);
                var match = shards.Match(text);
                if (match.Success)
                    progress?.Report((int.Parse(match.Groups[3].Value), string.Format(Loc.T("Precompiling shaders: {0} of {1}"), match.Groups[1].Value, match.Groups[2].Value)));
                else if (text.TrimStart().StartsWith("seeds:", StringComparison.OrdinalIgnoreCase) || text.IndexOf(" planning:", StringComparison.OrdinalIgnoreCase) >= 0)
                    progress?.Report((-1, Loc.T("Prep.Planning")));
                else if (text.IndexOf(" merging the shards", StringComparison.OrdinalIgnoreCase) >= 0)
                    progress?.Report((-1, Loc.T("Prep.Merging")));
                else if (text.IndexOf("ready to play", StringComparison.OrdinalIgnoreCase) >= 0)
                    progress?.Report((-1, Loc.T("Prep.CommonReady")));
            }
            process.OutputDataReceived += (s, e) => Line(e.Data);
            process.ErrorDataReceived += (s, e) => Line(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            using (cancel.Register(() => KillTree(process)))
                await Task.Run(() => process.WaitForExit());
            lock (log) log.Dispose(); // the translation step appends to the same file
            if (process.ExitCode != 0 || cancel.IsCancellationRequested) return process.ExitCode;

            // Then the engine's shader translation (what showed "Preparing shaders" before or during play): done here
            // too, so the game starts with nothing left to prepare.
            bool translated;
            try { translated = await new LauncherService(root).PrefetchShadersAsync(progress, cancel); }
            catch (Exception ex) { AppendLog(Path.Combine(logs, "precompile.log"), "translation: " + ex + "\r\n"); translated = false; }
            if (cancel.IsCancellationRequested) return process.ExitCode;
            if (!translated) return TranslationFailed;
            MarkPrecompiled(root);
            return 0;
        }

        // PrecompileAsync's code when the pipelines were compiled but the engine's shader translation did not finish.
        public const int TranslationFailed = 90;

        // Windows PowerShell running one of the engine's scripts without a window (the user never opens a script).
        public static ProcessStartInfo PowerShell(string script, IEnumerable<string> arguments, string workingDirectory)
        {
            var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var exe = Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(exe)) exe = "powershell.exe";
            var all = new List<string> { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script };
            all.AddRange(arguments);
            return new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.Join(" ", all.Select(Quote)),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
        }

        public static string Quote(string a) =>
            a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"', ';', '&', ',' }) < 0 ? a : "\"" + a.Replace("\"", "\\\"") + "\"";

        // The script and what it started (the engine, the shader tool).
        public static void KillTree(Process process)
        {
            try
            {
                using var kill = Process.Start(new ProcessStartInfo("taskkill", $"/T /F /PID {process.Id}") { CreateNoWindow = true, UseShellExecute = false });
                kill?.WaitForExit(5000);
            }
            catch { }
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
    }
}
