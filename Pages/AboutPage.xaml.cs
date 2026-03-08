using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;
using DisplayHub.Constants;

namespace DisplayHub.Pages;

public partial class AboutPage : Page, INavigationAware
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += (s, e) => VersionText.Text = $"Version {AppConstants.Version}";
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;
}
