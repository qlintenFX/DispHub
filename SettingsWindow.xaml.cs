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

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(MainWindow.SettingsManager.AppTheme);

        RootNavigation.IsPaneOpen = false;
        RootNavigation.Navigate(typeof(ProfilesPage));

        await Task.Delay(100);
        RootNavigation.IsPaneOpen = true;
        await Task.Delay(10);
        RootNavigation.IsPaneOpen = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (MainWindow.SettingsManager.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    private void ResetDisplay_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.DisplayManager.ResetToDefault();
    }

    public static void ApplyTheme(int theme)
    {
        switch (theme)
        {
            case 1:
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
                break;
            case 2:
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
                break;
            default:
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Unknown);
                break;
        }
    }
}
