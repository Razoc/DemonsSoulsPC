using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using DemonsSoulsPC.App.Models;

namespace DemonsSoulsPC.App.Services
{
    // Logs\dspc-summary.log: one short file (in French, for the developer) that says what a player's last session looked
    // like: the PC, the versions, the settings, the shaders' state, the frame rate (read every second from the game
    // window's title, "fps: 60"), the problems the engine logged and how the game ended. Rewritten every 30 seconds and
    // at the end, so it is there even when the launcher is killed with the game. At the end, Logs\engine-last.log gets
    // the useful part of the engine's own log (Build\Engine\logs, outside the report), and old logs are cleared.
    // A RecompLauncher review sends ONE file only, a .log of the Logs folder (never a .txt: 0.2.0's dspc-summary.txt was
    // never sent; players' reports came with engine-last.log or game-run.log, and only their end, ~2 MB). So the
    // summary is a .log, the final one is also appended at the end of engine-last.log and game-run.log (the part a
    // report keeps), and dspc-summary.log is written last (the newest .log), whichever file the review picks.
    public sealed class SessionReport : IDisposable
    {
        public const string FileName = "dspc-summary.log";
        private const string OldFileName = "dspc-summary.txt";
        public const string EngineCopy = "engine-last.log";

        private static readonly Regex FpsInTitle = new Regex(@"fps:\s*(\d+)", RegexOptions.Compiled);
        private static readonly Regex EngineLogs = new Regex(@"logs:\s*(\S+)\.\*\.log", RegexOptions.Compiled);
        private static readonly Regex Problem = new Regex(@"\b(error|failed|failure|fatal|exception|crash|device lost|out of memory|assert|abort)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // A count of none is no problem: "0 out of memory", "0 save failures", "0 failed".
        private static readonly Regex NoneCounted = new Regex(@"\b0 (\w+ ){0,3}?(failures?|failed|errors?|out of memory|exceptions?|crash(es)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Numbers = new Regex(@"0x[0-9a-fA-F]+|\d+(\.\d+)?", RegexOptions.Compiled);

        private readonly string _root;
        private readonly string _summaryPath;
        private readonly AppSettings? _settings;
        private readonly string _command;
        private readonly DateTime _started = DateTime.Now;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly object _lock = new object();
        private readonly List<(double Seconds, int Fps)> _samples = new List<(double, int)>();
        private readonly string _machine;
        private readonly Timer _sampler;
        private readonly Timer _writer;
        private string? _engineLogStem;
        private int? _exitCode;

        public SessionReport(string root, AppSettings? settings, string command)
        {
            _root = root;
            _settings = settings;
            _command = Anonymize(command);
            var logs = Path.Combine(root, "Logs");
            Directory.CreateDirectory(logs);
            _summaryPath = Path.Combine(logs, FileName);
            try { CleanOldLogs(root); } catch { }
            _machine = Machine();
            Write();
            _sampler = new Timer(_ => Sample(), null, 1000, 1000);
            _writer = new Timer(_ => Write(), null, 30000, 30000);
        }

        // A line of the engine script's output (game-run.log): it names the engine's log files.
        public void Output(string line)
        {
            var match = EngineLogs.Match(line);
            if (match.Success) lock (_lock) _engineLogStem = match.Groups[1].Value;
        }

        public void Finish(int exitCode)
        {
            lock (_lock) _exitCode = exitCode;
            _sampler.Change(Timeout.Infinite, Timeout.Infinite);
            _writer.Change(Timeout.Infinite, Timeout.Infinite);
            string text;
            lock (_lock) text = Build();
            var logs = Path.Combine(_root, "Logs");
            bool engineCopied = false;
            try { engineCopied = CopyEngineLog(); } catch { }
            try { TrimLog(Path.Combine(logs, "game-run.log"), 300 * 1024); } catch { }
            // The summary at the end of both logs a review may send (their end is what it keeps), then on its own, last.
            if (engineCopied) try { AppendSummary(Path.Combine(logs, EngineCopy), text); } catch { }
            try { AppendSummary(Path.Combine(logs, "game-run.log"), text); } catch { }
            try { File.WriteAllText(_summaryPath, text, new UTF8Encoding(true)); } catch { }
        }

        private static void AppendSummary(string path, string summary)
        {
            if (!File.Exists(path)) return;
            File.AppendAllText(path, Environment.NewLine + "==================== " + FileName + " (résumé de la partie) ====================" + Environment.NewLine
                + summary, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            _sampler.Dispose();
            _writer.Dispose();
        }

        private void Sample()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameSetup.GameExe)))
                {
                    var match = FpsInTitle.Match(p.MainWindowTitle ?? "");
                    if (!match.Success) continue;
                    lock (_lock) _samples.Add((_clock.Elapsed.TotalSeconds, int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)));
                    return;
                }
            }
            catch { }
        }

        private void Write()
        {
            try
            {
                string text;
                lock (_lock) text = Build();
                File.WriteAllText(_summaryPath, text, new UTF8Encoding(true));
            }
            catch { }
        }

        private string Build()
        {
            var s = new StringBuilder();
            s.AppendLine("Demon's Souls PC — résumé de la dernière partie");
            s.AppendLine("================================================");
            s.AppendLine($"Début : {_started:yyyy-MM-dd HH:mm:ss}, durée : {Duration(_clock.Elapsed)}"
                + (_exitCode == null ? " (en cours)" : $", fin : code {_exitCode} (0x{_exitCode:X8}) — {ExitMeaning(_exitCode.Value)}"));
            s.AppendLine();
            s.AppendLine("[Versions]");
            s.AppendLine($"Lanceur : {DspcInfo.CurrentVersionText}   Runtime : {DspcInfo.InstalledRuntimeVersion(_root)}   Moteur : {GameSetup.EngineVersion(_root)}");
            var game = GameFeatures.Load(GameSetup.EngineDirectory(_root));
            s.AppendLine($"Jeu : {game?.TitleId} {game?.Version}");
            s.AppendLine();
            s.AppendLine("[PC]");
            s.Append(_machine);
            s.AppendLine();
            s.AppendLine("[Réglages]");
            if (_settings != null)
                s.AppendLine($"Taille {_settings.Size}, FPS {_settings.Fps} (0=30, 1=60, 2=120), graphismes {_settings.Lighting} (0=standard), VSync {_settings.VSync}, plein écran {_settings.Fullscreen}, overlay {_settings.Overlay}, langue {_settings.Language}, priorité {_settings.Priority}");
            s.AppendLine("Commande : " + _command);
            s.AppendLine();
            s.AppendLine("[Shaders]");
            s.AppendLine(ShaderState());
            s.AppendLine();
            s.AppendLine("[Images/s, lues chaque seconde dans le titre de la fenêtre]");
            s.Append(FpsSummary());
            s.AppendLine();
            s.Append(EngineSection());
            return s.ToString();
        }

        private string FpsSummary()
        {
            if (_samples.Count == 0) return "Pas encore de mesure (la fenêtre du jeu n'a pas affiché de FPS).\n";
            // Loading screens and menus count too: the per-minute list shows where the frame rate fell.
            var values = _samples.Select(x => x.Fps).ToList();
            var sorted = values.OrderBy(v => v).ToList();
            double avg = values.Average();
            int low1 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.01))];
            int low5 = sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * 0.05))];
            var s = new StringBuilder();
            s.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} s mesurées : moyenne {1:0.0}, médiane {2}, 5 % bas {3}, 1 % bas {4}, minimum {5}, maximum {6}",
                values.Count, avg, sorted[sorted.Count / 2], low5, low1, sorted[0], sorted[sorted.Count - 1]));
            s.AppendLine($"Secondes sous 50 FPS : {values.Count(v => v < 50)}, sous 30 : {values.Count(v => v < 30)}, sous 10 : {values.Count(v => v < 10)}");
            s.AppendLine("Par minute (moyenne / minimum) :");
            var minutes = _samples.GroupBy(x => (int)(x.Seconds / 60)).OrderBy(g => g.Key).ToList();
            foreach (var minute in minutes.Skip(Math.Max(0, minutes.Count - 90)))
                s.AppendLine($"  {minute.Key,3} min : {minute.Average(x => x.Fps),5:0.0} / {minute.Min(x => x.Fps),3}");
            return s.ToString();
        }

        private string ShaderState()
        {
            var engine = GameSetup.EngineDirectory(_root);
            var marker = Path.Combine(engine, "_PipelineCache", "dspc-precompiled.txt");
            var lines = new List<string>
            {
                "Précompilation : " + (File.Exists(marker) ? "faite (" + SafeRead(marker).Trim() + ")" : "pas faite"),
            };
            var stat = Path.Combine(engine, "_PipelineCache", "static");
            if (Directory.Exists(stat))
                foreach (var f in new DirectoryInfo(stat).GetFiles().Where(f => f.Length > 1024 * 1024))
                    lines.Add($"  {f.Name} : {f.Length / (1024 * 1024)} Mo");
            return string.Join(Environment.NewLine, lines);
        }

        // From the engine's own log: its start-up facts, the problems it logged (grouped) and its last lines.
        private string EngineSection()
        {
            var stem = _engineLogStem;
            if (stem == null) return "[Moteur]\nJournal du moteur pas encore connu.\n";
            var engine = GameSetup.EngineDirectory(_root);
            var outPath = Path.Combine(engine, stem + ".out.log");
            var errPath = Path.Combine(engine, stem + ".err.log");
            var lines = ReadShared(outPath);
            var s = new StringBuilder();
            s.AppendLine($"[Moteur] journal : Build\\Engine\\{stem}.out.log ({lines.Count} lignes ; l'essentiel est copié dans Logs\\{EngineCopy} à la fin)");
            foreach (var line in lines.Where(l => l.StartsWith("PC memory:") || l.StartsWith("Vulkan GPU in use") || l.StartsWith("Static pipeline binaries")
                         || l.StartsWith("Shader prefetch cache: ") && l.Contains("translated before") || l.StartsWith("Shader prefetch wait: done")
                         || l.StartsWith("Pipeline warmup") || l.StartsWith("Presents limited")).Take(12))
                s.AppendLine("  " + Anonymize(line));
            var groups = new Dictionary<string, (string Example, int Count)>();
            foreach (var line in lines.Concat(ReadShared(errPath)).Where(l => Problem.IsMatch(NoneCounted.Replace(l, ""))))
            {
                var key = Numbers.Replace(line, "#");
                if (key.Length > 160) key = key.Substring(0, 160);
                groups[key] = groups.TryGetValue(key, out var g) ? (g.Example, g.Count + 1) : (line, 1);
                if (groups.Count > 300) break;
            }
            s.AppendLine($"Problèmes signalés par le moteur : {groups.Count} sortes");
            foreach (var g in groups.Values.OrderByDescending(g => g.Count).Take(40))
                s.AppendLine($"  {g.Count,6} × {Shorten(Anonymize(g.Example), 220)}");
            var err = ReadShared(errPath);
            if (err.Count > 0)
            {
                s.AppendLine("Fin du journal d'erreurs du moteur :");
                foreach (var line in err.Skip(Math.Max(0, err.Count - 40))) s.AppendLine("  " + Shorten(Anonymize(line), 300));
            }
            s.AppendLine("Dernières lignes du moteur :");
            foreach (var line in lines.Skip(Math.Max(0, lines.Count - 25))) s.AppendLine("  " + Shorten(Anonymize(line), 300));
            return s.ToString();
        }

        // False when this session has no engine log (the engine never started): engine-last.log is then an older session's.
        private bool CopyEngineLog()
        {
            if (_engineLogStem == null) return false;
            var engine = GameSetup.EngineDirectory(_root);
            var outLines = ReadShared(Path.Combine(engine, _engineLogStem + ".out.log"));
            var errLines = ReadShared(Path.Combine(engine, _engineLogStem + ".err.log"));
            var s = new StringBuilder();
            s.AppendLine($"== {_engineLogStem}.out.log : {outLines.Count} lignes (les 150 premières et les 1500 dernières) ==");
            foreach (var l in outLines.Take(150)) s.AppendLine(Anonymize(l));
            if (outLines.Count > 1650) s.AppendLine($"[... {outLines.Count - 1650} lignes omises ...]");
            foreach (var l in outLines.Skip(Math.Max(150, outLines.Count - 1500))) s.AppendLine(Anonymize(l));
            s.AppendLine($"== {_engineLogStem}.err.log : {errLines.Count} lignes (les 300 dernières) ==");
            foreach (var l in errLines.Skip(Math.Max(0, errLines.Count - 300))) s.AppendLine(Anonymize(l));
            var text = s.ToString();
            if (text.Length > 600 * 1024) text = text.Substring(text.Length - 600 * 1024);
            File.WriteAllText(Path.Combine(_root, "Logs", EngineCopy), text, new UTF8Encoding(true));
            return true;
        }

        // Keeps the report small: the engine's 10 last sessions and 2 last precompiles, no 0.1 logs, long logs cut.
        private static void CleanOldLogs(string root)
        {
            var engineLogs = Path.Combine(GameSetup.EngineDirectory(root), "logs");
            if (Directory.Exists(engineLogs))
            {
                var sessions = Directory.GetFiles(engineLogs, "*.out.log").Select(f => Path.GetFileName(f).Replace(".out.log", ""))
                    .OrderByDescending(n => n, StringComparer.Ordinal).Skip(10);
                foreach (var stem in sessions)
                    foreach (var ext in new[] { ".out.log", ".err.log" })
                        try { File.Delete(Path.Combine(engineLogs, stem + ext)); } catch { }
                foreach (var dir in Directory.GetDirectories(engineLogs, "*-precompile").OrderByDescending(d => d, StringComparer.Ordinal).Skip(2))
                    GameSetup.DeleteTree(dir);
            }
            var logs = Path.Combine(root, "Logs");
            foreach (var old in new[] { "hybrid-run.out.log", "hybrid-run.err.log", "setup-relinker.log", OldFileName })
                try { File.Delete(Path.Combine(logs, old)); } catch { }
            foreach (var log in new[] { "dspc-error.log", "dspc-debug.log", "setup.log" })
                try { TrimLog(Path.Combine(logs, log), 200 * 1024); } catch { }
        }

        private static void TrimLog(string path, int keep)
        {
            if (!File.Exists(path) || new FileInfo(path).Length <= keep) return;
            var bytes = File.ReadAllBytes(path);
            var text = Encoding.UTF8.GetString(bytes, bytes.Length - keep, keep);
            var cut = text.IndexOf('\n');
            File.WriteAllText(path, "[... début coupé ...]\n" + (cut >= 0 ? text.Substring(cut + 1) : text), new UTF8Encoding(true));
        }

        private static string Machine()
        {
            var s = new StringBuilder();
            try
            {
                using (var os = new ManagementObjectSearcher("SELECT Caption, Version, TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                    foreach (var o in os.Get())
                        s.AppendLine($"Windows : {o["Caption"]} {o["Version"]}, RAM : {Convert.ToInt64(o["TotalVisibleMemorySize"]) / (1024 * 1024.0):0.0} Go");
                using (var cpu = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor"))
                    foreach (var o in cpu.Get())
                        s.AppendLine($"Processeur : {o["Name"]?.ToString().Trim()} ({o["NumberOfCores"]} cœurs, {o["NumberOfLogicalProcessors"]} threads)");
                using (var gpu = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController"))
                    foreach (var o in gpu.Get())
                        s.AppendLine($"Carte graphique : {o["Name"]} (pilote {o["DriverVersion"]})");
            }
            catch (Exception ex) { s.AppendLine("PC : lecture impossible (" + ex.Message + ")"); }
            return s.ToString();
        }

        private static string ExitMeaning(int code)
        {
            switch (unchecked((uint)code))
            {
                case 0: return "fermeture normale";
                case 0xC0000005: return "plantage (accès mémoire)";
                case 0xC0000409: return "plantage (arrêt d'urgence)";
                case 0xC0000135: return "fichier DLL manquant";
                case 0xC00000FD: return "plantage (pile)";
                case 0xC0000017: case 0xE06D7363: return "plantage (mémoire / exception)";
                case 0xFFFFFFFF: return "arrêt forcé ou erreur";
                default: return "inattendu";
            }
        }

        private static List<string> ReadShared(string path)
        {
            var lines = new List<string>();
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string? line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            catch { }
            return lines;
        }

        private static string SafeRead(string path)
        {
            try { return File.ReadAllText(path); } catch { return ""; }
        }

        // The Windows user name is not the developer's business: C:\Users\<name> becomes C:\Users\<utilisateur>.
        private static readonly string Profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        private static string Anonymize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (!string.IsNullOrEmpty(Profile)) text = text.Replace(Profile, @"C:\Users\<utilisateur>");
            var user = Environment.UserName;
            if (user.Length >= 3) text = Regex.Replace(text, @"(?<=\\Users\\)" + Regex.Escape(user) + @"(?=\\|$)", "<utilisateur>", RegexOptions.IgnoreCase);
            return text;
        }

        private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max) + "…";

        private static string Duration(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00} min" : $"{t.Minutes} min {t.Seconds:00} s";
    }
}
