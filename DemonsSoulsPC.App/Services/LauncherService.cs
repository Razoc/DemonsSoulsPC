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
            return StartAsync(args, "");
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
            return dump!;
        }

        private async Task<int> StartAsync(List<string> options, string priority, AppSettings? settings = null)
        {
            var args = new List<string> { "-Follow" };
            args.AddRange(options);
            var psi = GameSetup.PowerShell(Path.Combine(Engine, GameSetup.RunScript), args, Engine);

            var logs = Path.Combine(_rootDirectory, "Logs");
            Directory.CreateDirectory(logs);
            try
            {
                File.WriteAllText(Path.Combine(logs, "dspc-launch.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] engine {GameSetup.EngineVersion(_rootDirectory)}\r\n{psi.FileName} {psi.Arguments}\r\n");
            }
            catch { }
            // Logs\dspc-summary.txt (what RecompLauncher's report needs in one file) and the engine log's useful part.
            using var report = new SessionReport(_rootDirectory, settings, psi.Arguments);
            var outLog = new StreamWriter(Path.Combine(logs, "game-run.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (sender, e) => { if (e.Data != null) { lock (outLog) outLog.WriteLine(e.Data); report.Output(e.Data); } };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) { lock (outLog) outLog.WriteLine(e.Data); report.Output(e.Data); } };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _ = Task.Run(() => SetEnginePriority(process, priority));

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

            // Frame rate: 0 = 30 (KYTY_FLIP_RATE=1, steady half rate), 1 = 60 (the game's own), 2 = up to 120.
            if (settings.Fps == 0) sets.Add("KYTY_FLIP_RATE=1");
            else if (settings.Fps == 2) args.Add("-Fps120");

            if (settings.VSync) args.AddRange(new[] { "-PresentMode", "Fifo" });
            if (settings.LowLatency) args.Add("-LowLatency");

            // Graphics: 0 standard (as on the PS5), 1 low, 2 lowest (shorter shadows and distant detail, smaller
            // textures): faster where the processor or the video memory limits.
            if (settings.Lighting == 1) args.AddRange(new[] { "-Quality", "low", "-TextureReduce", "1" });
            else if (settings.Lighting == 2) args.AddRange(new[] { "-Quality", "lowest", "-TextureReduce", "2" });

            // Upscaling: 6 = AMD FSR (built into the engine, any graphics card); the others stay the game's own.
            if (settings.Upscale == 6) args.AddRange(new[] { "-Upscaler", "fsr", "-OutputHeight", Math.Max(1080, Math.Min(2159, sizeValues[size, 1])).ToString() });

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
