using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DisplayHub.Constants;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

public partial class AboutPage : Page, INavigationAware
{
    public AboutPage()
    {
        InitializeComponent();
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && !string.IsNullOrEmpty(url))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
