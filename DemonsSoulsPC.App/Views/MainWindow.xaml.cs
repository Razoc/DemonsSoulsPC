using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DemonsSoulsPC.App.ViewModels;

namespace DemonsSoulsPC.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                Title = $"Demon’s Souls PC v{version.Major}.{version.Minor}.{version.Build}";
                VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }

            // 0.2.3: full screen from the start (DSPC_LAUNCHER_WINDOWED=1: in a window), on the main monitor or the one
            // DSPC_LAUNCHER_SCREEN names (its number in Windows' list, from 1). F11, Alt+Enter or the controller's
            // View / Create button switch between full screen and a window.
            // The place is set before the window shows (a full-screen window fills the monitor it is on), full screen once
            // it is there.
            PlaceOnChosenScreen();
            // The player's choice is kept (launcherWindowed in dspc-settings.json); full screen by default.
            Loaded += (_, __) =>
            {
                bool windowed = Environment.GetEnvironmentVariable("DSPC_LAUNCHER_WINDOWED") == "1"
                                || Vm?.Settings?.LauncherWindowed == true;
                SetFullScreen(!windowed);
            };
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.F11 || e.SystemKey == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    ToggleFullScreen();
                    e.Handled = true;
                }
            };
            PadHintsText.Text = Services.Loc.T("UI.PadHints");
            Services.GamepadNavigator.PadModeChanged += on => Dispatcher.BeginInvoke(new Action(() =>
                PadHints.Visibility = on ? Visibility.Visible : Visibility.Collapsed));
        }

        public bool IsFullScreen { get; private set; }

        // The player's switch (top-right button, F11, Alt+Enter, View / Create): also remembered for the next start.
        public void ToggleFullScreen()
        {
            SetFullScreen(!IsFullScreen);
            var vm = Vm;
            if (vm?.Settings != null && vm.Settings.LauncherWindowed == IsFullScreen)
            {
                vm.Settings.LauncherWindowed = !IsFullScreen;
                vm.OnPropertyChanged(nameof(MainWindowViewModel.Settings)); // saves dspc-settings.json
            }
        }

        void ScreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullScreen();

        public void SetFullScreen(bool on)
        {
            IsFullScreen = on;
            // Top-right button: "leave full screen" in full screen, "full screen" in a window.
            ScreenButtonIcon.Text = on ? "" : "";
            ScreenButton.ToolTip = Services.Loc.T(on ? "UI.LeaveFullScreen" : "UI.FullScreen");
            // Sharp when enlarged: text laid out on whole pixels ("Display") and rounded sizes are right at the page's own
            // size only; scaled up they come out as blurred, blocky pixels. Full screen draws the text as shapes
            // ("Ideal"), without rounding, and enlarges the pictures smoothly.
            TextOptions.SetTextFormattingMode(this, on ? TextFormattingMode.Ideal : TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, on ? TextRenderingMode.Grayscale : TextRenderingMode.Auto);
            UseLayoutRounding = !on;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
            if (on)
            {
                SizeToContent = SizeToContent.Manual;
                Scaler.Stretch = Stretch.Uniform;
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanMinimize;
                Scaler.Stretch = Stretch.None;
                SizeToContent = SizeToContent.WidthAndHeight;
                // Back in the middle of the screen it is on.
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
                    var dpi = VisualTreeHelper.GetDpi(this);
                    Left = screen.Left / dpi.DpiScaleX + (screen.Width / dpi.DpiScaleX - ActualWidth) / 2;
                    Top = screen.Top / dpi.DpiScaleY + (screen.Height / dpi.DpiScaleY - ActualHeight) / 2;
                }));
            }
        }

        void PlaceOnChosenScreen()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable("DSPC_LAUNCHER_SCREEN"), out int n)) return;
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (n < 1 || n > screens.Length) return;
            var area = screens[n - 1].WorkingArea;
            double scale;
            using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96.0;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = area.Left / scale + 40;
            Top = area.Top / scale + 40;
        }

        MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

        // Quitter (button, or Y / Triangle): closes the launcher as its window's close button does.
        void QuitButton_Click(object sender, RoutedEventArgs e) => Quit();
        public void Quit() => Close();

        // Start / Options: Play, as the button.
        public void Play()
        {
            var command = Vm?.LaunchGameCommand;
            if (command != null && command.CanExecute(null)) command.Execute(null);
        }

        // LB / RB: the page before or after, in the side bar's order.
        public void NextPage(int step)
        {
            var vm = Vm;
            if (vm == null) return;
            var pages = (LauncherPage[])Enum.GetValues(typeof(LauncherPage));
            int i = Array.IndexOf(pages, vm.CurrentPage);
            vm.CurrentPage = pages[(i + step + pages.Length) % pages.Length];
        }

        // The page's first setting (else the Play button).
        public void FocusFirstSetting()
        {
            if (FirstFocusable(Page) is UIElement first) first.Focus();
            else PlayButton.Focus();
        }

        // B / Circle: back to the side bar, on the page shown.
        public void FocusPageList()
        {
            foreach (var child in Rail.Children)
                if (child is RadioButton r && r.IsChecked == true) { r.Focus(); return; }
        }

        static UIElement? FirstFocusable(DependencyObject root)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Control c && c.Focusable && c.IsEnabled && c.IsVisible && c.IsTabStop) return c;
                if (FirstFocusable(child) is UIElement found) return found;
            }
            return null;
        }
    }

    // Converters
    public class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b && parameter != null)
                return Enum.Parse(typeof(LauncherPage), parameter.ToString()!);
            return Binding.DoNothing;
        }
    }

    public class BoolToProgressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is bool b && b) ? 1 : 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PageVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LauncherPage page && parameter is string targetPage)
            {
                return page.ToString() == targetPage ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Template Selector for page content
    public class PageTemplateSelector : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LauncherPage page)
            {
                return page switch
                {
                    LauncherPage.Home => new HomeView(),
                    LauncherPage.GraphicsSettings => new GraphicsSettingsView(),
                    LauncherPage.ControllerSettings => new ControllerSettingsView(),
                    LauncherPage.AdvancedSettings => new AdvancedSettingsView(),
                    LauncherPage.Installation => new InstallationView(),
                    _ => new HomeView()
                };
            }
            return new HomeView();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}