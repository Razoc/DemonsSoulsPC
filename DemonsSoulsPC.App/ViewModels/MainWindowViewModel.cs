using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using DemonsSoulsPC.App.Models;
using DemonsSoulsPC.App.Services;

namespace DemonsSoulsPC.App.ViewModels
{
    public enum LauncherPage
    {
        Home,
        GraphicsSettings,
        ControllerSettings,
        AdvancedSettings,
        Installation
    }

    public class MainWindowViewModel : ViewModelBase
    {
        private readonly SettingsManager _settingsManager;
        private readonly InstallationManager _installationManager;
        private readonly HardwareDetector _hardwareDetector;
        private readonly ControllerDetector _controllerDetector;
        private readonly LauncherService _launcherService;
        private readonly string _rootDirectory;
        public string RootDirectory => _rootDirectory;

        public SettingsManager SettingsManager => _settingsManager;

        private LauncherPage _currentPage = LauncherPage.Home;
        private AppSettings _settings = new();
        private InstallationState? _installationState;
        private HardwareInfo? _hardwareInfo;
        private ObservableCollection<ControllerInfo> _controllers = new();
        private ControllerInfo? _selectedController;
        private bool _isLaunching;
        private bool _isLoading;
        private string _statusMessage = "";

        // Update service
        private UpdateService? _updateService;
        private UpdateState _updateState = new();
        private bool _showUpdateNotification = false;

        public static MainWindowViewModel? Current { get; private set; }

        public MainWindowViewModel(string rootDirectory)
        {
            Current = this;
            _rootDirectory = rootDirectory;
            _settingsManager = new SettingsManager(rootDirectory);
            _installationManager = new InstallationManager(rootDirectory);
            _hardwareDetector = new HardwareDetector();
            _controllerDetector = new ControllerDetector();
            _launcherService = new LauncherService(rootDirectory);
            _updateService = new UpdateService(rootDirectory);

            _installationManager.StatusChanged += msg => StatusMessage = msg;
            _installationManager.ProgressChanged += pct => { /* could show progress */ };

            _updateService.StateChanged += OnUpdateStateChanged;
            _updateService.DownloadProgressChanged += (progress, status) =>
            {
                UpdateState.DownloadProgress = progress;
                UpdateState.DownloadStatus = status;
                OnPropertyChanged(nameof(UpdateState));
            };
            _updateService.StatusChanged += msg => StatusMessage = msg;

            NavigateCommand = new RelayCommand<LauncherPage>(page => CurrentPage = page);
            LaunchGameCommand = new RelayCommand(async () => await LaunchGameAsync(), () => !IsLaunching);
            ApplyBestSettingsCommand = new RelayCommand(ApplyBestSettings);
            RefreshControllersCommand = new RelayCommand(RefreshControllers);
            RepairInstallationCommand = new RelayCommand(async () => await RepairInstallationAsync());
            RebuildInstallationCommand = new RelayCommand(async () => await RebuildInstallationAsync());
            OpenInstallFolderCommand = new RelayCommand(() => _installationManager.OpenInstallFolder());
            OpenLogFolderCommand = new RelayCommand(() => _installationManager.OpenLogFolder());
            ChangeInstallLocationCommand = new RelayCommand(async () => await ChangeInstallLocationAsync());
            UninstallCommand = new RelayCommand(async () => await UninstallAsync());

            // Update commands
            CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync(true));
            DownloadUpdateCommand = new RelayCommand(async () => await DownloadAndInstallUpdateAsync(), () => UpdateState.UpdateAvailable && !UpdateState.IsDownloading && !UpdateState.IsInstalling);
            IgnoreUpdateCommand = new RelayCommand(IgnoreUpdate);
            DismissUpdateNotificationCommand = new RelayCommand(() => ShowUpdateNotification = false);

            _ = InitializeAsync();
        }

