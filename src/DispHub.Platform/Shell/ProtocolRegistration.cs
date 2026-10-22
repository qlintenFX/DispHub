// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Win32;

namespace DispHub.Platform.Shell;

/// <summary>
/// Makes disphub:// links open DispHub. The Store package declares this in its manifest; unpackaged builds register
/// it for the current user only.
/// </summary>
public static class ProtocolRegistration
{
    public static void EnsureRegistered(string scheme)
    {
        if (PackageInfo.IsPackaged || Environment.ProcessPath is not { } exe)
            return;

        string command = $"\"{exe}\" \"%1\"";
        using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}");
        using RegistryKey commandKey = key.CreateSubKey(@"shell\open\command");
        // Never take over a handler that still works, such as the Store install.
        if (commandKey.GetValue(null) is string existing &&
            (string.Equals(existing, command, StringComparison.OrdinalIgnoreCase) || File.Exists(ExecutableOf(existing))))
        {
            return;
        }
        key.SetValue(null, "URL:DispHub preset");
        key.SetValue("URL Protocol", "");
        commandKey.SetValue(null, command);
    }

    private static string ExecutableOf(string command)
    {
        if (!command.StartsWith('"'))
            return command.Split(' ')[0];
        int end = command.IndexOf('"', 1);
        return end > 1 ? command[1..end] : command.Trim('"');
    }
}
