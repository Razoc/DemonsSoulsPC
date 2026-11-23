using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DemonsSoulsPC.App.Models;

namespace DemonsSoulsPC.App.Services
{
    public class LauncherService
    {
        private readonly string _rootDirectory;
        private readonly SettingsManager _settingsManager;

        public LauncherService(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
            _settingsManager = new SettingsManager(rootDirectory);
        }

        private string Engine => GameSetup.EngineDirectory(_rootDirectory);

        // Starts the game: the engine's own launch script (Build\Engine\run-windows.ps1, hidden) with the options
        // the settings give; it prepares the shaders when needed, starts kyty_emulator.exe and waits for it (-Follow).
        public async Task<int> LaunchGameAsync(AppSettings settings, HardwareInfo? hardware = null)
        {
            CheckEngine();
            // --play (RecompLauncher, no window) has no hardware yet: the screen's refresh (frame generation's game rate)
            // and the chosen card come from it, so it is detected here as the window does (0.2.2).
            if (hardware == null)
            {
                try { hardware = new HardwareDetector().DetectHardware(); hardware.SelectGpu(settings.Gpu); } catch { }
            }
            settings.LastLaunched = DateTime.Now;
            _settingsManager.Save(settings);
            return await StartAsync(BuildArguments(settings, hardware), settings.Priority, settings);
        }

        // DemonsSoulsPC.exe --run [run-windows.ps1 options]: the game with the dump from the settings and only the
        // given options (measurement scripts: "-Set KYTY_FPS_HUD=2", "-Width 1920 -Height 1080"...).
        public Task<int> RunWithDefaultsAsync(string[] options)
        {
            CheckEngine();
            var settings = _settingsManager.Load();
            var args = new List<string> { "-Game", GameFolder(settings) };
            args.AddRange(options);
            // The shader translation before the game, as Play does (without it the engine translates beside the game, its
            // "Preparing shaders" bar over play), unless the options set the engine's variables themselves.
            if (!options.Any(o => o.Equals("-Set", StringComparison.OrdinalIgnoreCase)))
                args.AddRange(new[] { "-Set", string.Join(",", PrefetchSets()) });
            return StartAsync(args, "");
        }

        private static IEnumerable<string> PrefetchSets() => new[]
        {
            $"KYTY_SHADER_PREFETCH_THREADS={Math.Max(4, Environment.ProcessorCount - 2)}",
            "KYTY_PREFETCH_WAIT=1",
        };

