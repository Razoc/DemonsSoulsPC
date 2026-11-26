using System;
using System.Collections.Generic;
using DemonsSoulsPC.App.Models;

namespace DemonsSoulsPC.App.Services
{
    // Demon's Souls PC 0.2.2: the player's goal instead of graphics jargon (the idea of Bloodborne PC's launcher):
    // Smooth, Balanced or Quiet, each turned into the settings of the Graphics page for this PC, with the reason for
    // every one. Taste stays the player's (window size, full screen, card, frame counter, controller are not touched).
    //
    // What the settings are made from (measured 23/11 on a Ryzen 7 9800X3D + RTX 5070, 1440p): the game is held by the
    // processor near 70 FPS (render thread): with 120 FPS unlocked, native 69, DLSS Quality 66, DLSS Performance 75,
    // FSR 3 66; so lower graphics or an upscaler give almost no frames there, and frame generation is the real gain
    // (60 -> 120 on screen), so Smooth keeps the game's own upscaling. (DLSS-G beside DLSS upscaling only stops on loading
    // screens, checked 26/11: the PS5 Pro path upscales them too, logged as "no camera".)
    public static class GoalPlanner
    {
        public const int Custom = 0, Smooth = 1, Balanced = 2, Quiet = 3;

        public class Plan
        {
            public int Lighting, Fps, Upscale, FrameGenMode;
            public bool VSync, LowLatency;
            public List<string> Reasons = new List<string>();
        }

        // A processor that keeps the game near its limit by itself: 12 threads or more (a Ryzen 5 7600 / Core i5-12400
        // class and up). Below, "low" graphics (shorter shadows and distant detail) help the processor too.
        static bool StrongCpu(HardwareInfo pc) => pc.CpuThreads >= 12;

        public static Plan Make(int goal, HardwareInfo pc)
        {
            var plan = new Plan();
            var r = plan.Reasons;
            bool rtx = pc.SupportsDlss;
            bool dlssFg = pc.SupportsFrameGeneration && pc.MaxFrameGenMultiplier >= 2;
            int refresh = pc.RefreshRate;

            switch (goal)
            {
                case Smooth:
                    plan.Lighting = StrongCpu(pc) ? 0 : 1;
                    r.Add(plan.Lighting == 0 ? Loc.T("Goal.CpuLimit") : Loc.T("Goal.SmoothLow"));
                    plan.Upscale = 0;
                    if (refresh >= 100)
                    {
                        // Frame generation works best from about 60 base FPS: x2 only (x3/x4 would hold the game near
                        // 55/41 on a 165 Hz screen, with more latency).
                        plan.FrameGenMode = dlssFg ? 1 : 4;
                        int rate = LauncherService.FrameGenGameRate(plan.FrameGenMode, refresh);
                        if (rate == 0) rate = 60;
                        r.Add(Loc.T(dlssFg ? "Goal.FgDlss" : "Goal.FgFsr", rate, rate * 2, refresh));
                        if (dlssFg) r.Add(Loc.T("Goal.FgNoUpscale"));
                        plan.Fps = 1;
                        plan.VSync = false;
                    }
                    else
                    {
                        plan.FrameGenMode = 0;
                        plan.Fps = 1;
                        plan.VSync = true;
                        r.Add(Loc.T("Goal.Screen60", refresh));
                    }
                    plan.LowLatency = pc.SupportsReflex;
                    if (plan.LowLatency) r.Add(Loc.T("Goal.Reflex"));
                    break;

                case Balanced:
                    plan.Lighting = 0;
                    plan.Fps = 1;
                    plan.FrameGenMode = 0;
                    plan.VSync = true;
                    plan.LowLatency = false;
                    r.Add(Loc.T("Goal.Steady60"));
                    // At 60 the card has room (GPU ~70 % on an RTX 5070): a sharper picture, one step up.
                    if (rtx && pc.RtxSeries >= 40) { plan.Upscale = 5; r.Add(Loc.T("Goal.Dlaa")); }
                    else if (rtx) { plan.Upscale = 1; r.Add(Loc.T("Goal.DlssQuality")); }
                    else { plan.Upscale = 0; r.Add(Loc.T("Goal.GameAa")); }
                    r.Add(Loc.T("Goal.NoFgBalanced"));
                    break;

                case Quiet:
                    plan.Fps = 1;
                    plan.FrameGenMode = 0;
                    plan.VSync = true;
                    plan.LowLatency = false;
                    r.Add(Loc.T("Goal.QuietRest"));
                    // One step down: fewer pixels for the card (DLSS Quality renders 2/3 of the height), lower graphics.
                    if (rtx) { plan.Upscale = 1; r.Add(Loc.T("Goal.QuietDlss")); }
                    else { plan.Upscale = 0; }
                    plan.Lighting = 1;
                    r.Add(Loc.T("Goal.QuietLow"));
                    break;
            }
            return plan;
        }

        public static void Apply(int goal, HardwareInfo pc, AppSettings s)
        {
            Apply(Make(goal, pc), s);
            s.Goal = goal;
        }

        public static void Apply(Plan plan, AppSettings s)
        {
            s.Lighting = plan.Lighting;
            s.Fps = plan.Fps;
            s.Upscale = plan.Upscale;
            s.FrameGenMode = plan.FrameGenMode;
            s.VSync = plan.VSync;
            s.LowLatency = plan.LowLatency;
        }

        static bool Matches(Plan plan, AppSettings s) =>
            s.Lighting == plan.Lighting && s.Upscale == plan.Upscale && s.FrameGenMode == plan.FrameGenMode &&
            (plan.FrameGenMode != 0 || (s.Fps == plan.Fps && s.VSync == plan.VSync));

        // The goal the current settings are (Custom when the player changed one by hand since). The goal last picked
        // first: on some PCs two goals give the same settings (a 60 Hz screen without DLSS: Smooth = Quiet).
        public static int Current(AppSettings s, HardwareInfo? pc)
        {
            if (pc == null) return Custom;
            if (s.Goal >= Smooth && s.Goal <= Quiet && Matches(Make(s.Goal, pc), s)) return s.Goal;
            foreach (var goal in new[] { Smooth, Balanced, Quiet })
                if (Matches(Make(goal, pc), s)) return goal;
            return Custom;
        }

        // The goal a new install starts with: Smooth where frame generation can fill a fast screen, else Balanced.
        public static int Recommended(HardwareInfo pc) => pc.RefreshRate >= 100 && pc.SupportsFrameGeneration ? Smooth : Balanced;
    }
}
