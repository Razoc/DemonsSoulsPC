using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DemonsSoulsPC.App.Services;

namespace DemonsSoulsPC.App.Views
{
    // First run: language, install folder, shortcuts; then downloads and installs the runtime.
    // With runtimeOnly (an existing install whose runtime pin changed) it goes straight to the download.
    public partial class InstallWindow : Window
    {
        private readonly bool _runtimeOnly;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private bool _busy;

        // The folder Demon’s Souls PC ended up installed in (null if cancelled or failed).
        public string? InstalledDirectory { get; private set; }

        public InstallWindow(string? runtimeOnlyDir = null)
        {
            InitializeComponent();
            _runtimeOnly = runtimeOnlyDir != null;
            FolderBox.Text = runtimeOnlyDir ?? DspcInfo.DefaultInstallDirectory();
            FolderHint.Text = Loc.T("Install.FolderHint", DspcInfo.FormatBytes(DspcInfo.NeededBytes));
            LanguageBox.SelectedIndex = Loc.Language == Loc.French ? 0 : 1;
            UpdateFreeSpace();
            Closing += (s, e) =>
            {
                if (_busy) { e.Cancel = true; _cancel.Cancel(); }
            };
            if (_runtimeOnly)
                Loaded += (s, e) => Install_Click(this, new RoutedEventArgs());
        }

        private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            var lang = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string;
            var data = DspcInfo.LoadUserData();
            data.Language = lang;
            DspcInfo.SaveUserData(data);
            if (lang != Loc.Language)
            {
                // App reopens this window in the chosen language, keeping the folder.
                Loc.Init(lang);
                LanguageChanged = true;
                DialogResult = false;
            }
        }

        // Closed only to reopen in another language (App shows a fresh window with ChosenFolder).
        public bool LanguageChanged { get; private set; }
        public string ChosenFolder => FolderBox.Text;
        public string? StartFolder { set { if (value != null) FolderBox.Text = value; } }

        private void FolderBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateFreeSpace();

        private void UpdateFreeSpace()
        {
            if (FreeSpaceText == null) return;
            var free = DspcInstaller.FreeSpace(FolderBox.Text);
            FreeSpaceText.Text = free >= 0 ? Loc.T("Install.FreeSpace", DspcInfo.FormatBytes(free)) : "";
            FreeSpaceText.Foreground = free >= 0 && free < DspcInfo.NeededBytes
                ? (System.Windows.Media.Brush)FindResource("ErrorBrush")
                : (System.Windows.Media.Brush)FindResource("TextMutedBrush");
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Loc.T("Install.PickFolder"),
                ShowNewFolderButton = true,
            };
            var parent = Path.GetDirectoryName(FolderBox.Text);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)) dialog.SelectedPath = parent;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Picking a parent folder installs into an "Demon’s Souls PC" folder inside it, unless it is empty or already Demon’s Souls PC.
                var picked = dialog.SelectedPath;
                var empty = !Directory.EnumerateFileSystemEntries(picked).Any();
                FolderBox.Text = empty || Path.GetFileName(picked).Equals("DemonsSoulsPC", StringComparison.OrdinalIgnoreCase)
                    ? picked : Path.Combine(picked, "DemonsSoulsPC");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) { _cancel.Cancel(); return; }
            DialogResult = false;
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var dir = FolderBox.Text.Trim();
            try { dir = Path.GetFullPath(dir); }
            catch (Exception ex) { ShowError(Loc.T("Install.BadFolder", ex.Message)); return; }

            if (!_runtimeOnly)
            {
                var problem = DspcInstaller.CheckFolder(dir);
                if (problem != null) { ShowError(problem); return; }
                var hasFiles = Directory.EnumerateFileSystemEntries(dir).Any();
                if (hasFiles && !DspcInfo.IsInstallFolder(dir) &&
                    MessageBox.Show(Loc.T("Install.NotEmpty"), Loc.T("App.Name"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            _busy = true;
            ChoicePanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            InstallButton.IsEnabled = false;
            var progress = new Progress<(int Percent, string Message)>(p =>
            {
                Progress.Value = p.Percent;
                StatusText.Text = p.Message;
            });
            try
            {
                await DspcInstaller.InstallRuntimeAsync(dir, progress, _cancel.Token);
                StatusText.Text = Loc.T("Install.Finishing");
                var exe = DspcInstaller.CopySelf(dir);
                if (!_runtimeOnly && ShortcutsBox.IsChecked == true)
                {
                    try { DspcInstaller.CreateShortcuts(exe); } catch { /* shortcuts are a convenience */ }
                }
                var data = DspcInfo.LoadUserData();
                data.InstallDir = dir;
                DspcInfo.SaveUserData(data);
                Progress.Value = 100;
                StatusText.Text = Loc.T("Install.Done");
                InstalledDirectory = dir;
                _busy = false;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                _busy = false;
                ProgressPanel.Visibility = Visibility.Collapsed;
                ChoicePanel.Visibility = _runtimeOnly ? Visibility.Collapsed : Visibility.Visible;
                InstallButton.IsEnabled = true;
                ShowError(ex is OperationCanceledException ? Loc.T("Common.Cancel") : Loc.T("Install.Failed", ex.Message));
            }
        }

        private void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
