using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DemonsSoulsPC.App.Services;

namespace DemonsSoulsPC.App.Views
{
    // Shown by Play while the shaders were never precompiled for this engine and graphics driver
    // (GameSetup.PrecompileNeeded): the quick precompile (-Quick: what play meets) with a progress bar, then the game. "Play now" stops it (what is
    // done is kept, the next Play continues); closing the window stops it and starts nothing.
    public class ShaderPrepWindow : Window
    {
        public enum Outcome { Done, PlayNow, Closed }

        private readonly string _root;
        private readonly string _dump;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly ProgressBar _bar;
        private readonly TextBlock _status;
        private readonly TextBlock _remaining;
        private readonly Stopwatch _clock = new Stopwatch();
        private int _firstPercent = -1;
        private TimeSpan _firstAt;
        private Outcome _outcome = Outcome.Closed;
        private bool _finished;

        private ShaderPrepWindow(string root, string dump)
        {
            _root = root;
            _dump = dump;
            Title = Loc.T("Prep.Title");
            Width = 620;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.CanMinimize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (TryFindResource("MainWindowStyle") is Style windowStyle) Style = windowStyle;
            try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/dspc.ico")); } catch { }

            var panel = new StackPanel { Margin = new Thickness(32) };
            panel.Children.Add(Text(Loc.T("Prep.Heading"), "TitleTextStyle", new Thickness(0, 0, 0, 12)));
            panel.Children.Add(Text(Loc.T("Prep.Intro"), "BodyTextStyle", new Thickness(0, 0, 0, 20)));
            _bar = new ProgressBar { Height = 14, Minimum = 0, Maximum = 100, IsIndeterminate = true };
            if (TryFindResource("ModernProgressBarStyle") is Style barStyle) _bar.Style = barStyle;
            panel.Children.Add(_bar);
            _status = Text(Loc.T("Prep.Starting"), "BodyTextStyle", new Thickness(0, 12, 0, 0));
            panel.Children.Add(_status);
            _remaining = Text("", "MutedTextStyle", new Thickness(0, 4, 0, 0));
            panel.Children.Add(_remaining);
            panel.Children.Add(Text(Loc.T("Prep.PlayNowHint"), "MutedTextStyle", new Thickness(0, 20, 0, 10)));
            var playNow = new Button { Content = Loc.T("Prep.PlayNow"), HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 180, Padding = new Thickness(16, 8, 16, 8) };
            if (TryFindResource("SecondaryButtonStyle") is Style buttonStyle) playNow.Style = buttonStyle;
            playNow.Click += (s, e) => { _outcome = Outcome.PlayNow; _stop.Cancel(); };
            panel.Children.Add(playNow);
            Content = panel;

            Loaded += async (s, e) => await RunAsync();
            Closing += (s, e) => { if (!_finished) _stop.Cancel(); };
        }

        private TextBlock Text(string text, string style, Thickness margin)
        {
            var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = margin };
            if (TryFindResource(style) is Style found) block.Style = found;
            return block;
        }

        private async Task RunAsync()
        {
            _clock.Start();
            var progress = new Progress<(int Percent, string Message)>(Report);
            int code;
            try { code = await GameSetup.PrecompileAsync(_root, _dump, progress, _stop.Token, quick: true); }
            catch (Exception ex)
            {
                GameSetup.AppendLog(System.IO.Path.Combine(_root, "Logs", "precompile.log"), "launcher: " + ex + Environment.NewLine);
                code = -1;
            }
            _finished = true;
            if (!_stop.IsCancellationRequested)
            {
                // A failure does not keep anyone from playing: the game starts, and the next Play tries again.
                _outcome = Outcome.Done;
                if (code != 0)
                    MessageBox.Show(this, string.Format(Loc.T("Precompile failed (code {0}): see Logs\\precompile.log."), code) + "\n\n" + Loc.T("Prep.FailedPlay"),
                        "Demon’s Souls PC", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            Close();
        }

        private void Report((int Percent, string Message) p)
        {
            _status.Text = p.Message;
            if (p.Percent < 0) return;
            _bar.IsIndeterminate = false;
            _bar.Value = p.Percent;
            Title = $"{p.Percent} % · {Loc.T("Prep.Title")}";
            // The time left from the pace since the first progress line (the planning before it does not count).
            if (_firstPercent < 0) { _firstPercent = p.Percent; _firstAt = _clock.Elapsed; return; }
            var done = p.Percent - _firstPercent;
            if (done <= 0 || p.Percent >= 100) return;
            var minutes = (_clock.Elapsed - _firstAt).TotalMinutes / done * (100 - p.Percent);
            _remaining.Text = minutes < 1.5 ? Loc.T("Prep.LeftAlmost") : string.Format(Loc.T("Prep.LeftMinutes"), (int)Math.Ceiling(minutes));
        }

        // Before Play: prepares the shaders when needed. False: the window was closed (the game is not started).
        public static bool PrepareBeforePlay(string root, string? dump, Window? owner = null)
        {
            if (string.IsNullOrEmpty(dump) || !System.IO.File.Exists(System.IO.Path.Combine(dump, "eboot.bin"))) return true;
            if (!GameSetup.PrecompileNeeded(root)) return true;
            var window = new ShaderPrepWindow(root, dump!);
            if (owner != null && owner.IsVisible) window.Owner = owner;
            window.ShowDialog();
            return window._outcome != Outcome.Closed;
        }
    }
}
