using System;
using DemonsSoulsPC.App.Services;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using DemonsSoulsPC.App.Models;

namespace DemonsSoulsPC.App.ViewModels
{
    public class GraphicsSettingsViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _mainViewModel;

        public GraphicsSettingsViewModel(MainWindowViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _mainViewModel.PropertyChanged += (sender, e) => OnPropertyChanged(string.Empty);
        }

        // Goal (0.2.2, as in Bloodborne PC): Custom / Smooth / Balanced / Quiet. The shown goal is the one the settings
        // are now (Custom once one was changed by hand); picking one sets the Graphics page for this PC (GoalPlanner).
        public string[] GoalLabels => new[] { Loc.T("Goal.Custom"), Loc.T("Goal.Smooth"), Loc.T("Goal.Balanced"), Loc.T("Goal.Quiet") };

        public int GoalIndex
        {
            get => GoalPlanner.Current(_mainViewModel.Settings, _mainViewModel.HardwareInfo);
            set
            {
                var pc = _mainViewModel.HardwareInfo;
                if (pc == null || value < GoalPlanner.Smooth || value > GoalPlanner.Quiet || value == GoalIndex) return;
                GoalPlanner.Apply(value, pc, _mainViewModel.Settings);
                _mainViewModel.StatusMessage = Loc.T("Goal.Applied", GoalLabels[value]);
                OnPropertyChanged(string.Empty);
                _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
            }
        }

        // Why each setting of the goal (the goal's row tooltip).
        public string GoalNote
        {
            get
            {
                var pc = _mainViewModel.HardwareInfo;
                int goal = GoalIndex;
                if (pc == null || goal == GoalPlanner.Custom) return Loc.T("Goal.CustomNote");
                return "• " + string.Join("\n• ", GoalPlanner.Make(goal, pc).Reasons);
            }
        }

        // Resolution
        // Indices 0..3 = 16:9, indices 4..5 = 21:9 ultrawide (#58)
        public string[] ResolutionLabels => new[] { "1280 x 720", "1600 x 900", "1920 x 1080", "2560 x 1440", "2560 x 1080 (21:9)", "3440 x 1440 (21:9)" };

