using System;
using System.Windows;
using DisplayHub.Services.Logging;
using Wpf.Ui.Appearance;

namespace DisplayHub;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            Logger.LogError("Unhandled AppDomain exception", args.ExceptionObject as Exception);

        DispatcherUnhandledException += (sender, args) =>
            Logger.LogError("Unhandled Dispatcher exception", args.Exception);

        base.OnStartup(e);

        Logger.Initialize("displayhub.log");
        ApplicationThemeManager.ApplySystemTheme();
        Logger.Log("DisplayHub starting...");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Log("DisplayHub closed normally");
        base.OnExit(e);
    }
}
