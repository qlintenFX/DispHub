using DisplayHub.Services.Logging;
using Wpf.Ui.Appearance;

namespace DisplayHub;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        Logger.Initialize("displayhub.log");

        // Follow the system light/dark theme instead of hardcoding Dark
        ApplicationThemeManager.ApplySystemTheme();

        Logger.Log("DisplayHub starting...");
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        Logger.Log("DisplayHub closed normally");
        base.OnExit(e);
    }
}
