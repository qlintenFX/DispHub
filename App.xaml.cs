using System.Windows;

namespace DisplayHub;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            Services.Logging.Logger.LogError("UnhandledException", (args.ExceptionObject as Exception)!);

        DispatcherUnhandledException += (s, args) =>
        {
            Services.Logging.Logger.LogError("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