        public int ResolutionIndex
        {
            get => _mainViewModel.Settings.Size;
            set
            {
                if (_mainViewModel.Settings.Size != value)
                {
                    _mainViewModel.Settings.Size = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Graphics card (issue #24). A laptop with hybrid graphics reports two adapters, and the one
        // that drives the built-in screen is usually the integrated GPU: the launcher must describe
        // (and enable NVIDIA features for) a card the user can pick, not just the first adapter.
        public bool HasGpuChoice => (_mainViewModel.HardwareInfo?.Gpus.Count ?? 0) > 1;

        public string[] GpuLabels
        {
            get
            {
                var labels = new List<string> { Loc.T("Automatic - best graphics card") };
                foreach (var gpu in _mainViewModel.HardwareInfo?.Gpus ?? new List<GpuAdapter>())
                    labels.Add(gpu.Label);
                return labels.ToArray();
            }
        }

        public int GpuIndex
        {
            get => (_mainViewModel.HardwareInfo?.SelectedGpuIndex ?? -1) + 1;
            set
            {
                var hardware = _mainViewModel.HardwareInfo;
                var selected = value - 1;
                if (hardware == null || selected < -1 || selected >= hardware.Gpus.Count || hardware.SelectedGpuIndex == selected)
                    return;

                hardware.SelectGpu(selected);
                _mainViewModel.Settings.Gpu = selected;

                // DLSS and frame generation need the NVIDIA card: a card without them turns the
                // options off instead of leaving settings the launcher cannot honour.
                if (!hardware.SupportsDlss && _mainViewModel.Settings.Upscale >= 1 && _mainViewModel.Settings.Upscale <= 5)
                    _mainViewModel.Settings.Upscale = 0;
                if (_mainViewModel.Settings.FrameGenMode >= 1 && _mainViewModel.Settings.FrameGenMode <= 3 &&
                    (!hardware.SupportsFrameGeneration || _mainViewModel.Settings.FrameGenMode + 1 > hardware.SelectedGpu?.MaxFrameGenMultiplier))
                    _mainViewModel.Settings.FrameGenMode = 0;

                OnPropertyChanged();
                OnPropertyChanged(nameof(GpuNote));
                OnPropertyChanged(nameof(HardwareNote));
                OnPropertyChanged(nameof(UpscaleIndex));
                OnPropertyChanged(nameof(FrameGenIndex));
                OnPropertyChanged(nameof(SupportsDlss));
                OnPropertyChanged(nameof(SupportsReflex));
                OnPropertyChanged(nameof(IsFrameGenAvailable));
                _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.HardwareInfo));
                _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
            }
        }

        public string GpuNote
        {
            get
            {
                var hardware = _mainViewModel.HardwareInfo;
                if (hardware == null || hardware.Gpus.Count == 0)
                    return Loc.T("No graphics card detected.");

                var detected = string.Join(", ", hardware.Gpus.Select(g =>
                    $"{g.Name} ({(g.IsDiscrete ? "discrete" : g.IsIntegrated ? "integrated" : "graphics")})"));
                var text = Loc.T("Detected: ") + detected + ".";

                if (hardware.SelectedGpuIndex < 0)
                    text += Loc.T(" Automatic uses the discrete card: the renderer prefers one over integrated graphics.");
                else
                    text += Loc.T(" The choice is passed to the game as ANYPS5_GPU; the renderer also prefers a discrete card by itself.");

                if (hardware.SelectedGpu?.RtxSeries >= 20)
                    text += Loc.T(" DLSS, Reflex and frame generation use this card.");
                else if (hardware.Gpus.Any(g => g.RtxSeries >= 20))
                    text += Loc.T(" The NVIDIA options need the RTX card: pick it, or Automatic, to use them.");
                return text;
            }
        }

        // Fullscreen
        public bool Fullscreen
        {
            get => _mainViewModel.Settings.Fullscreen;
            set
            {
                if (_mainViewModel.Settings.Fullscreen != value)
                {
                    _mainViewModel.Settings.Fullscreen = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Ultrawide render-space patch (issue #29/#33/#58).
        public bool Ultrawide
        {
            get => _mainViewModel.Settings.Ultrawide;
            set
            {
                if (_mainViewModel.Settings.Ultrawide != value)
                {
                    _mainViewModel.Settings.Ultrawide = value;
                    if (value && _mainViewModel.Settings.Size < 4)
                    {
                        _mainViewModel.Settings.Size = _mainViewModel.Settings.Size == 3 ? 5 : 4;
                    }
                    else if (!value && _mainViewModel.Settings.Size >= 4)
                    {
                        _mainViewModel.Settings.Size = _mainViewModel.Settings.Size == 5 ? 3 : 2;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ResolutionIndex));
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public string UltrawideNote => "Uses the 21:9 game patch and 21:9 window dimensions (2560x1080 / 3440x1440) when enabled; 16:9 remains unchanged.";

        // Demon’s Souls PC: rendering quality (Settings.Lighting): 0 = the PS5 Performance mode settings, 1 = the
        // game's own settings (4K Fidelity mode), applied to copies of misc\renderconfig_ps5_*.txt at each launch.
        public string[] RayTracingLabels => new[] { Loc.T("UI.RenderPerf"), Loc.T("UI.RenderQuality"), Loc.T("UI.RenderLowest") };

        public int RayTracingIndex
        {
            get => _mainViewModel.Settings.Lighting;
            set
            {
                if (_mainViewModel.Settings.Lighting != value)
                {
                    _mainViewModel.Settings.Lighting = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RayTracingNote));
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public string RayTracingNote => _mainViewModel.Settings.Lighting >= 1
            ? Loc.T("UI.RenderQualityTip")
            : Loc.T("UI.RenderPerfTip");

        // NVIDIA frame generation presents the frames itself, and its VSync works only on D3D12 (Streamline
        // DLSS-G guide, 22.5); this port draws with Vulkan, so VSync applies only with frame generation off.
        public bool IsVSyncAvailable => _mainViewModel.Settings.FrameGenMode == 0;
        public string VSyncNote => IsVSyncAvailable
            ? Loc.T("Locks presentation to your screen's refresh (no tearing).")
            : Loc.T("UI.VSyncNoFg");

        // VSync
        public bool VSync
        {
            get => _mainViewModel.Settings.VSync;
            set
            {
                if (_mainViewModel.Settings.VSync != value)
                {
                    _mainViewModel.Settings.VSync = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // With frame generation on, the target sets the game's own limit (as in the classic launcher).
        public bool IsFpsLimitEnabled => _mainViewModel.Settings.FrameGenMode == 0;
        public string FpsLimitNote => IsFpsLimitEnabled
            ? Loc.T("The game may run below the selected limit in demanding scenes.")
            : Loc.T("UI.FpsLimitFg");

        // FPS Limiter
        public string[] FpsLabels => new[] { "30 FPS", Loc.T("UI.Fps60Recommended"), "120 FPS" };

        public int FpsIndex
        {
            get => _mainViewModel.Settings.Fps;
            set
            {
                if (_mainViewModel.Settings.Fps != value)
                {
                    _mainViewModel.Settings.Fps = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Upscaling
        // Demon's Souls PC 0.2.2: 0 the game's own (its TAA), 1-5 NVIDIA DLSS super resolution (RTX), 6 AMD FSR 3 (any card).
        public string[] UpscaleLabels => new[] { Loc.T("UI.UpscaleGame"), Loc.T("UI.DlssQuality"), Loc.T("UI.DlssBalanced"),
                                                 Loc.T("UI.DlssPerformance"), Loc.T("UI.DlssUltra"), "NVIDIA DLAA", "AMD FSR 3" };

        public string UpscaleNote => UpscaleIndex switch
        {
            0 => Loc.T("UI.UpscaleGameNote"),
            6 => Loc.T("UI.UpscaleFsrNote"),
            _ => Loc.T("UI.UpscaleDlssNote", new[] { "", "67 %", "58 %", "50 %", "33 %", "100 %" }[Math.Min(5, Math.Max(1, UpscaleIndex))])
                 + (SupportsDlss ? "" : " " + Loc.T("UI.UpscaleDlssNotHere")),
        };

        public int UpscaleIndex
        {
            get => _mainViewModel.Settings.Upscale;
            set
            {
                if (_mainViewModel.Settings.Upscale != value)
                {
                    _mainViewModel.Settings.Upscale = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(UpscaleNote));
                    OnPropertyChanged(nameof(IsFrameGenAvailable));
                    OnPropertyChanged(nameof(FrameGenNote));
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Frame Generation
        // Demon's Souls PC 0.2.2: the engine's own frame generation. 1-3 = NVIDIA DLSS frame generation with 1-3 generated
        // frames (run-windows -FrameGen n, Streamline runtime in Build\Engine\streamline; RTX 40 for 2x, RTX 50 up to 4x),
        // 4 = AMD FSR 3 frame generation (-Fsr, amd_fidelityfx_vk.dll next to the engine; any card).
        public string[] FrameGenLabels => new[] { Loc.T("Off (default)"), Loc.T("UI.DlssFg2"), Loc.T("UI.DlssFg3"), Loc.T("UI.DlssFg4"), Loc.T("UI.FsrFg") };

        public int FrameGenIndex
        {
            get => _mainViewModel.Settings.FrameGenMode;
            set
            {
                if (_mainViewModel.Settings.FrameGenMode != value)
                {
                    _mainViewModel.Settings.FrameGenMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFrameGenTargetEnabled));
                    OnPropertyChanged(nameof(FrameGenNote));
                    OnPropertyChanged(nameof(IsFpsLimitEnabled));
                    OnPropertyChanged(nameof(IsVSyncAvailable));
                    OnPropertyChanged(nameof(VSyncNote));
                    OnPropertyChanged(nameof(FpsLimitNote));
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Frame Gen Target
        public string[] FrameGenTargetLabels => new[] { Loc.T("No lock - game as fast as it can"), Loc.T("50 FPS on screen"), Loc.T("60 FPS on screen (recommended)"), Loc.T("90 FPS on screen"), Loc.T("120 FPS on screen"), Loc.T("144 FPS on screen"), Loc.T("165 FPS on screen") };

        public int FrameGenTargetIndex
        {
            get => _mainViewModel.Settings.FrameGenTarget;
            set
            {
                if (_mainViewModel.Settings.FrameGenTarget != value)
                {
                    _mainViewModel.Settings.FrameGenTarget = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FrameGenNote));
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // NVIDIA Reflex
        public bool LowLatency
        {
            get => _mainViewModel.Settings.LowLatency;
            set
            {
                if (_mainViewModel.Settings.LowLatency != value)
                {
                    _mainViewModel.Settings.LowLatency = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // FPS counter (Demon’s Souls PC 0.2.1): a plain switch for the overlay's "FPS only" mode. Turning it on keeps a
        // more detailed overlay already chosen on the Advanced page.
        public bool FpsCounter
        {
            get => _mainViewModel.Settings.Overlay != 0;
            set
            {
                int wanted = value ? (_mainViewModel.Settings.Overlay == 0 ? 1 : _mainViewModel.Settings.Overlay) : 0;
                if (_mainViewModel.Settings.Overlay != wanted)
                {
                    _mainViewModel.Settings.Overlay = wanted;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OverlayIndex));
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Performance Overlay
        public string[] OverlayLabels => new[] { Loc.T("UI.OverlayOff"), Loc.T("UI.OverlayFps"), Loc.T("UI.OverlayCompact"), Loc.T("UI.OverlayFull") };

        public int OverlayIndex
        {
            get => _mainViewModel.Settings.Overlay;
            set
            {
                if (_mainViewModel.Settings.Overlay != value)
                {
                    _mainViewModel.Settings.Overlay = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Computed properties for UI state
        // NVIDIA modes need an RTX 40+ card and an upscaler; AMD FSR 3 (index 5) works on every card.
        public bool IsFrameGenAvailable => true;

        public bool IsFrameGenTargetEnabled => FrameGenIndex != 0;

        public bool SupportsDlss => _mainViewModel.HardwareInfo?.SupportsDlss == true;

        public bool SupportsReflex => _mainViewModel.HardwareInfo?.SupportsReflex == true;

        public string FrameGenNote
        {
            get
            {
                if (FrameGenIndex == 0)
                    return Loc.T("UI.FgOffDs");
                // The game's rate follows the screen (LauncherService.FrameGenGameRate): game rate x frames = shown.
                int refresh = _mainViewModel.HardwareInfo?.RefreshRate ?? 60;
                int mult = LauncherService.FrameGenFactor(FrameGenIndex);
                int rate = LauncherService.FrameGenGameRate(FrameGenIndex, refresh);
                if (rate == 0) rate = 60;
                string pace = " " + Loc.T("UI.FgPace", rate, rate * mult, refresh);
                if (FrameGenIndex == 4)
                    return Loc.T("UI.FgFsrNote") + pace;
                var max = _mainViewModel.HardwareInfo?.SelectedGpu?.MaxFrameGenMultiplier ?? 0;
                return Loc.T("UI.FgDlssNote", mult) + pace + (max < mult ? " " + Loc.T("UI.FgDlssNotHere") : "");
            }
        }

        public string HardwareNote
        {
            get
            {
                if (_mainViewModel.HardwareInfo == null) return "";

                var gpu = _mainViewModel.HardwareInfo.GpuName;
                if (_mainViewModel.HardwareInfo.RtxSeries >= 50)
                    return $"{gpu}: DLSS + Multi Frame Gen (up to 4x) + Reflex available";
                else if (_mainViewModel.HardwareInfo.RtxSeries >= 40)
                    return $"{gpu}: DLSS + Frame Gen (2x) + Reflex available";
                else if (_mainViewModel.HardwareInfo.RtxSeries >= 20)
                    return $"{gpu}: DLSS + Reflex available (Frame Gen needs RTX 40+)";
                else
                    return $"{gpu}: Using game's own upscaling (NVIDIA features need RTX 20+)";
            }
        }
    }
}
