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
        ThemeComboBox.SelectedIndex = MainWindow.SettingsManager.AppTheme;

        if (MainWindow.SettingsManager.CloseToTray)
            CloseToTrayRadio.IsChecked = true;
        else
            CloseAppRadio.IsChecked = true;

        _isLoaded = true;
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        int theme = ThemeComboBox.SelectedIndex;
        MainWindow.SettingsManager.AppTheme = theme;
        SettingsWindow.ApplyTheme(theme);
    }

    private void CloseBehavior_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.CloseToTray = CloseToTrayRadio.IsChecked == true;
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.StartWithWindows = StartWithWindowsToggle.IsChecked == true;
    }
}
