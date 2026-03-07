using System.Windows.Controls;
using DisplayHub.Constants;

namespace DisplayHub.UI.Pages;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppConstants.Version}";
        CopyrightText.Text = $"© 2024–{DateTime.Now.Year} qlintenFX";
    }
}
