using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

public partial class SettingsPage : Page, INavigationAware
{
    private bool _isLoaded = false;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        StartWithWindowsToggle.IsChecked = MainWindow.SettingsManager.StartWithWindows;
        MinimizeToTrayToggle.IsChecked = MainWindow.SettingsManager.MinimizeToTray;
        ResetOnExitToggle.IsChecked = MainWindow.SettingsManager.ResetOnExit;
        _isLoaded = true;
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.StartWithWindows = StartWithWindowsToggle.IsChecked == true;
    }

    private void MinimizeToTray_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.MinimizeToTray = MinimizeToTrayToggle.IsChecked == true;
    }

    private void ResetOnExit_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.ResetOnExit = ResetOnExitToggle.IsChecked == true;
    }

    private void ResetNow_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.DisplayManager.ResetToDefault();
    }
}
