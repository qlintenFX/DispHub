using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DisplayHub.Constants;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace DisplayHub.Pages;

public partial class HomePage : Page, INavigationAware
{
    public HomePage()
    {
        InitializeComponent();
        VersionTextBlock.Text = $"v{AppConstants.Version}";
    }

    public Task OnNavigatedToAsync()
    {
        UpdateStatus();
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void UpdateStatus()
    {
        var sm = MainWindow.SettingsManager;
        int profileCount = MainWindow.ProfileManager.Profiles.Count;

        // Feature cards
        ProfileStatusText.Text = $"{profileCount} profile{(profileCount != 1 ? "s" : "")} configured";
        DcStatusText.Text = MainWindow.DynamicControls.IsEnabled ? "Enabled" : "Disabled";
        WidgetStatusText.Text = sm.TaskbarWidgetEnabled ? "Enabled" : "Disabled";
        FlyoutStatusText.Text = sm.FlyoutEnabled ? "Enabled" : "Disabled";
    }

    private void NavigateToPage(Type pageType)
    {
        var window = Window.GetWindow(this) as SettingsWindow;
        var nav = window?.FindName("RootNavigation") as NavigationView;
        nav?.Navigate(pageType);
    }

    private void Profiles_Click(object sender, RoutedEventArgs e) => NavigateToPage(typeof(ProfilesPage));
    private void DynamicControls_Click(object sender, RoutedEventArgs e) => NavigateToPage(typeof(DynamicControlsPage));
    private void TaskbarWidget_Click(object sender, RoutedEventArgs e) => NavigateToPage(typeof(TaskbarWidgetPage));
    private void Flyout_Click(object sender, RoutedEventArgs e) => NavigateToPage(typeof(FlyoutPage));
    private void Settings_Click(object sender, RoutedEventArgs e) => NavigateToPage(typeof(SettingsPage));
    private void About_Click(object sender, RoutedEventArgs e) => NavigateToPage(typeof(AboutPage));

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/qlintenFX/DisplayHub") { UseShellExecute = true });
    }

    private void ReportBug_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/qlintenFX/DisplayHub/issues/new") { UseShellExecute = true });
    }
}
