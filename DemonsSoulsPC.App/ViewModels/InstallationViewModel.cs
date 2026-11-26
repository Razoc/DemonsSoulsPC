using System;
using DemonsSoulsPC.App.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using DemonsSoulsPC.App.Models;
using DemonsSoulsPC.App.ViewModels;

namespace DemonsSoulsPC.App.ViewModels
{
    public class InstallationViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _mainViewModel;

        public InstallationViewModel(MainWindowViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _mainViewModel.PropertyChanged += (sender, e) => OnPropertyChanged(string.Empty);
            RepairCommand = new RelayCommand(() => _mainViewModel.RepairInstallationCommand.Execute(null));
            // 0.2: nothing to rebuild; the button precompiles the shaders instead.
            RebuildCommand = new RelayCommand(Precompile);
            ChangeLocationCommand = new RelayCommand(() => _mainViewModel.ChangeInstallLocationCommand.Execute(null));
            OpenInstallFolderCommand = new RelayCommand(() => _mainViewModel.OpenInstallFolderCommand.Execute(null));
            OpenLogFolderCommand = new RelayCommand(() => _mainViewModel.OpenLogFolderCommand.Execute(null));
            UninstallCommand = new RelayCommand(() => _mainViewModel.UninstallCommand.Execute(null));
            RunSetupAgainCommand = new RelayCommand(RunSetupAgain);
        }

        public InstallationState? State => _mainViewModel.InstallationState;

        public string InstallPath => _mainViewModel.InstallationState?.InstallPath ?? Loc.T("Not installed");

        public string GameDumpPath => _mainViewModel.InstallationState?.GameDumpPath ?? Loc.T("Not configured");

        public string GameVersion => _mainViewModel.InstallationState?.GameVersion ?? Loc.T("Unknown");

        public string GameTitleId => _mainViewModel.InstallationState?.GameTitleId ?? Loc.T("Unknown");

        public string InstalledAt => _mainViewModel.InstallationState?.InstalledAt?.ToString("yyyy-MM-dd HH:mm") ?? Loc.T("Never");

        public string InstallSize => _mainViewModel.InstallationState?.InstallSizeBytes > 0
            ? FormatBytes(_mainViewModel.InstallationState.InstallSizeBytes)
            : Loc.T("Unknown");

        public bool IsInstalled => _mainViewModel.InstallationState?.IsInstalled == true;

        public string AnyPs5Version => _mainViewModel.Settings.InstallationManifest?.AnyPs5Version ?? Loc.T("Unknown");

        public string KytyVersion => _mainViewModel.Settings.InstallationManifest?.KytyVersion ?? Loc.T("Unknown");

        public ICommand RepairCommand { get; }
        public ICommand RebuildCommand { get; }
        public ICommand ChangeLocationCommand { get; }
        public ICommand OpenInstallFolderCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand RunSetupAgainCommand { get; }

        // The full shader precompile (about an hour, the CPU fully loaded), asked by the player (0.2.5): the launcher
        // closes and only the preparation window stays, as before the first Play; closing it stops the run (what is
        // done is kept, the next run continues).
        private void Precompile()
        {
            var dump = _mainViewModel.Settings.GameDumpPath;
            if (string.IsNullOrEmpty(dump) || !System.IO.File.Exists(System.IO.Path.Combine(dump, "eboot.bin")))
            {
                MessageBox.Show(Loc.T("The game folder (with eboot.bin) was not found: choose it again in the settings."), "Demon’s Souls PC");
                return;
            }
            Application.Current.MainWindow?.Hide();
            Views.ShaderPrepWindow.PrepareNow(_mainViewModel.RootDirectory, dump!);
            Application.Current.Shutdown(0);
        }

        private void RunSetupAgain()
        {
            _mainViewModel.Settings.FirstRunComplete = false;
            _mainViewModel.SettingsManager.Save(_mainViewModel.Settings);
            _mainViewModel.CurrentPage = LauncherPage.Home;
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double size = bytes;
            while (size >= 1024 && i < suffixes.Length - 1)
            {
                size /= 1024;
                i++;
            }
            return $"{size:F1} {suffixes[i]}";
        }
    }
}