        public override void OnPropertyChanged(string? propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName == nameof(Settings) && !IsLoading && _settingsManager != null)
            {
                try { _settingsManager.Save(Settings); } catch { }
            }
        }

        public LauncherPage CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public AppSettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        public InstallationState? InstallationState
        {
            get => _installationState;
            set => SetProperty(ref _installationState, value);
        }

        public HardwareInfo? HardwareInfo
        {
            get => _hardwareInfo;
            set => SetProperty(ref _hardwareInfo, value);
        }

        public ObservableCollection<ControllerInfo> Controllers
        {
            get => _controllers;
            set => SetProperty(ref _controllers, value);
        }

        public ControllerInfo? SelectedController
        {
            get => _selectedController;
            set => SetProperty(ref _selectedController, value);
        }

        public UpdateState UpdateState
        {
            get => _updateState;
            set => SetProperty(ref _updateState, value);
        }

        public bool ShowUpdateNotification
        {
            get => _showUpdateNotification;
            set => SetProperty(ref _showUpdateNotification, value);
        }

        public bool IsLaunching
        {
            get => _isLaunching;
            set => SetProperty(ref _isLaunching, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand LaunchGameCommand { get; }
        public ICommand ApplyBestSettingsCommand { get; }
        public ICommand RefreshControllersCommand { get; }
        public ICommand RepairInstallationCommand { get; }
        public ICommand RebuildInstallationCommand { get; }
        public ICommand OpenInstallFolderCommand { get; }
        public ICommand OpenLogFolderCommand { get; }
        public ICommand ChangeInstallLocationCommand { get; }
        public ICommand UninstallCommand { get; }

        // Update commands
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand DownloadUpdateCommand { get; }
        public ICommand IgnoreUpdateCommand { get; }
        public ICommand DismissUpdateNotificationCommand { get; }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = Loc.T("Loading...");

            try
            {
                // Load settings
                Settings = _settingsManager.Load();

                // Check installation state
                InstallationState = await _installationManager.GetInstallationStateAsync();

                // Detect hardware
                HardwareInfo = await Task.Run(() => _hardwareDetector.DetectHardware());
                // The card chosen on the Graphics page (issue #24), -1 = automatic.
                HardwareInfo.SelectGpu(Settings.Gpu);
                OnPropertyChanged(nameof(HardwareInfo));

                // Detect controllers
                await RefreshControllersAsync();

                // Apply best settings if first run
                if (Settings.ApplyBestOnStart && InstallationState?.IsInstalled == true)
                {
                    Settings.ApplyBestOnStart = false;
                    ApplyBestSettings();
                }

                StatusMessage = InstallationState?.IsInstalled == true ? Loc.T("Ready") : Loc.T("Setup required");

                // Updated from 0.1.x: the 0.2 engine is there but the game is not set up on it yet (no
                // Build\Engine\dspc-game.json). The dump is known: set it up now (a few seconds, saves brought over).
                var setup = new GameSetup(_rootDirectory);
                var features = GameFeatures.Load(GameSetup.EngineDirectory(_rootDirectory));
                if (InstallationState?.IsInstalled != true && setup.IsReleaseLayout && features == null &&
                    !string.IsNullOrEmpty(Settings.GameDumpPath) && System.IO.File.Exists(System.IO.Path.Combine(Settings.GameDumpPath, "eboot.bin")))
                {
                    StatusMessage = Loc.T("Setting the game up on the new engine...");
                    try
                    {
                        await _installationManager.RepairAsync();
                        InstallationState = await _installationManager.GetInstallationStateAsync();
                        StatusMessage = Loc.T("Ready");
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Setup failed: {ex.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }

            // Auto-check for updates in background (after UI is responsive)
            if (Settings.AutoCheckUpdates)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000); // Small delay to let UI settle
                    await _updateService?.CheckForUpdatesAsync(false);
                });
            }
        }

        private async Task LaunchGameAsync()
        {
            if (InstallationState?.IsInstalled != true)
            {
                MessageBox.Show(Loc.T("Please complete the setup first."), Loc.T("Demon’s Souls PC"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsLaunching = true;
            // The shaders first when never precompiled for this engine and driver (a window with the progress).
            if (!DemonsSoulsPC.App.Views.ShaderPrepWindow.PrepareBeforePlay(RootDirectory, Settings.GameDumpPath, Application.Current?.MainWindow))
            {
                IsLaunching = false;
                return;
            }
            StatusMessage = Loc.T("Launching Demon’s Souls...");

            try
            {
                // Determine which launcher to use based on settings
                int exitCode;
                if (Settings.FrameGenMode != 0)
                {
                    exitCode = await _launcherService.LaunchGameAsync(Settings, HardwareInfo);
                }
                else if (Settings.Upscale != 0)
                {
                    exitCode = await _launcherService.LaunchGameAsync(Settings, HardwareInfo);
                }
                else
                {
                    exitCode = await _launcherService.LaunchGameAsync(Settings, HardwareInfo);
                }

                StatusMessage = exitCode == 0 ? Loc.T("Game exited normally") : Loc.T("Game exited with code {0}", exitCode);

                // The in-game menu may have changed the settings file: take its values back.
                Settings = _settingsManager.Load();
                OnPropertyChanged(nameof(Settings));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Launch error: {ex.Message}";
                MessageBox.Show($"Failed to launch game:\n{ex.Message}", "Demon’s Souls PC", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLaunching = false;
            }
        }

        private void ApplyBestSettings()
        {
            if (HardwareInfo == null) return;

            var best = PickBestSettings(HardwareInfo);

            // Demon’s Souls PC: PS5 Performance mode rendering, 60 FPS limit, the largest 16:9 size that fits the
            // screen; no DLSS, FSR or frame generation (not tested on this game yet).
            Settings.Lighting = 0;
            Settings.Fps = 1;
            Settings.Size = best.Size;
            Settings.Fullscreen = true;
            Settings.Upscale = 0;
            Settings.FrameGenMode = 0;
            Settings.LowLatency = false;
            Settings.Controller = 0; // Auto
            Settings.RumblePercent = 70; // Medium
            Settings.Rumble = RumbleSetting.ToLegacyIndex(70);

            _settingsManager.Save(Settings);
            StatusMessage = Loc.T("Applied best settings for this PC");
        }

        private void RefreshControllers()
        {
            _ = RefreshControllersAsync();
        }

        // The controller choice stays the user's (Auto lets the game pick the pad that is pressed).
        private async Task RefreshControllersAsync()
        {
            var detected = await Task.Run(() => _controllerDetector.DetectControllers());
            Controllers.Clear();
            foreach (var c in detected)
                Controllers.Add(c);

        }

        private async Task RepairInstallationAsync()
        {
            if (string.IsNullOrEmpty(Settings.GameDumpPath))
            {
                // Installs made by the classic launcher or an older version do not know the dump folder: ask once.
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = Loc.T("Select your Demon’s Souls dump folder (the one with eboot.bin)"), ShowNewFolderButton = false })
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    var info = await new GameValidator().ValidateGameDumpAsync(dialog.SelectedPath);
                    if (!info.IsValid)
                    {
                        MessageBox.Show(info.ValidationError ?? Loc.T("That folder is not an Demon’s Souls dump."), Loc.T("Demon’s Souls PC"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    Settings.GameDumpPath = dialog.SelectedPath;
                    _settingsManager.Save(Settings);
                }
            }

            var result = MessageBox.Show(Loc.T("This will rebuild the installation from your game dump. Continue?"),
                Loc.T("Repair Installation"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusMessage = Loc.T("Repairing installation...");

            try
            {
                var success = await _installationManager.RepairAsync();
                if (success)
                {
                    InstallationState = await _installationManager.GetInstallationStateAsync();
                    StatusMessage = Loc.T("Repair complete");
                }
                else
                {
                    StatusMessage = Loc.T("Repair failed");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Repair error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RebuildInstallationAsync()
        {
            if (string.IsNullOrEmpty(Settings.GameDumpPath))
            {
                // Installs made by the classic launcher or an older version do not know the dump folder: ask once.
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = Loc.T("Select your Demon’s Souls dump folder (the one with eboot.bin)"), ShowNewFolderButton = false })
                {
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    var info = await new GameValidator().ValidateGameDumpAsync(dialog.SelectedPath);
                    if (!info.IsValid)
                    {
                        MessageBox.Show(info.ValidationError ?? Loc.T("That folder is not an Demon’s Souls dump."), Loc.T("Demon’s Souls PC"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    Settings.GameDumpPath = dialog.SelectedPath;
                    _settingsManager.Save(Settings);
                }
            }

            var result = MessageBox.Show(Loc.T("This will completely rebuild the installation. Continue?"),
                Loc.T("Rebuild Installation"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusMessage = Loc.T("Rebuilding installation...");

            try
            {
                var success = await _installationManager.RebuildAsync();
                if (success)
                {
                    InstallationState = await _installationManager.GetInstallationStateAsync();
                    StatusMessage = Loc.T("Rebuild complete");
                }
                else
                {
                    StatusMessage = Loc.T("Rebuild failed");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rebuild error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ChangeInstallLocationAsync()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Loc.T("Select new installation location"),
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                IsLoading = true;
                StatusMessage = Loc.T("Moving installation...");

                try
                {
                    await _installationManager.ChangeInstallLocationAsync(dialog.SelectedPath);
                    InstallationState = await _installationManager.GetInstallationStateAsync();
                    StatusMessage = Loc.T("Installation moved successfully");
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Move error: {ex.Message}";
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private async Task UninstallAsync()
        {
            var result = MessageBox.Show(Loc.T("This will remove all Demon’s Souls PC generated files. Your original game dump will NOT be deleted. Continue?"),
                Loc.T("Uninstall Demon’s Souls PC"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            result = MessageBox.Show(Loc.T("Are you sure? This cannot be undone."),
                Loc.T("Confirm Uninstall"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            StatusMessage = Loc.T("Uninstalling...");

            try
            {
                await _installationManager.UninstallAsync(true); // Keep game dump
                InstallationState = await _installationManager.GetInstallationStateAsync();
                Settings.FirstRunComplete = false;
                _settingsManager.Save(Settings);
                StatusMessage = Loc.T("Uninstall complete. Run setup to reinstall.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Uninstall error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Best settings logic (from AkiBot; only Size is used here)
        private (int Size, int Upscale, int FrameGen, int Target, bool LowLatency) PickBestSettings(HardwareInfo pc)
        {
            var best = (Size: 2, Upscale: 0, FrameGen: 0, Target: 2, LowLatency: false);

            for (int i = 0; i < 4; i++)
            {
                var widths = new[] { 1280, 1600, 1920, 2560 };
                var heights = new[] { 720, 900, 1080, 1440 };
                if (widths[i] <= pc.ScreenWidth && heights[i] <= pc.ScreenHeight)
                    best.Size = i;
            }

            if (pc.RtxSeries >= 20)
            {
                best.Upscale = 1;
                best.LowLatency = true;
            }

            if (pc.RtxSeries >= 40)
            {
                best.FrameGen = 1; // Auto
                int goal = pc.RtxSeries >= 50 && pc.RefreshRate >= 120 ? 120 :
                          pc.RtxSeries >= 50 && pc.RefreshRate >= 90 ? 90 : 60;
                var targetValues = new[] { 0, 50, 60, 90, 120, 144, 165 };
                best.Target = Array.IndexOf(targetValues, goal);
                if (best.Target < 0) best.Target = 2;
            }

            return best;
        }

        // Update service event handlers
        private void OnUpdateStateChanged(UpdateState state)
        {
            UpdateState = state;

            // Show notification when update becomes available
            if (state.UpdateAvailable && state.LatestUpdate != null && !state.IsChecking)
            {
                // Don't show if user dismissed or if we're currently downloading/installing
                if (!ShowUpdateNotification && !state.IsDownloading && !state.IsInstalling)
                {
                    ShowUpdateNotification = true;
                }
            }
            else if (!state.UpdateAvailable)
            {
                ShowUpdateNotification = false;
            }

            // Refresh command states
            OnPropertyChanged(nameof(UpdateState));
            (DownloadUpdateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task CheckForUpdatesAsync(bool forceCheck = false)
        {
            if (_updateService == null) return;

            if (!forceCheck && Settings.AutoCheckUpdates == false)
            {
                return;
            }

            await _updateService.CheckForUpdatesAsync(forceCheck);
        }

        private async Task DownloadAndInstallUpdateAsync()
        {
            if (_updateService == null || UpdateState.LatestUpdate == null) return;

            var updateInfo = UpdateState.LatestUpdate;

            // Download the update
            var downloadSuccess = await _updateService.DownloadUpdateAsync(updateInfo);
            if (!downloadSuccess)
            {
                return;
            }

            // Install the update
            var installSuccess = await _updateService.InstallUpdateAsync(updateInfo);
            if (installSuccess)
            {
                ShowUpdateNotification = false;
                UpdateState.UpdateAvailable = false;
                UpdateState.LatestUpdate = null;

                // Clear ignored version since we updated
                var settings = _settingsManager.Load();
                settings.IgnoredUpdateVersion = null;
                _settingsManager.Save(settings);

                // Restart the launcher
                RestartLauncher();
            }
        }

        private void IgnoreUpdate()
        {
            if (UpdateState.LatestUpdate != null)
            {
                _updateService?.IgnoreVersion(UpdateState.LatestUpdate.Version);
                ShowUpdateNotification = false;
                UpdateState.IgnoredVersion = UpdateState.LatestUpdate.Version;
            }
        }

        private void RestartLauncher()
        {
            try
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                System.Windows.Application.Current.Shutdown();
            }
            catch
            {
                // Fallback: just close
                System.Windows.Application.Current.Shutdown();
            }
        }
    }
}