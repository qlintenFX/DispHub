// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;

namespace DispHub;

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
