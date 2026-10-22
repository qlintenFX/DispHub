// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using Windows.ApplicationModel.Activation;

namespace DispHub.Platform.Shell;

public static class LaunchInfo
{
    /// <summary>
    /// The command line plus, for the Store package, the link it was opened with (packaged apps get protocol
    /// activations through the activation API, not the command line).
    /// </summary>
    public static string[] GetArguments(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!PackageInfo.IsPackaged)
            return args;
        try
        {
            if (global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs() is ProtocolActivatedEventArgs protocol)
                return [.. args, protocol.Uri.OriginalString];
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
        }
        return args;
    }

    /// <summary>True when Windows started DispHub at sign-in: the packaged StartupTask or the --startup Run-key argument.</summary>
    public static bool IsStartupLaunch(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Contains(Autostart.StartupArgument, StringComparer.OrdinalIgnoreCase))
            return true;
        if (!PackageInfo.IsPackaged)
            return false;
        try
        {
            return global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind == ActivationKind.StartupTask;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }
}
