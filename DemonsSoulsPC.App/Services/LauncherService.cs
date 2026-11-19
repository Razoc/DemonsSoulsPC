using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        // Starts the game directly (no script): the settings' variables, then the runtime defaults the old
        // launch script applied, then DemonsSouls.exe in Build\RunHybrid with its output in Logs.
        public async Task<int> LaunchGameAsync(AppSettings settings, HardwareInfo? hardware = null)
        {
            var runHybrid = Path.Combine(_rootDirectory, "Build", "RunHybrid");
            if (!File.Exists(Path.Combine(runHybrid, "DemonsSouls.exe")))
            {
                throw new FileNotFoundException("DemonsSouls.exe not found in RunHybrid", runHybrid);
            }

            settings.LastLaunched = DateTime.Now;
            _settingsManager.Save(settings);
            GameSetup.ApplyRenderConfig(_rootDirectory, settings.Lighting == 1);

            return await StartGameAsync(BuildEnvironmentVariables(settings, hardware), Array.Empty<string>());
        }

        // DemonsSoulsPC.exe --run [game arguments]: the game with the runtime defaults only, the caller's environment
        // and its arguments (measurement scripts: AKIBOT_* variables set beforehand, "-lvl <level>"...).
        public Task<int> RunWithDefaultsAsync(string[] gameArgs)
        {
            var runHybrid = Path.Combine(_rootDirectory, "Build", "RunHybrid");
            if (!File.Exists(Path.Combine(runHybrid, "DemonsSouls.exe")))
            {
                throw new FileNotFoundException("DemonsSouls.exe not found in RunHybrid", runHybrid);
            }
            return StartGameAsync(new Dictionary<string, string>(), gameArgs);
        }

        private async Task<int> StartGameAsync(Dictionary<string, string> variables, string[] gameArgs)
        {
            var runHybrid = Path.Combine(_rootDirectory, "Build", "RunHybrid");
            var psi = new ProcessStartInfo
            {
                WorkingDirectory = runHybrid,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            var env = psi.EnvironmentVariables;
            foreach (var kvp in variables)
            {
                env[kvp.Key] = kvp.Value;
            }
            RuntimeDefaults.Apply(env, runHybrid);

            psi.FileName = Path.Combine(runHybrid, env["AKIBOT_EXE"]);
            var args = new List<string>();
            if (env["AKIBOT_SKIP_INTRO"] == "1") args.Add("-skipIntro");
            args.AddRange(gameArgs);
            psi.Arguments = string.Join(" ", args.ConvertAll(a => a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0 ? a : "\"" + a.Replace("\"", "\\\"") + "\""));

            var logs = Path.Combine(_rootDirectory, "Logs");
            Directory.CreateDirectory(logs);
            // What the game is started with (Demon’s Souls PC, AnyPS5 and Kyty variables), to compare runs when one fails.
            try
            {
                var launch = new List<string> { $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {psi.FileName} {psi.Arguments}" };
                foreach (string key in env.Keys)
                {
                    var upper = key.ToUpperInvariant();
                    if (upper.StartsWith("AKIBOT_") || upper.StartsWith("APS5_") || upper.StartsWith("ANYPS5_") || upper.StartsWith("KYTY_"))
                        launch.Add($"{upper}={env[key]}");
                }
                launch.Sort(1, launch.Count - 1, StringComparer.Ordinal);
                File.WriteAllLines(Path.Combine(logs, "dspc-launch.log"), launch);
            }
            catch { }
            using var outLog = new StreamWriter(Path.Combine(logs, "hybrid-run.out.log"), false);
            using var errLog = new StreamWriter(Path.Combine(logs, "hybrid-run.err.log"), false);
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (sender, e) => { if (e.Data != null) lock (outLog) outLog.WriteLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) lock (errLog) errLog.WriteLine(e.Data); };

            process.Start();
            RuntimeDefaults.SetPriority(process, env["AKIBOT_PRIORITY"]);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // WaitForExit() without a timeout also waits for the end of both outputs.
            await Task.Run(() => process.WaitForExit());
            return process.ExitCode;
        }

        private Dictionary<string, string> BuildEnvironmentVariables(AppSettings settings, HardwareInfo? hardware)
        {
            var env = new Dictionary<string, string>();

            // Demon’s Souls PC in-game menu (F1 / Escape): reads and saves this same settings file, in the launcher's language.
            env["AKIBOT_SETTINGS_FILE"] = Path.Combine(_rootDirectory, "Build", "dspc-settings.json");
            env["AKIBOT_LANG"] = Loc.Language;

            // Graphics card (issue #24): an explicit choice names the card for the AnyPS5 renderer.
            // kyty_gpu.dll prefers a discrete card over integrated graphics by
            // itself, so an automatic choice passes nothing and the renderer keeps that preference.
            var gpu = hardware?.SelectedGpu;
            if (hardware != null && hardware.SelectedGpuIndex >= 0 && gpu != null)
            {
                env["ANYPS5_GPU"] = GpuMatchToken(gpu.Name);
            }

            // One game executable (no lighting or ultrawide variants: those were Astro Bot patches).
            env["AKIBOT_EXE"] = GameSetup.GameExe;

            // FPS limit
            var fpsValues = new[] { "30", "35", "45", "50", "55", "60", "unlimited" };
            if (settings.Fps >= 0 && settings.Fps < fpsValues.Length)
            {
                env["AKIBOT_FPS_LIMIT"] = fpsValues[settings.Fps];
            }

            // Window size
            // Indices 0..3 = 16:9, indices 4..5 = 21:9 ultrawide (#58)
            var sizeValues = new[,] { { 1280, 720 }, { 1600, 900 }, { 1920, 1080 }, { 2560, 1440 }, { 2560, 1080 }, { 3440, 1440 } };
            if (settings.Size >= 0 && settings.Size < sizeValues.GetLength(0))
            {
                int width = sizeValues[settings.Size, 0];
                int height = sizeValues[settings.Size, 1];
                env["KYTY_GPU_WIDTH"] = width.ToString();
                env["KYTY_GPU_HEIGHT"] = height.ToString();
            }

            // Fullscreen
            env["KYTY_GPU_FULLSCREEN"] = settings.Fullscreen ? "1" : "0";
            // Started from RecompLauncher's Big Picture mode (controller, couch): always full screen.
            if (Environment.GetEnvironmentVariable("RECOMPLAUNCHER_MODE") == "bigpicture")
                env["KYTY_GPU_FULLSCREEN"] = "1";

            // Performance overlay
            var overlayValues = new[] { "", "fps", "compact", "full" };
            if (settings.Overlay >= 0 && settings.Overlay < overlayValues.Length)
            {
                env["AKIBOT_OVERLAY"] = overlayValues[settings.Overlay];
            }

            // DLSS, FSR and frame generation: not tested on Demon’s Souls yet, hidden in the launcher; a settings file
            // that still asks for them (in-game menu, RecompLauncher) is ignored.
            settings.Upscale = 0;
            settings.FrameGenMode = 0;
            settings.LowLatency = false;
            if (settings.Upscale != 0 || settings.FrameGenMode != 0)
            {
                env["AKIBOT_CUDA_HOLE"] = "1";
                env["AKIBOT_DLSS_PATH"] = Path.Combine(_rootDirectory, "Build", "RunHybrid");
            }
            if (settings.Upscale == 6)
            {
                // AMD FSR 3.1 (Demon’s Souls PC): same pass as DLSS, amd_fidelityfx_vk.dll next to the game.
                env["AKIBOT_FSR"] = "1";
            }
            else if (settings.Upscale != 0)
            {
                env["AKIBOT_DLSS"] = "1";
                // Upscale: 1 Quality, 2 Balanced, 3 Performance, 4 Ultra Performance, 5 DLAA (DLSS preset).
                var dlssModes = new[] { "quality", "quality", "balanced", "performance", "ultra", "dlaa" };
                env["AKIBOT_DLSS_QUALITY"] = dlssModes[Math.Max(0, Math.Min(settings.Upscale, dlssModes.Length - 1))];
            }

            // Frame generation plan (AstroBotLauncher.FrameGenPlan): the game is never capped below 30.
            // Auto with a target starts at 2x with the game capped at half the target and lets the game
            // raise the multiplier; fixed modes cap the game at target / multiplier; no lock = uncapped.
            if (settings.FrameGenMode == 5)
            {
                // AMD FSR 3 frame generation (Demon’s Souls PC): always 1 generated frame, the game at half the target.
                var fsrTargets = new[] { 0, 50, 60, 90, 120, 144, 165 };
                int fsrGoal = settings.FrameGenTarget >= 0 && settings.FrameGenTarget < fsrTargets.Length ? fsrTargets[settings.FrameGenTarget] : 60;
                env["AKIBOT_FSRFG"] = "1";
                env["AKIBOT_FPS_LIMIT"] = fsrGoal == 0 ? "unlimited" : Math.Max(30, fsrGoal / 2).ToString();
            }
            else if (settings.FrameGenMode != 0)
            {
                var targetValues = new[] { 0, 50, 60, 90, 120, 144, 165 };
                int goal = settings.FrameGenTarget >= 0 && settings.FrameGenTarget < targetValues.Length
                    ? targetValues[settings.FrameGenTarget] : 60;
                int mult = settings.FrameGenMode == 1 ? 2 : Math.Min(Math.Max(settings.FrameGenMode, 2), 4);
                int limit = goal == 0 ? 0 : Math.Max(30, goal / mult);
                env["AKIBOT_DLSSG"] = "1";
                env["AKIBOT_DLSSG_FRAMES"] = (mult - 1).ToString();
                if (settings.FrameGenMode == 1 && goal != 0)
                {
                    env["AKIBOT_DLSSG_TARGET"] = goal.ToString();
                    env["AKIBOT_DLSSG_MAX_FRAMES"] = "3";
                }
                env["AKIBOT_FPS_LIMIT"] = limit == 0 ? "unlimited" : limit.ToString();
            }

            // NVIDIA Reflex
            if (settings.LowLatency)
            {
                env["AKIBOT_LOW_LATENCY"] = "1";
            }

            // Controller settings
            var padValues = new[] { "auto", "ps", "xbox", "switch", "generic" };
            if (settings.Controller >= 0 && settings.Controller < padValues.Length)
            {
                env["AKIBOT_PAD_PREFER"] = padValues[settings.Controller];
            }

            // Vibration strength in percent (0-200); the Switch boost follows it instead of being a flat
            // 96 of 255, which made low strengths louder than the setting itself (#36).
            int rumblePercent = settings.EffectiveRumblePercent;
            env["AKIBOT_RUMBLE_SCALE"] = rumblePercent.ToString();
            env["AKIBOT_RUMBLE_SWITCH_FLOOR"] = RumbleSetting.SwitchFloor(rumblePercent, settings.SwitchBoost).ToString();

            // Wireless DualSense features (Bluetooth HID audio/haptics) - issue #34
            env["AKIBOT_DS_WIRELESS_SPEAKER"] = settings.DualSenseWirelessSpeaker ? "1" : "0";
            env["AKIBOT_DS_WIRELESS_HAPTICS"] = settings.DualSenseWirelessHaptics ? "1" : "0";

            // Window title
            env["AKIBOT_WINDOW_TITLE"] = "Demon’s Souls PC";

            // Game language (AnyPS5 console language number); Auto maps the Windows display language.
            int language = settings.Language >= 0 && settings.Language <= 30 ? settings.Language : WindowsLanguage();
            env["ANYPS5_LANGUAGE"] = language.ToString();

            // VSync: Vulkan FIFO presentation (the default is immediate presentation; skipped when DLSS Frame Generation is active).
            if (settings.VSync && settings.FrameGenMode == 0) env["KYTY_GPU_PRESENT_MODE"] = "Fifo";

            // Advanced page: variables RuntimeDefaults reads (it keeps its defaults when unset).
            if (settings.HangWatchdog) env["AKIBOT_HANG_WATCHDOG"] = "1";
            if (!settings.ShaderCache) env["AKIBOT_SHADER_CACHE"] = "0";
            env["AKIBOT_DRAW_PREP_TUNING"] = settings.DrawPrepTuning ? "1" : "0";
            if (settings.Priority == "normal" || settings.Priority == "abovenormal" || settings.Priority == "high")
                env["AKIBOT_PRIORITY"] = settings.Priority;
            if (settings.CpPriority == 1 || settings.CpPriority == 2) env["KYTY_CP_PRIORITY"] = settings.CpPriority.ToString();
            if (!settings.TriggerState) env["APS5_PAD_TRIGGER_STATE"] = "0";

            env["APS5_SAVEDATA_TRANSFER_FALLBACK"] = "1";

            return env;
        }

        // The renderer selects a card by name (ANYPS5_GPU, a case-insensitive substring of the Vulkan
        // device name). The model token is used instead of the full Windows name so it still matches
        // the shorter name Vulkan reports ("NVIDIA GeForce RTX 4050 Laptop GPU" -> "RTX 4050").
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
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameSetup.GameExe));
            return processes.Length > 0;
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
