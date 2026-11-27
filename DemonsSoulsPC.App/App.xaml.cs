using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using DemonsSoulsPC.App.Models;
using DemonsSoulsPC.App.Services;
using DemonsSoulsPC.App.Views;
using DemonsSoulsPC.App.ViewModels;

namespace DemonsSoulsPC.App
{
    public partial class App : Application
    {
        private SettingsManager _settingsManager = null!;
        private AppSettings _settings = null!;
        private static string? _root;

        // Started by RecompLauncher's Play with --launcher (see OnStartup).
        public static bool FromRecompLauncher { get; private set; }

        // A shader precompile never outlives the launcher that started it (an update's restart, a closed window...).
        protected override void OnExit(ExitEventArgs e)
        {
            GameSetup.StopRunningPrecompile();
            base.OnExit(e);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var userData = DspcInfo.LoadUserData();
            Loc.Init(userData.Language);
            DspcUpdater.CleanUpOldExe();
            Migration.Run(GetRootDirectory());

            // Set up global exception handling. Started with arguments (--prepare, --play... from RecompLauncher or a
            // script) there is no window and nobody to close a message box: log the error and exit.
            // DemonsSoulsPC.exe --launcher: RecompLauncher's Play (recomplauncher.json "start"). The launcher opens as
            // usual (full screen unless the player chose a window); its Play hides it, and when the game ends the
            // launcher closes with the game's exit code, so RecompLauncher sees crashes as with --play.
            FromRecompLauncher = e.Args.Length == 1 && e.Args[0] == "--launcher";
            bool headless = e.Args.Length > 0 && !FromRecompLauncher;
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogException(ex);
                if (headless) { try { Console.Error.WriteLine(ex?.Message); } catch { } Environment.Exit(1); }
                MessageBox.Show(Loc.T("Common.UnexpectedError", ex?.Message ?? ""), "Demon’s Souls PC", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                LogException(args.Exception);
                args.Handled = true;
                if (headless) { try { Console.Error.WriteLine(args.Exception.Message); } catch { } Shutdown(1); return; }
                MessageBox.Show(Loc.T("Common.UnexpectedError", args.Exception.Message), "Demon’s Souls PC", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            };

            // DemonsSoulsPC.exe --setup "<dump folder>": the setup wizard's work without a window (exit code 0 on
            // success; progress and errors in Logs\setup.log).
            if (e.Args.Length == 2 && e.Args[0] == "--setup")
            {
                RunSetupWithoutWindow(e.Args[1]);
                return;
            }
            // DemonsSoulsPC.exe --prepare "<dump folder>": for RecompLauncher (recomplauncher.json). Installs or updates the
            // runtime in this exe's folder, then sets the game up from the dump, without a window. Prints
            // "progress <0-1> <text>" lines; exit code 0 on success.
            if (e.Args.Length == 2 && e.Args[0] == "--prepare")
            {
                RunPrepareWithoutWindow(e.Args[1]);
                return;
            }
            // DemonsSoulsPC.exe --install "<folder>": the install window's work without a window (exit code 0 on
            // success; progress in <folder>\Logs\install.log). No shortcuts.
            if (e.Args.Length == 2 && e.Args[0] == "--install")
            {
                RunInstallWithoutWindow(e.Args[1]);
                return;
            }
            // DemonsSoulsPC.exe --run [game arguments]: starts the game without a window, with the runtime defaults and
            // the caller's environment (measurement scripts); exit code = the game's.
            if (e.Args.Length >= 1 && e.Args[0] == "--run")
            {
                RunGameWithoutWindow(e.Args.Skip(1).ToArray());
                return;
            }
            // DemonsSoulsPC.exe --play: what the Play button does (saved settings), without a window.
            if (e.Args.Length == 1 && e.Args[0] == "--play")
            {
                PlayWithoutWindow();
                return;
            }
            // DemonsSoulsPC.exe --precompile: the full shader precompile (about an hour, the CPU fully loaded) without a
            // window; progress lines as for --prepare, output in Logs\precompile.log; stopping it keeps what is done.
            if (e.Args.Length == 1 && e.Args[0] == "--precompile")
            {
                PrecompileWithoutWindow();
                return;
            }
            // DemonsSoulsPC.exe --translate: only the engine's shader translation (the precompile's second step), without a
            // window; exit code 0 when done. Log in Logs\precompile.log ("translation:" lines).
            if (e.Args.Length == 1 && e.Args[0] == "--translate")
            {
                TranslateWithoutWindow();
                return;
            }
            // DemonsSoulsPC.exe --remove-game: removes what the setup built (Installation page, Uninstall).
            if (e.Args.Length == 1 && e.Args[0] == "--remove-game")
            {
                RemoveGameWithoutWindow();
                return;
            }

            try
            {
                // Not inside an install folder: go to the saved install, or install now.
                if (FindInstallUpwards() == null)
                {
                    StartFromOutsideInstall(userData);
                    return;
                }
                OfferRuntimeUpdate();

                // Initialize settings
                InitializeSettings();

                // 0.2.3: every window of the launcher works with a controller (GamepadNavigator).
                GamepadNavigator.Start();
                LogDebug($"Settings loaded. FirstRunComplete: {_settings.FirstRunComplete}");

                // A game already set up in Build\RunHybrid (for example by the old launcher) is used as it
                // is: the setup wizard would rebuild it from other parts.
                if (!_settings.FirstRunComplete && ExistingInstallation())
                {
                    _settings.FirstRunComplete = true;
                    _settingsManager.Save(_settings);
                    LogDebug("Existing installation in Build\\RunHybrid found, setup skipped.");
                }

                // Show setup wizard or main window based on first run
                if (!_settings.FirstRunComplete)
                {
                    LogDebug("Showing SetupWizard...");
                    // The wizard is the only window for a while: closing it must not end the app.
                    ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    var setupWizard = new SetupWizard(GetRootDirectory());
                    var result = setupWizard.ShowDialog();
                    LogDebug($"SetupWizard closed with result: {result}");

                    // Reload settings after setup: the wizard saved them through its own manager, so read the
                    // file again instead of this manager's cached copy.
                    _settingsManager = new SettingsManager(GetRootDirectory());
                    _settings = _settingsManager.Load();
                    LogDebug($"Settings reloaded. FirstRunComplete: {_settings.FirstRunComplete}");

                    if (_settings.FirstRunComplete)
                    {
                        LogDebug("Showing MainWindow...");
                        ShowMainWindow();
                    }
                    else
                    {
                        LogDebug("Setup not complete, shutting down.");
                        Shutdown(0);
                    }
                }
                else
                {
                    LogDebug("First run complete, showing MainWindow...");
                    ShowMainWindow();
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show(Loc.T("Common.StartupError", ex.ToString()), "Demon’s Souls PC", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        // DemonsSoulsPC.exe started outside an install folder (for example from Downloads).
        private void StartFromOutsideInstall(DspcInfo.UserData userData)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var installDir = DspcInfo.IsInstallFolder(userData.InstallDir) ? userData.InstallDir : null;
            if (installDir == null)
            {
                string? folder = null;
                while (true)
                {
                    var window = new InstallWindow { StartFolder = folder };
                    window.ShowDialog();
                    if (window.LanguageChanged) { folder = window.ChosenFolder; continue; }
                    installDir = window.InstalledDirectory;
                    break;
                }
                if (installDir == null) { Shutdown(0); return; }
            }
            else
            {
                // Already installed: a newer DemonsSoulsPC.exe replaces the installed one.
                var installed = Path.Combine(installDir, DspcInfo.ExeName);
                try
                {
                    var installedVersion = File.Exists(installed) ? AssemblyName.GetAssemblyName(installed).Version : null;
                    if (installedVersion == null || installedVersion < DspcInfo.CurrentVersion)
                        DspcInstaller.CopySelf(installDir);
                }
                catch { }
            }
            Process.Start(new ProcessStartInfo(Path.Combine(installDir, DspcInfo.ExeName)) { UseShellExecute = true, WorkingDirectory = installDir });
            Shutdown(0);
        }

        // An install whose runtime pin changed offers to update it; the setup wizard then rebuilds the game.
        private void OfferRuntimeUpdate()
        {
            var root = GetRootDirectory();
            var installed = DspcInfo.InstalledRuntimeVersion(root);
            if (installed == "" || installed == DspcInfo.RuntimeVersion)
                return;
            if (MessageBox.Show(Loc.T("Runtime.UpdateAvailable", DspcInfo.RuntimeVersion, installed), "Demon’s Souls PC",
                    MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
                return;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new InstallWindow(root);
            if (window.ShowDialog() != true)
                return; // keep the current runtime
            InitializeSettings();
            // A known dump is set up on the new runtime by the main window (MainWindowViewModel); otherwise the wizard asks for it.
            if (string.IsNullOrEmpty(_settings.GameDumpPath) || !File.Exists(Path.Combine(_settings.GameDumpPath, "eboot.bin")))
            {
                _settings.FirstRunComplete = false;
                _settingsManager.Save(_settings);
            }
        }

        private async void PrecompileWithoutWindow()
        {
            var root = GetRootDirectory();
            StreamWriter? output = null;
            try { output = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true }; }
            catch { }
            try
            {
                InitializeSettings();
                var dump = _settings.GameDumpPath ?? GameFeatures.Load(GameSetup.EngineDirectory(root))?.Dump;
                if (string.IsNullOrEmpty(dump) || !File.Exists(Path.Combine(dump, "eboot.bin")))
                    throw new DirectoryNotFoundException("The game folder (with eboot.bin) is not set up.");
                var progress = new Progress<(int Percent, string Message)>(p =>
                {
                    if (p.Percent < 0) return;
                    try { output?.WriteLine(FormattableString.Invariant($"progress {p.Percent / 100.0:0.00} {p.Message}")); } catch { }
                });
                var code = await GameSetup.PrecompileAsync(root, dump!, progress);
                Shutdown(code);
            }
            catch (Exception ex)
            {
                try { Console.Error.WriteLine(ex.Message); } catch { }
                ShowVersionRefusal(ex);
                Shutdown(2);
            }
        }

        private async Task CheckForDspcUpdateAsync()
        {
            try
            {
                var release = await DspcUpdater.CheckAsync();
                if (release == null) return;
                if (MessageBox.Show(Loc.T("Update.AskInstall", release.VersionText, DspcInfo.CurrentVersionText), "Demon’s Souls PC",
                        MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes)
                    return;
                await DspcUpdater.InstallAndRestartAsync(release, null);
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show(Loc.T("Update.Failed", ex.Message), "Demon’s Souls PC", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LogDebug(string message)
        {
            try
            {
                var logPath = Path.Combine(GetRootDirectory(), "Logs", "dspc-debug.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                Services.GameSetup.AppendLog(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        private async void RunSetupWithoutWindow(string dump)
        {
            var root = GetRootDirectory();
            var log = Path.Combine(root, "Logs", "setup.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                var progress = new Progress<(int Percent, string Message)>(p =>
                    Services.GameSetup.AppendLog(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {p.Percent,3}% {p.Message}\r\n"));
                var features = await new GameSetup(root).RunAsync(dump, progress);
                InitializeSettings();
                _settings.FirstRunComplete = true;
                _settings.ApplyBestOnStart = true;
                _settings.GameDumpPath = dump;
                _settings.GameVersion = features.Version;
                _settings.GameTitleId = features.TitleId;
                _settingsManager.Save(_settings);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                Services.GameSetup.AppendLog(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FAILED: {ex}\r\n");
                ShowVersionRefusal(ex);
                Shutdown(2);
            }
        }

        private async void RunPrepareWithoutWindow(string dump)
        {
            var root = GetRootDirectory();
            var log = Path.Combine(root, "Logs", "setup.log");
            StreamWriter? output = null;
            try { output = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true }; }
            catch { }
            void Print(double share, string text)
            {
                try { output?.WriteLine(FormattableString.Invariant($"progress {share:0.00} {text}")); }
                catch { }
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                var last = "";
                IProgress<(int Percent, string Message)> Part(double from, double to) =>
                    new Progress<(int Percent, string Message)>(p =>
                    {
                        if (p.Message == last) return;
                        last = p.Message;
                        Print(from + (to - from) * Math.Max(0, Math.Min(100, p.Percent)) / 100.0, p.Message);
                        Services.GameSetup.AppendLog(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {p.Percent,3}% {p.Message}\r\n");
                    });

                if (!DspcInfo.IsInstallFolder(root) || DspcInfo.InstalledRuntimeVersion(root) != DspcInfo.RuntimeVersion)
                    await DspcInstaller.InstallRuntimeAsync(root, Part(0, 0.6), System.Threading.CancellationToken.None);
                var features = await new GameSetup(root).RunAsync(dump, Part(0.6, 1));
                InitializeSettings();
                _settings.FirstRunComplete = true;
                _settings.GameDumpPath = dump;
                _settings.GameVersion = features.Version;
                _settings.GameTitleId = features.TitleId;
                _settingsManager.Save(_settings);
                Print(1, "Demon’s Souls PC " + DspcInfo.CurrentVersionText);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                try { Services.GameSetup.AppendLog(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FAILED: {ex}\r\n"); } catch { }
                try { Console.Error.WriteLine(ex.Message); } catch { }
                ShowVersionRefusal(ex);
                Shutdown(2);
            }
        }

        private async void RunInstallWithoutWindow(string folder)
        {
            var dir = Path.GetFullPath(folder);
            var log = Path.Combine(dir, "Logs", "install.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                var last = "";
                var progress = new Progress<(int Percent, string Message)>(p =>
                {
                    if (p.Message == last) return;
                    last = p.Message;
                    Services.GameSetup.AppendLog(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {p.Percent,3}% {p.Message}\r\n");
                });
                await DspcInstaller.InstallRuntimeAsync(dir, progress, System.Threading.CancellationToken.None);
                DspcInstaller.CopySelf(dir);
                var data = DspcInfo.LoadUserData();
                data.InstallDir = dir;
                DspcInfo.SaveUserData(data);
                Services.GameSetup.AppendLog(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] done: runtime {DspcInfo.RuntimeVersion}\r\n");
                Shutdown(0);
            }
            catch (Exception ex)
            {
                try { Services.GameSetup.AppendLog(log, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] FAILED: {ex}\r\n"); } catch { }
                ShowVersionRefusal(ex);
                Shutdown(2);
            }
        }

        private async void PlayWithoutWindow()
        {
            try
            {
                var root = GetRootDirectory();
                var settings = new SettingsManager(root).Load();
                // The shaders first when never precompiled for this engine and driver (a window with the progress).
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var dump = settings.GameDumpPath ?? GameFeatures.Load(GameSetup.EngineDirectory(root))?.Dump;
                if (!Views.ShaderPrepWindow.PrepareBeforePlay(root, dump)) { Shutdown(0); return; }
                Shutdown(await new LauncherService(root).LaunchGameAsync(settings));
            }
            catch (Exception ex)
            {
                LogException(ex);
                ShowVersionRefusal(ex);
                Shutdown(2);
            }
        }

        private async void TranslateWithoutWindow()
        {
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var ok = await new LauncherService(GetRootDirectory()).PrefetchShadersAsync(null, System.Threading.CancellationToken.None);
                Shutdown(ok ? 0 : 1);
            }
            catch (Exception ex)
            {
                LogException(ex);
                ShowVersionRefusal(ex);
                Shutdown(2);
            }
        }

        private async void RunGameWithoutWindow(string[] gameArgs)
        {
            try
            {
                Shutdown(await new LauncherService(GetRootDirectory()).RunWithDefaultsAsync(gameArgs));
            }
            catch (Exception ex)
            {
                LogException(ex);
                ShowVersionRefusal(ex);
                Shutdown(2);
            }
        }

        private async void RemoveGameWithoutWindow()
        {
            try
            {
                await new InstallationManager(GetRootDirectory()).UninstallAsync(true);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                LogException(ex);
                ShowVersionRefusal(ex);
                Shutdown(2);
            }
        }

        private static bool ExistingInstallation()
        {
            var engine = GameSetup.EngineDirectory(GetRootDirectory());
            return File.Exists(Path.Combine(engine, GameSetup.GameExe)) && File.Exists(Path.Combine(engine, GameFeatures.FileName));
        }

        private void InitializeSettings()
        {
            var rootDir = GetRootDirectory();
            Directory.CreateDirectory(Path.Combine(rootDir, "Build"));

            _settingsManager = new SettingsManager(rootDir);
            _settings = _settingsManager.Load();
        }

        private void ShowMainWindow()
        {
            var mainWindow = new MainWindow();
            var rootDir = GetRootDirectory();
            mainWindow.DataContext = new MainWindowViewModel(rootDir);
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            _ = CheckForDspcUpdateAsync();
        }

        private void InitializeApplication()
        {
            // Ensure required directories exist
            var rootDir = GetRootDirectory();
            var dirs = new[]
            {
                Path.Combine(rootDir, "Build"),
                Path.Combine(rootDir, "Logs"),
                Path.Combine(rootDir, "Build", "launcher")
            };

            foreach (var dir in dirs)
            {
                Directory.CreateDirectory(dir);
            }
        }

        // The game folder: the first folder from the exe upwards that holds an Demon’s Souls PC install, so the
        // app also works from its own build folder; else the exe's folder.
        private static string GetRootDirectory()
        {
            return _root ??= FindInstallUpwards() ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        }

        private static string? FindInstallUpwards()
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            for (var dir = new DirectoryInfo(exeDir); dir != null; dir = dir.Parent)
            {
                if (Services.DspcInfo.IsInstallFolder(dir.FullName))
                {
                    return dir.FullName;
                }
            }
            return null;
        }

        // The game version refusal (GameFeatures.EnsureSupportedVersion) is shown even without a window: RecompLauncher's
        // --prepare and --play would otherwise end with only a log line.
        private static void ShowVersionRefusal(Exception ex)
        {
            if (ex is UnsupportedGameVersionException)
                try { MessageBox.Show(ex.Message, "Demon’s Souls PC", MessageBoxButton.OK, MessageBoxImage.Warning); } catch { }
        }

        private void LogException(Exception? ex)
        {
            try
            {
                var logPath = Path.Combine(GetRootDirectory(), "Logs", "dspc-error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                Services.GameSetup.AppendLog(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
            catch { }
        }
    }
}