        private static readonly Regex EngineLogLine = new Regex(@"logs:\s*(\S+)\.\*\.log", RegexOptions.Compiled);
        private static readonly Regex TitleProgress = new Regex(@"(\d+)\s*/\s*(\d+)", RegexOptions.Compiled);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        // The engine's shader translation (the PS5 shaders into SPIR-V, _PipelineCache\static\*.prefetch), the part of the
        // shader preparation the precompile does not do: without it the first Play holds the game behind the engine's
        // "Preparing shaders" bar (minutes on a small processor). The engine has no "translate and quit": it is started
        // as Play starts it (same options, so the same cache key), held by KYTY_PREFETCH_WAIT=1 before the game's first
        // instruction, its window hidden, and stopped once its log says the wait is over ("Shader prefetch wait: done",
        // written after the cache index is saved). True when the translation is done (or the engine runs none).
        public async Task<bool> PrefetchShadersAsync(IProgress<(int Percent, string Message)>? progress, System.Threading.CancellationToken cancel)
        {
            CheckEngine();
            var settings = _settingsManager.Load();
            var args = BuildArguments(settings, null);
            args.RemoveAll(a => a == "-Fullscreen");
            for (int i = 0; i + 1 < args.Count; i++)
            {
                if (args[i] == "-Width") args[i + 1] = "640";
                if (args[i] == "-Height") args[i + 1] = "360";
            }
            // The hold's own limit (600 s by default) would let the game start on a slow PC: up to two hours here.
            int set = args.IndexOf("-Set");
            if (set >= 0 && set + 1 < args.Count) args[set + 1] += ",KYTY_PREFETCH_WAIT_S=7200";
            else args.AddRange(new[] { "-Set", "KYTY_PREFETCH_WAIT_S=7200" });
            args.Insert(0, "-Follow");

            var logs = Path.Combine(_rootDirectory, "Logs");
            Directory.CreateDirectory(logs);
            var logPath = Path.Combine(logs, "precompile.log");
            void Log(string line) => GameSetup.AppendLog(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] translation: {line}\r\n");

            Log(MemoryCheck.RemoveMemorySavings(Engine));
            Log(EngineTitle.Apply(Engine));
            var psi = GameSetup.PowerShell(Path.Combine(Engine, GameSetup.RunScript), args, Engine);
            Log(psi.Arguments);
            string? stem = null;
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (s, e) => { if (e.Data != null) { var m = EngineLogLine.Match(e.Data); if (m.Success) stem = m.Groups[1].Value; } };
            process.ErrorDataReceived += (s, e) => { };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            progress?.Report((-1, Loc.T("Prep.Translating")));

            bool? result = null;
            var hidden = new HashSet<IntPtr>();
            var started = DateTime.Now;
            int tick = 0;
            using (cancel.Register(() => GameSetup.KillTree(process)))
            {
                while (result == null && !process.HasExited && !cancel.IsCancellationRequested)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                    tick++;
                    foreach (var engine in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameSetup.GameExe)))
                    {
                        try
                        {
                            if (!string.Equals(engine.MainModule?.FileName, Path.Combine(Engine, GameSetup.GameExe), StringComparison.OrdinalIgnoreCase)) continue;
                            var window = engine.MainWindowHandle;
                            if (window != IntPtr.Zero && hidden.Add(window)) ShowWindow(window, 0); // SW_HIDE
                            var match = TitleProgress.Match(engine.MainWindowTitle ?? "");
                            if (match.Success && int.TryParse(match.Groups[2].Value, out var total) && total > 0)
                            {
                                var done = int.Parse(match.Groups[1].Value);
                                progress?.Report((Math.Min(100, done * 100 / total), string.Format(Loc.T("Prep.TranslatingCount"), done, total)));
                            }
                        }
                        catch { }
                    }
                    if (stem == null || tick % 4 != 0) continue; // the log once a second
                    foreach (var line in ReadShared(Path.Combine(Engine, stem + ".out.log")))
                    {
                        if (!line.StartsWith("Shader prefetch wait:", StringComparison.Ordinal) || line.Contains("the game starts once")) continue;
                        Log(line);
                        result = line.Contains(": done") || line.Contains("no shader prefetch runs");
                        break;
                    }
                }
            }
            GameSetup.KillTree(process);
            // The engine gone before the game (or the next step) starts: a few seconds, then stopped outright.
            for (int wait = 0; wait < 40; wait++)
            {
                var left = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameSetup.GameExe)).Where(p =>
                {
                    try { return string.Equals(p.MainModule?.FileName, Path.Combine(Engine, GameSetup.GameExe), StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                }).ToList();
                if (left.Count == 0) break;
                if (wait == 20) foreach (var p in left) try { p.Kill(); } catch { }
                await Task.Delay(250).ConfigureAwait(false);
            }
            if (result == null) Log(cancel.IsCancellationRequested ? "stopped" : $"the engine ended first (code {(process.HasExited ? process.ExitCode : -1)})");
            Log($"{(result == true ? "done" : "not done")} in {(DateTime.Now - started).TotalSeconds:0} s");
            return result == true;
        }

        private static List<string> ReadShared(string path)
        {
            var lines = new List<string>();
            try
            {
                if (!File.Exists(path)) return lines;
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            catch { }
            return lines;
        }

        private void CheckEngine()
        {
            if (!File.Exists(Path.Combine(Engine, GameSetup.GameExe)) || !File.Exists(Path.Combine(Engine, GameSetup.RunScript)))
                throw new FileNotFoundException("kyty_emulator.exe or run-windows.ps1 not found in Build\\Engine", Engine);
        }

        private string GameFolder(AppSettings settings)
        {
            var dump = settings.GameDumpPath;
            if (string.IsNullOrEmpty(dump) || !File.Exists(Path.Combine(dump, "eboot.bin")))
                dump = GameFeatures.Load(Engine)?.Dump;
            if (string.IsNullOrEmpty(dump) || !File.Exists(Path.Combine(dump, "eboot.bin")))
                throw new DirectoryNotFoundException(Loc.T("The game folder (with eboot.bin) was not found: choose it again in the settings."));
            GameFeatures.EnsureSupportedVersion(dump!);
            return dump!;
        }

        private async Task<int> StartAsync(List<string> options, string priority, AppSettings? settings = null)
        {
            var args = new List<string> { "-Follow" };
            args.AddRange(options);
            var psi = GameSetup.PowerShell(Path.Combine(Engine, GameSetup.RunScript), args, Engine);

            var logs = Path.Combine(_rootDirectory, "Logs");
            Directory.CreateDirectory(logs);
            // The engine's memory savings removed (MemoryCheck): patched before every start, in case the runtime was
            // installed or repaired since.
            var memory = MemoryCheck.RemoveMemorySavings(Engine);
            memory += "\r\n" + EngineTitle.Apply(Engine); // the window's title: "Demon's Souls PC"
            try { memory += "\r\n" + await Task.Run(MemoryCheck.FreeUpForEngine); } catch { }
            try
            {
                File.WriteAllText(Path.Combine(logs, "dspc-launch.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] engine {GameSetup.EngineVersion(_rootDirectory)}\r\n{memory}\r\n{psi.FileName} {psi.Arguments}\r\n");
            }
            catch { }
            // Logs\dspc-summary.log (what RecompLauncher's report needs in one file, also appended to the logs it may send)
            // and the engine log's useful part.
            using var report = new SessionReport(_rootDirectory, settings, psi.Arguments);
            var outLog = new StreamWriter(Path.Combine(logs, "game-run.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (sender, e) => { if (e.Data != null) { lock (outLog) outLog.WriteLine(e.Data); report.Output(e.Data); } };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) { lock (outLog) outLog.WriteLine(e.Data); report.Output(e.Data); } };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _ = Task.Run(() => SetEnginePriority(process, priority));
            EngineTitle.WatchWindow(process);

            // WaitForExit() without a timeout also waits for the end of both outputs.
            await Task.Run(() => process.WaitForExit());
            lock (outLog) outLog.Dispose();
            report.Finish(process.ExitCode);
            return process.ExitCode;
        }

        // The script starts the engine a little after it starts (shader inputs first, up to a minute the first time):
        // its priority is set once it is there.
        private static void SetEnginePriority(Process script, string priority)
        {
            ProcessPriorityClass cls;
            switch (priority)
            {
                case "normal": return;
                case "high": cls = ProcessPriorityClass.High; break;
                case "abovenormal": cls = ProcessPriorityClass.AboveNormal; break;
                default: return;
            }
            var until = DateTime.Now.AddMinutes(5);
            while (DateTime.Now < until && !script.HasExited)
            {
                foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameSetup.GameExe)))
                {
                    try { p.PriorityClass = cls; return; } catch { }
                }
                System.Threading.Thread.Sleep(1000);
            }
        }

        // The settings as run-windows.ps1 options (the same ones the engine's own launcher passes).
        public List<string> BuildArguments(AppSettings settings, HardwareInfo? hardware)
        {
            var args = new List<string> { "-Game", GameFolder(settings) };
            var sets = new List<string>();

            // Window size; indices 4 and 5 are 21:9 (the game renders its 3D wider: -Aspect 21:9).
            var sizeValues = new[,] { { 1280, 720 }, { 1600, 900 }, { 1920, 1080 }, { 2560, 1440 }, { 2560, 1080 }, { 3440, 1440 } };
            int size = settings.Size >= 0 && settings.Size < sizeValues.GetLength(0) ? settings.Size : 2;
            args.AddRange(new[] { "-Width", sizeValues[size, 0].ToString(), "-Height", sizeValues[size, 1].ToString() });
            if (size >= 4) args.AddRange(new[] { "-Aspect", "21:9" });

            // Full screen; RecompLauncher's Big Picture mode (controller, couch) always.
            if (settings.Fullscreen || Environment.GetEnvironmentVariable("RECOMPLAUNCHER_MODE") == "bigpicture")
                args.Add("-Fullscreen");

            // Frame generation (the Graphics page): 1-3 = NVIDIA DLSS with 1-3 generated frames (the Streamline runtime
            // the runtime ships in Build\Engine\streamline, where the engine looks by itself), 4 = AMD FSR 3 2x
            // (amd_fidelityfx_vk.dll next to the engine). The engine checks the card and driver and runs without it
            // when they cannot. VSync is the frame generator's own business then.
            bool frameGen = settings.FrameGenMode >= 1 && settings.FrameGenMode <= 4;

            // Frame rate: 0 = 30 (KYTY_FLIP_RATE=1, steady half rate), 1 = 60 (the game's own), 2 = up to 120.
            // With frame generation (0.2.2): the game's rate follows the screen instead (FrameGenGameRate), so the
            // frames shown fill its refresh without going past it (165 Hz: x2 -> game up to 82, x3 -> 55, x4 -> 41).
            int fgRate = frameGen ? FrameGenGameRate(settings.FrameGenMode, hardware?.RefreshRate ?? 60) : 0;
            if (fgRate > 0) args.AddRange(new[] { "-Vblank", fgRate.ToString() });
            else if (!frameGen && settings.Fps == 0) sets.Add("KYTY_FLIP_RATE=1");
            else if (!frameGen && settings.Fps == 2) args.Add("-Fps120");
            if (settings.FrameGenMode >= 1 && settings.FrameGenMode <= 3) args.AddRange(new[] { "-FrameGen", settings.FrameGenMode.ToString() });
            else if (settings.FrameGenMode == 4) args.Add("-Fsr");

            if (settings.VSync && !frameGen) args.AddRange(new[] { "-PresentMode", "Fifo" });
            if (settings.LowLatency) args.Add("-LowLatency");

            // Graphics: 0 standard (as on the PS5), 1 low, 2 lowest (shorter shadows and distant detail, smaller
            // textures): faster where the processor or the video memory limits.
            if (settings.Lighting == 1) args.AddRange(new[] { "-Quality", "low", "-TextureReduce", "1" });
            else if (settings.Lighting == 2) args.AddRange(new[] { "-Quality", "lowest", "-TextureReduce", "2" });

            // Upscaling, through Demon's Souls' own PS5 Pro path (src/loader/demonsSoulsSuperResolution.h): the upscaler in
            // place of Sony's, its output at the window's height (-OutputHeight, 1080-2159: the default 3840x2160 output cost
            // an RTX 5070 60 -> 43 FPS in DLSS Quality). 1-5 = NVIDIA DLSS (sl.dlss + nvngx_dlss in Build\Engine\streamline;
            // RTX cards), whose mode the engine takes from the share of the output the 3D is rendered at
            // (src/local/frame-gen.cpp: >= 0.95 DLAA, 0.62 Quality, 0.55 Balanced, 0.45 Performance, else Ultra
            // Performance), so each mode is a render height: output x 2/3, 0.58, 1/2, 1/3, 1 (1440 output: 960, 835, 720,
            // 480, 1440). Without DLSS on the card the engine falls back to FSR 3. 6 = AMD FSR 3 (built into the engine,
            // any card) at the engine's own render height for that output.
            int output = Math.Max(1080, Math.Min(2159, sizeValues[size, 1]));
            if (settings.Upscale >= 1 && settings.Upscale <= 5)
            {
                var shares = new[] { 0.0, 2.0 / 3, 0.58, 0.5, 1.0 / 3, 1.0 };
                int render = (int)Math.Round(output * shares[settings.Upscale]);
                args.AddRange(new[] { "-Upscaler", "dlss", "-OutputHeight", output.ToString(), "-RenderHeight", render.ToString() });
            }
            else if (settings.Upscale == 6)
                args.AddRange(new[] { "-Upscaler", "fsr", "-OutputHeight", output.ToString() });

            // Performance overlay (F8 in the game switches it): 1 frame rate, 2 detailed.
            if (settings.Overlay >= 1) sets.Add($"KYTY_FPS_HUD={Math.Min(settings.Overlay, 2)}");

            // Shader preparation (the PS5 shaders translated for this PC, kept in _PipelineCache\static\*.prefetch): done
            // before the game starts, on every thread but two. Left to the engine it runs beside the game on 2-4 threads
            // for minutes, and a session closed early starts it over: 17 s instead on a 16-thread PC, nothing once cached.
            sets.Add($"KYTY_SHADER_PREFETCH_THREADS={Math.Max(4, Environment.ProcessorCount - 2)}");
            sets.Add("KYTY_PREFETCH_WAIT=1");

            // Console language; Auto maps the Windows display language.
            int language = settings.Language >= 0 && settings.Language <= 30 ? settings.Language : WindowsLanguage();
            args.AddRange(new[] { "-Language", language.ToString() });

            // Graphics card: an explicit choice names it (a part of its Vulkan name); automatic = the first discrete one.
            var gpu = hardware?.SelectedGpu;
            if (hardware != null && hardware.SelectedGpuIndex >= 0 && gpu != null)
                args.AddRange(new[] { "-Gpu", GpuMatchToken(gpu.Name) });

            if (sets.Count > 0) args.AddRange(new[] { "-Set", string.Join(",", sets) });
            return args;
        }

        // Frames shown per game frame for a frame generation mode (1-3 NVIDIA x2-x4, 4 AMD FSR 3 x2), 1 without.
        public static int FrameGenFactor(int mode) => mode >= 1 && mode <= 3 ? mode + 1 : mode == 4 ? 2 : 1;

        // The game's own rate (the engine's virtual vblank, -Vblank) with frame generation: the screen's refresh divided by
        // the frames shown per game frame, so the generated frames fill the screen without going past its refresh
        // (frames past it are thrown away or tear, with more latency). 30-120; 0 = the console's 60 (a 60 Hz rate, or a
        // screen it cannot tell). Above 60 the engine's script keeps the game real time (gameMinDeltaTime=0.004); below,
        // the game steps by its frame's own time. On a 9800X3D the game tops out near 70 by the processor anyway.
        public static int FrameGenGameRate(int mode, int refreshHz)
        {
            int factor = FrameGenFactor(mode);
            if (factor <= 1 || refreshHz < 50) return 0;
            int rate = Math.Min(120, refreshHz / factor);
            // Frame generation needs about 40-60 game frames a second (below, the latency and the artifacts show): a
            // fast screen keeps at least 40; a 60 Hz one keeps the game's 60 (the generated frames gain nothing there).
            if (rate < 40) rate = refreshHz >= 100 ? 40 : 60;
            return rate == 60 ? 0 : rate;
        }

        // The engine selects a card by a part of its name (KYTY_GPU). The model token is used instead of the full
        // Windows name so it still matches the shorter name Vulkan reports ("NVIDIA GeForce RTX 4050 Laptop GPU" -> "RTX 4050").
        public static string GpuMatchToken(string name)
        {
            var match = Regex.Match(name, @"(?:RTX|GTX)\s*\d{3,4}(?:\s*(?:Ti|SUPER))?", RegexOptions.IgnoreCase);
            if (match.Success) return match.Value;
            match = Regex.Match(name, @"Arc\s*[AB]?\d{3}", RegexOptions.IgnoreCase);
            if (match.Success) return match.Value;
            match = Regex.Match(name, @"(?:UHD|Iris(?:\s*Xe)?|HD)\s*Graphics(?:\s*\d{3})?", RegexOptions.IgnoreCase);
            if (match.Success) return match.Value;
            match = Regex.Match(name, @"RX\s*\d{4}(?:\s*XT)?", RegexOptions.IgnoreCase);
            if (match.Success) return match.Value;
            return name;
        }

        // The console language number for the Windows display language; English (United States) when unknown.
        public static int WindowsLanguage()
        {
            var culture = System.Globalization.CultureInfo.CurrentUICulture;
            string name = culture.Name, two = culture.TwoLetterISOLanguageName;
            switch (name)
            {
                case "pt-BR": return 17;
                case "en-GB": return 18;
                case "fr-CA": return 22;
                case "zh-TW": case "zh-HK": case "zh-MO": return 10;
            }
            if (two == "es") return name == "es-ES" ? 3 : 20;
            if (two == "zh") return 11;
            switch (two)
            {
                case "ja": return 0;
                case "en": return 1;
                case "fr": return 2;
                case "de": return 4;
                case "it": return 5;
                case "nl": return 6;
                case "pt": return 7;
                case "ru": return 8;
                case "ko": return 9;
                case "fi": return 12;
                case "sv": return 13;
                case "da": return 14;
                case "nb": case "nn": case "no": return 15;
                case "pl": return 16;
                case "tr": return 19;
                case "ar": return 21;
                case "cs": return 23;
                case "hu": return 24;
                case "el": return 25;
                case "ro": return 26;
                case "th": return 27;
                case "vi": return 28;
                case "id": return 29;
                case "uk": return 30;
                default: return 1;
            }
        }

        public bool IsGameRunning()
        {
            return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameSetup.GameExe)).Length > 0;
        }

        public void KillGameProcesses()
        {
            foreach (var proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameSetup.GameExe)))
            {
                try { proc.Kill(); } catch { }
            }
        }
    }
}
