// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using Microsoft.Win32;

namespace DispHub.Platform.Gamma;

/// <summary>
/// The GdiIcmGammaRange registry value. Windows only accepts gamma ramps close to the identity ramp unless it is 256,
/// which f.lux and similar tools set to allow warmer night colours. Setting it needs administrator rights.
/// </summary>
public static class GammaRange
{
    private const string KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM";
    private const string ValueName = "GdiIcmGammaRange";

    public static int? Read()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is int value ? value : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    public static bool IsExtended => Read() is >= 256;

    /// <summary>Runs reg.exe elevated (shows a UAC prompt) to set the value to 256. Returns false if the user declined.</summary>
    public static bool TryEnableExtended()
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "reg.exe"),
            Arguments = $"add \"HKLM\\{KeyPath}\" /v {ValueName} /t REG_DWORD /d 256 /f",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        };
        try
        {
            using var process = System.Diagnostics.Process.Start(start);
            if (process is null)
                return false;
            process.WaitForExit(TimeSpan.FromSeconds(30));
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
