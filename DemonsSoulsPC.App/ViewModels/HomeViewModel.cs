using System;
using DemonsSoulsPC.App.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using DemonsSoulsPC.App.Models;
using DemonsSoulsPC.App.ViewModels;

namespace DemonsSoulsPC.App.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _mainViewModel;

        public HomeViewModel(MainWindowViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _mainViewModel.PropertyChanged += (sender, e) => OnPropertyChanged(string.Empty);
            NavigateToSettingsCommand = new RelayCommand(() => _mainViewModel.CurrentPage = LauncherPage.GraphicsSettings);
            NavigateToControllerCommand = new RelayCommand(() => _mainViewModel.CurrentPage = LauncherPage.ControllerSettings);
            NavigateToInstallationCommand = new RelayCommand(() => _mainViewModel.CurrentPage = LauncherPage.Installation);
        }

        public string GameStatus => _mainViewModel.InstallationState?.IsInstalled == true
            ? Loc.T("Installed and Ready")
            : Loc.T("Not Installed");

        public string GameVersion => _mainViewModel.InstallationState?.GameVersion ?? Loc.T("Unknown");

        public string GameTitleId => _mainViewModel.InstallationState?.GameTitleId ?? Loc.T("Unknown");

        public string CpuInfo => _mainViewModel.HardwareInfo?.CpuName ?? Loc.T("Unknown");

        public string GpuInfo => _mainViewModel.HardwareInfo?.GpuName ?? Loc.T("Unknown");

        public string RamInfo => _mainViewModel.HardwareInfo?.RamGb > 0
            ? $"{_mainViewModel.HardwareInfo.RamGb} GB"
            : Loc.T("Unknown");

        public string DisplayInfo => _mainViewModel.HardwareInfo != null
            ? $"{_mainViewModel.HardwareInfo.ScreenWidth}x{_mainViewModel.HardwareInfo.ScreenHeight}\n{_mainViewModel.HardwareInfo.RefreshRate} Hz"
            : Loc.T("Unknown");

        public string CurrentGraphicsConfig => BuildGraphicsConfigString();

        public string RayTracingToggleText => _mainViewModel.Settings.Lighting == 1 ? Loc.T("UI.RenderQuality") : Loc.T("UI.RenderPerf");

        public bool CanLaunch => _mainViewModel.InstallationState?.IsInstalled == true;

        public ICommand NavigateToSettingsCommand { get; }
        public ICommand NavigateToControllerCommand { get; }
        public ICommand NavigateToInstallationCommand { get; }
        public ICommand ToggleRayTracingCommand => new RelayCommand(() =>
        {
            _mainViewModel.Settings.Lighting = _mainViewModel.Settings.Lighting == 1 ? 0 : 1;
            OnPropertyChanged(nameof(CurrentGraphicsConfig));
            OnPropertyChanged(nameof(RayTracingToggleText));
            _mainViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Settings));
        });

        private string BuildGraphicsConfigString()
        {
            var parts = new System.Collections.Generic.List<string>();

            var sizeLabels = new[] { "1280x720", "1600x900", "1920x1080", "2560x1440" };
            if (_mainViewModel.Settings.Size >= 0 && _mainViewModel.Settings.Size < sizeLabels.Length)
                parts.Add(sizeLabels[_mainViewModel.Settings.Size]);

            parts.Add(_mainViewModel.Settings.Fullscreen ? Loc.T("Fullscreen") : Loc.T("Windowed"));

            parts.Add(_mainViewModel.Settings.Lighting == 1 ? Loc.T("UI.RenderQuality") : Loc.T("UI.RenderPerf"));

            return string.Join("  ·  ", parts);
        }
    }
}