using System;
using System.Windows;
using System.Windows.Controls;
using DisplayHub.Services.Logging;

namespace DisplayHub.UI.Pages;

public partial class SettingsPage : Page
{
    private readonly MainWindow mainWindow;

    public SettingsPage(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow;
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        StartupToggle.IsChecked = mainWindow.SettingsManager.GetStartWithWindows();
        MinimizeToTrayToggle.IsChecked = mainWindow.SettingsManager.GetMinimizeToTray();
    }

    private void StartupToggle_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            mainWindow.SettingsManager.SetStartWithWindows(StartupToggle.IsChecked == true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update startup setting", ex);
            System.Windows.MessageBox.Show($"Failed to update startup setting: {ex.Message}", "Settings Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StartupToggle.IsChecked = mainWindow.SettingsManager.GetStartWithWindows();
        }
    }

    private void MinimizeToTrayToggle_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            mainWindow.SettingsManager.SetMinimizeToTray(MinimizeToTrayToggle.IsChecked == true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update minimize to tray setting", ex);
            System.Windows.MessageBox.Show($"Failed to update minimize to tray setting: {ex.Message}", "Settings Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            MinimizeToTrayToggle.IsChecked = mainWindow.SettingsManager.GetMinimizeToTray();
        }
    }
}
