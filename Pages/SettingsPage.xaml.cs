using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DisplayHub.Constants;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

public partial class SettingsPage : Page, INavigationAware
{
    private bool _isLoaded;

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
        TrayLeftClickComboBox.SelectedIndex = MainWindow.SettingsManager.TrayLeftClickBehavior;

        if (MainWindow.SettingsManager.CloseToTray)
            CloseToTrayRadio.IsChecked = true;
        else
            CloseAppRadio.IsChecked = true;

        UpdateMasterToggleLabel();
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

    private void TrayLeftClick_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.TrayLeftClickBehavior = TrayLeftClickComboBox.SelectedIndex;
    }

    private void MasterToggleKeybind_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        MainWindow.SettingsManager.MasterToggleKey = (uint)dialog.VirtualKeyCode;
        MainWindow.SettingsManager.MasterToggleMod = dialog.Modifiers;
        UpdateMasterToggleLabel();

        if (Application.Current.MainWindow is MainWindow mw)
            mw.UpdateMasterToggleHotkey();
    }

    private void UpdateMasterToggleLabel()
    {
        var key = MainWindow.SettingsManager.MasterToggleKey;
        var mod = MainWindow.SettingsManager.MasterToggleMod;
        MasterToggleKeybind.Content = key == 0 ? "None" : FormatKeybind(key, mod);
    }

    private static string FormatKeybind(uint vk, uint mod)
    {
        if (vk == 0) return "None";
        var parts = new List<string>();
        if ((mod & AppConstants.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mod & AppConstants.MOD_ALT) != 0) parts.Add("Alt");
        if ((mod & AppConstants.MOD_SHIFT) != 0) parts.Add("Shift");
        Key key = KeyInterop.KeyFromVirtualKey((int)vk);
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }
}
