using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DemonsSoulsPC.App.Services
{
    // The runtime defaults the game needs, applied by the launcher itself (they used to live in a launch
    // script). A variable already set (by the settings or by the user's environment) always wins, as before.
    public static class RuntimeDefaults
    {
        public const string HostModule = "akibot_host.dll";
        public const string PresetFile = "u59-preset.json";

        public static void Apply(StringDictionary env, string runHybrid)
        {
            env["APS5_GPU_ENGINE"] = "kyty";
            env.Remove("KYTY_BARRIER_BATCH");
            env.Remove("KYTY_DISPATCH_OVERLAP");
            env.Remove("KYTY_WAVE64_WARPS");
            // Demon’s Souls (tests of 2026-11-19): KYTY_BUFFER_RANGE_FIXES reads into the game's unmapped memory holes
            // (access violation in character creation), and the game rewrites its command buffers as soon as
            // sceAgcSuspendPoint returns, so Kyty must have read them first (else an unknown packet at the first logo).
            env.Remove("KYTY_BUFFER_RANGE_FIXES");
            Default(env, "KYTY_AGC_DONE_MODE", "idle");
            // ~3,000 compute dispatches per gameplay frame: prepared by the draw-prep workers instead of the command
            // processor (A/B 2026-11-19: 18.7 -> 20.7 FPS in the tutorial). DSPC_DRAW_PREP_DISPATCH=0 turns it off.
            if (Get(env, "DSPC_DRAW_PREP_DISPATCH") != "0")
                Default(env, "KYTY_DRAW_PREP_DISPATCH", "1");
            // Indirect draws whose arguments the game's CPU wrote keep DrawRun's runs going (verify: 0 mismatches in
            // 1.9M checks; 20.7 -> 22.2 FPS, 2026-11-19). DSPC_DRAW_RUN_CPU_INDIRECT=0 turns it off.
            if (Get(env, "DSPC_DRAW_RUN_CPU_INDIRECT") != "0")
                Default(env, "KYTY_DRAW_RUN_CPU_INDIRECT", "1");
            // The sequencer reads register packets directly from pages the resolver saw clean since the last GPU write
            // (A/B/A/B 2026-11-19: CP wait 25 -> 20 ms, 20-22 -> 22.5 FPS; experimental). DSPC_FRONT_CLEAN_SINCE=0 turns it off.
            if (Get(env, "DSPC_FRONT_CLEAN_SINCE") != "0")
                Default(env, "KYTY_FRONT_CLEAN_SINCE", "1");
            // BDA "hot" passes only where a game CPU write can be ordered before GPU work (submission, waits...),
            // not at every dispatch (verify: 96.5% of passes skippable; A/B/A/B 2026-11-20: +0.4 to +0.9 FPS).
            // Needs runtime 0.1.2 (kyty_gpu.dll with KYTY_BDA_HOT_EPOCH). DSPC_BDA_HOT_EPOCH=0 turns it off.
            if (Get(env, "DSPC_BDA_HOT_EPOCH") != "0")
                Default(env, "KYTY_BDA_HOT_EPOCH", "1");

            // Hang watchdog (A/B 2026-11-07, OVERNIGHT.md): the preset's KYTY_HANG_WATCHDOG=auto turns it on for
            // RTX 50 GPUs, where it costs the command processor ~1.8 ms per frame. AKIBOT_HANG_WATCHDOG=1 brings it back.
            if (!Has(env, "KYTY_HANG_WATCHDOG") && Get(env, "AKIBOT_HANG_WATCHDOG") != "1")
                env["KYTY_HANG_WATCHDOG"] = "0";

            // Persistent shader and pipeline cache (2026-11-08): without the title ID Kyty's caches stay closed and
            // shaders compile during play (100-117 ms hitches). AKIBOT_SHADER_CACHE=0 turns it off.
            if (!Has(env, "AKIBOT_CACHE_TITLE_ID") && Get(env, "AKIBOT_SHADER_CACHE") != "0")
                env["AKIBOT_CACHE_TITLE_ID"] = "PPSA01341";

            // Kyty engine preset (Build\RunHybrid\u59-preset.json).
            foreach (var kvp in LoadPreset(Path.Combine(runHybrid, PresetFile)))
                Default(env, kvp.Key, kvp.Value);

            Default(env, "AKIBOT_EXE", GameSetup.GameExe);

            env["KYTY_GPU_HOST_MODULE"] = HostModule;
            // The game reads L2/R2 only when scePadGetTriggerEffectState answers as a DualSense does (2026-11-07).
            Default(env, "APS5_PAD_TRIGGER_STATE", "1");
            // Saves mounted through the transfer call when the game asks for it.
            Default(env, "APS5_SAVEDATA_TRANSFER_FALLBACK", "1");
            // Command processor at THREAD_PRIORITY_HIGHEST (A/B 2026-11-07).
            Default(env, "KYTY_CP_PRIORITY", "2");
            // Draw-prep helpers at the CP's priority with short spins (measured on Astro Bot; off unless asked for).
            if (Get(env, "AKIBOT_DRAW_PREP_TUNING") == "1")
            {
                Default(env, "KYTY_DRAW_PREP_PRIORITY", "2");
                Default(env, "KYTY_DRAW_PREP_SPIN_US", "10");
                Default(env, "KYTY_DRAW_PREP_COLD_SPIN_US", "5");
            }
            Default(env, "AKIBOT_PRIORITY", "abovenormal");
            Default(env, "AKIBOT_WINDOW_TITLE", "Demon’s Souls PC");
            Default(env, "KYTY_GPU_WIDTH", "1920");
            Default(env, "KYTY_GPU_HEIGHT", "1080");
        }

        public static void SetPriority(Process process, string? priority)
        {
            try
            {
                switch ((priority ?? "").ToLowerInvariant())
                {
                    case "normal": process.PriorityClass = ProcessPriorityClass.Normal; break;
                    case "high": process.PriorityClass = ProcessPriorityClass.High; break;
                    case "belownormal": process.PriorityClass = ProcessPriorityClass.BelowNormal; break;
                    default: process.PriorityClass = ProcessPriorityClass.AboveNormal; break;
                }
            }
            catch { }
        }

        private static Dictionary<string, string> LoadPreset(string file)
        {
            var values = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(file)) return values;
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var p in doc.RootElement.EnumerateObject())
                    values[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
            }
            catch { }
            return values;
        }

        private static bool Has(StringDictionary env, string key) => !string.IsNullOrEmpty(env[key]);

        private static string Get(StringDictionary env, string key) => env[key] ?? "";

        // "If not defined, set": an empty value leaves the variable unset, as the script did.
        private static void Default(StringDictionary env, string key, string value)
        {
            if (!Has(env, key) && !string.IsNullOrEmpty(value)) env[key] = value;
        }
    }
}
