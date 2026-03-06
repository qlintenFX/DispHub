using DisplayHub.Services.Logging;

namespace DisplayHub;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        Logger.Initialize("displayhub.log");
        Logger.Log("DisplayHub starting...");
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        Logger.Log("DisplayHub closed normally");
        base.OnExit(e);
    }
}
