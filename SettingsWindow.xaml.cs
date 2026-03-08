using DisplayHub.Pages;
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
        RootNavigation.IsPaneOpen = false;
        RootNavigation.Navigate(typeof(ProfilesPage));

        await Task.Delay(100);
        RootNavigation.IsPaneOpen = true;
        await Task.Delay(10);
        RootNavigation.IsPaneOpen = false;
    }
}
