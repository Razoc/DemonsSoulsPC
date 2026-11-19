using System;
using DemonsSoulsPC.App.Services;
using System.ComponentModel;
using DemonsSoulsPC.App.Models;

namespace DemonsSoulsPC.App.ViewModels
{
    public class AdvancedSettingsViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _mainViewModel;

        public AdvancedSettingsViewModel(MainWindowViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _mainViewModel.PropertyChanged += (sender, e) => OnPropertyChanged(string.Empty);
        }

        // Lighting/Renderer
        public string[] LightingLabels => new[] { Loc.T("OFF - Faster lighting (Non-RT, recommended)"), Loc.T("ON - Ray Tracing (Original lighting)") };

        public int LightingIndex
        {
            get => _mainViewModel.Settings.Lighting;
            set
            {
                if (_mainViewModel.Settings.Lighting != value)
                {
                    _mainViewModel.Settings.Lighting = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Game Speed
        // Game language: the console language numbers the game asks for (AnyPS5's ANYPS5_LANGUAGE).
        public string[] LanguageLabels => LanguageNames;

        public int LanguageIndex
        {
            get => _mainViewModel.Settings.Language < 0 || _mainViewModel.Settings.Language > 30 ? 0 : _mainViewModel.Settings.Language + 1;
            set
            {
                int language = value <= 0 ? -1 : value - 1;
                if (_mainViewModel.Settings.Language != language)
                {
                    _mainViewModel.Settings.Language = language;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public string LanguageNote => Loc.T("The language the game shows its text in. Auto follows your Windows language when the game has it.");

        private static readonly string[] LanguageNames =
        {
            Loc.T("Auto (Windows language)"), Loc.T("Japanese"), Loc.T("English (United States)"), Loc.T("French"), Loc.T("Spanish"), Loc.T("German"), Loc.T("Italian"), Loc.T("Dutch"),
            Loc.T("Portuguese (Portugal)"), Loc.T("Russian"), Loc.T("Korean"), Loc.T("Chinese (Traditional)"), Loc.T("Chinese (Simplified)"), Loc.T("Finnish"), Loc.T("Swedish"),
            Loc.T("Danish"), Loc.T("Norwegian"), Loc.T("Polish"), Loc.T("Portuguese (Brazil)"), Loc.T("English (United Kingdom)"), Loc.T("Turkish"), Loc.T("Spanish (Latin America)"),
            Loc.T("Arabic"), Loc.T("French (Canada)"), Loc.T("Czech"), Loc.T("Hungarian"), Loc.T("Greek"), Loc.T("Romanian"), Loc.T("Thai"), Loc.T("Vietnamese"), Loc.T("Indonesian"), Loc.T("Ukrainian"),
        };

        public string[] SpeedLabels => new[] { Loc.T("100% - real time"), "95%", "90%", "85%", "80%", Loc.T("75% (recommended)") };

        public int SpeedIndex
        {
            get => _mainViewModel.Settings.Speed;
            set
            {
                if (_mainViewModel.Settings.Speed != value)
                {
                    _mainViewModel.Settings.Speed = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public bool HangWatchdog
        {
            get => _mainViewModel.Settings.HangWatchdog;
            set
            {
                if (!Equals(_mainViewModel.Settings.HangWatchdog, value))
                {
                    _mainViewModel.Settings.HangWatchdog = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public bool ShaderCache
        {
            get => _mainViewModel.Settings.ShaderCache;
            set
            {
                if (!Equals(_mainViewModel.Settings.ShaderCache, value))
                {
                    _mainViewModel.Settings.ShaderCache = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public bool DrawPrepTuning
        {
            get => _mainViewModel.Settings.DrawPrepTuning;
            set
            {
                if (!Equals(_mainViewModel.Settings.DrawPrepTuning, value))
                {
                    _mainViewModel.Settings.DrawPrepTuning = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public string Priority
        {
            get => _mainViewModel.Settings.Priority;
            set
            {
                if (!Equals(_mainViewModel.Settings.Priority, value))
                {
                    _mainViewModel.Settings.Priority = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public string[] PriorityOptions => new[] { "normal", "abovenormal", "high" };

        public int CpPriority
        {
            get => _mainViewModel.Settings.CpPriority;
            set
            {
                if (!Equals(_mainViewModel.Settings.CpPriority, value))
                {
                    _mainViewModel.Settings.CpPriority = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public int[] CpPriorityOptions => new[] { 1, 2 };

        public bool MotionControls
        {
            get => _mainViewModel.Settings.MotionControls;
            set
            {
                if (!Equals(_mainViewModel.Settings.MotionControls, value))
                {
                    _mainViewModel.Settings.MotionControls = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public bool TriggerState
        {
            get => _mainViewModel.Settings.TriggerState;
            set
            {
                if (!Equals(_mainViewModel.Settings.TriggerState, value))
                {
                    _mainViewModel.Settings.TriggerState = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Wireless DualSense (Bluetooth HID audio/haptics) - issue #34
        public bool DualSenseWirelessSpeaker
        {
            get => _mainViewModel.Settings.DualSenseWirelessSpeaker;
            set
            {
                if (!Equals(_mainViewModel.Settings.DualSenseWirelessSpeaker, value))
                {
                    _mainViewModel.Settings.DualSenseWirelessSpeaker = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public bool DualSenseWirelessHaptics
        {
            get => _mainViewModel.Settings.DualSenseWirelessHaptics;
            set
            {
                if (!Equals(_mainViewModel.Settings.DualSenseWirelessHaptics, value))
                {
                    _mainViewModel.Settings.DualSenseWirelessHaptics = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        public bool SkipIntro
        {
            get => _mainViewModel.Settings.SkipIntro;
            set
            {
                if (!Equals(_mainViewModel.Settings.SkipIntro, value))
                {
                    _mainViewModel.Settings.SkipIntro = value;
                    OnPropertyChanged();
                    _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
                }
            }
        }

        // Experimental Features
        public bool IsExperimentalExpanded { get; set; } = false;

        // Info texts
        public string LightingNote => "Faster lighting uses the NonRT renderer (AstroBot-NonRT.exe) which disables GI probes and some lighting shaders for better performance.";

        public string SpeedNote => Loc.T("The game runs in real-time regardless of FPS. Lower values slow down the game simulation.");

        public string HangWatchdogNote => "Records diagnostics when the game hangs. Turn on when reporting a freeze; it costs about 1.8 ms per frame on RTX 50 cards, so it is off by default.";

        public string ShaderCacheNote => "Persistent shader and pipeline caches eliminate late shader compiles (100-117ms hitches). Requires title ID.";

        public string DrawPrepNote => "Raises draw-prep helper priority to match CP priority, reducing CP spin time by ~1.2ms/frame when other programs run.";

        public string PriorityNote => "Process priority (above normal recommended) and the command-processor thread priority (2 = highest) keep other programs off the reserved core.";

        public string MotionNote => "Enables accelerometer/gyroscope input for shake and tilt moves. Disable if your controller doesn't have motion sensors.";

        public string TriggerNote2 => Loc.T("Enables DualSense trigger effect state reporting. The game ignores L2/R2 without it.");

        public string DualSenseWirelessSpeakerNote => "Routes the DualSense speaker audio over Bluetooth HID. Requires a Bluetooth-connected DualSense; wired controllers use USB audio instead.";

        public string DualSenseWirelessHapticsNote => "Routes DualSense haptic feedback over Bluetooth HID. Requires a Bluetooth-connected DualSense; wired controllers use USB audio instead.";

        // Detailed overlay (moved from the Graphics page, which now has a plain FPS counter switch).
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

        public string SkipIntroNote => Loc.T("Skips the opening PlayStation Studios and Team Asobi intro videos (-skipIntro).");

        public string ExperimentalWarning => "Experimental features may cause instability. Only enable if you understand the risks and are prepared to debug issues.";
    }
}