// SPDX-License-Identifier: GPL-3.0-or-later
using DisplayHub.Pages;
using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace DisplayHub;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(MainWindow.SettingsManager.AppTheme);
        RootNavigation.Navigate(typeof(HomePage));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (MainWindow.SettingsManager.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        // Close-to-tray is off: fully exit the application
        MainWindow.DisplayManager?.ResetToDefault();
        MainWindow.HotkeyManager?.Dispose();
        Application.Current.Shutdown();
    }

    private void ResetDisplay_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.DisplayManager.ResetToDefault();
    }

    public static void ApplyTheme(int theme)
    {
        var appTheme = theme switch
        {
            1 => Wpf.Ui.Appearance.ApplicationTheme.Light,
            2 => Wpf.Ui.Appearance.ApplicationTheme.Dark,
            _ => Wpf.Ui.Appearance.ApplicationTheme.Unknown,
        };
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(appTheme);
    }
}
