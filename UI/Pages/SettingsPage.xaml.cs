using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DisplayHub.Services.Logging;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.UI.Pages;

public partial class SettingsPage : Page, INavigationAware
{
    private MainWindow MainWindow => (System.Windows.Application.Current.MainWindow as MainWindow)!;

    public SettingsPage()
    {
        InitializeComponent();
    }

    public Task OnNavigatedToAsync()
    {
        var mw = MainWindow;
        StartupToggle.IsChecked = mw.SettingsManager.GetStartWithWindows();
        MinimizeToTrayToggle.IsChecked = mw.SettingsManager.GetMinimizeToTray();
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void StartupToggle_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            MainWindow.SettingsManager.SetStartWithWindows(StartupToggle.IsChecked == true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update startup setting", ex);
            System.Windows.MessageBox.Show($"Failed to update startup setting: {ex.Message}",
                "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StartupToggle.IsChecked = MainWindow.SettingsManager.GetStartWithWindows();
        }
    }

    private void MinimizeToTrayToggle_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            MainWindow.SettingsManager.SetMinimizeToTray(MinimizeToTrayToggle.IsChecked == true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update minimize to tray setting", ex);
            System.Windows.MessageBox.Show($"Failed to update minimize to tray setting: {ex.Message}",
                "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            MinimizeToTrayToggle.IsChecked = MainWindow.SettingsManager.GetMinimizeToTray();
        }
    }
}
