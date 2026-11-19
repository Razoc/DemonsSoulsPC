using System;
using DemonsSoulsPC.App.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DemonsSoulsPC.App.ViewModels;

namespace DemonsSoulsPC.App.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            // Built before the controls, so every binding sees this page's view model at once.
            if (MainWindowViewModel.Current is MainWindowViewModel main)
            {
                DataContext = new HomeViewModel(main);
            }
            InitializeComponent();
        }
    }

    public class StatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                // Negative statuses first: in French "Non installé" also contains "installé".
                if (status == Loc.T("Not Installed") || status == Loc.T("Not installed") || status == Loc.T("Not Ready"))
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4444"));
                if (status == Loc.T("Installed and Ready") || status == Loc.T("Ready") || status == Loc.T("Installed"))
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4AA"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAA00"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}