// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DispHub.Platform.Shell;

/// <summary>A program with a window on screen.</summary>
public sealed record RunningProgram(string Path, string Name, string WindowTitle);

/// <summary>Names, icons and the list of running programs, for picking an app to make a rule for.</summary>
public static class ProgramInfo
{
    private static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "ApplicationFrameHost.exe", "TextInputHost.exe", "SystemSettings.exe", "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe", "SearchHost.exe", "LockApp.exe",
    };

    /// <summary>The program's own name ("Google Chrome"), falling back to its file name.</summary>
    public static string DisplayName(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                string? name = !string.IsNullOrWhiteSpace(info.FileDescription) ? info.FileDescription : info.ProductName;
                if (!string.IsNullOrWhiteSpace(name))
                    return name.Trim();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>The program's icon at <paramref name="size"/> pixels, or 0. The caller destroys it with DestroyIcon.</summary>
    public static nint ExtractIcon(string path, int size) => ExtractIcon(path, 0, size);

    /// <summary>Windows' own icon for a program, as Explorer shows for one without an icon of its own.</summary>
    public static unsafe nint GenericIcon(int size)
    {
        var info = new SHSTOCKICONINFO { cbSize = (uint)sizeof(SHSTOCKICONINFO) };
        if (PInvoke.SHGetStockIconInfo(SHSTOCKICONID.SIID_APPLICATION, SHGSI_FLAGS.SHGSI_ICONLOCATION, ref info).Failed)
            return 0;
        return ExtractIcon(info.szPath.ToString(), info.iIcon, size);
    }

    /// <summary>
    /// Where a program is from its file name alone: a running copy, where it registered for the Run box (App Paths),
    /// or the icon its installer registered for Installed apps. Null when none of them knows it.
    /// </summary>
    public static string? Find(string executable)
    {
        string name = System.IO.Path.GetFileName(executable.Trim());
        if (name.Length == 0)
            return null;

        Process[] running = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(name));
        try
        {
            foreach (Process process in running)
            {
                if (ForegroundWatcher.GetProcessPath((uint)process.Id) is { } path && IsNamed(path, name))
                    return path;
            }
        }
        finally
        {
            foreach (Process process in running)
                process.Dispose();
        }

        foreach (RegistryKey root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using RegistryKey? key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{name}");
            if (Existing(key?.GetValue(null) as string) is { } path)
                return path;
        }
        return Installed.Value.GetValueOrDefault(name);
    }

    /// <summary>Installed programs by file name, from the DisplayIcon of each entry in Installed apps.</summary>
    private static readonly Lazy<Dictionary<string, string>> Installed = new(() =>
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        (RegistryHive Hive, RegistryView View)[] places =
            [(RegistryHive.CurrentUser, RegistryView.Default), (RegistryHive.LocalMachine, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry32)];
        foreach ((RegistryHive hive, RegistryView view) in places)
        {
            using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? apps = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (apps is null)
                continue;
            foreach (string app in apps.GetSubKeyNames())
            {
                using RegistryKey? entry = apps.OpenSubKey(app);
                // C:\Program Files\VideoLAN\VLC\vlc.exe, sometimes with ",0" after it
                string? icon = (entry?.GetValue("DisplayIcon") as string)?.Split(',')[0];
                if (Existing(icon) is { } path && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    found.TryAdd(System.IO.Path.GetFileName(path), path);
            }
        }
        return found;
    });

    private static bool IsNamed(string path, string name) =>
        string.Equals(System.IO.Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase);

    private static string? Existing(string? path)
    {
        string? trimmed = path?.Trim().Trim('"');
        return !string.IsNullOrEmpty(trimmed) && System.IO.Path.IsPathRooted(trimmed) && File.Exists(trimmed) ? trimmed : null;
    }

    private static unsafe nint ExtractIcon(string path, int index, int size)
    {
        HICON large = default;
        fixed (char* file = path)
        {
            if (PInvoke.SHDefExtractIcon(file, index, 0, &large, null, (uint)size).Failed)
                return 0;
        }
        return large;
    }

    public static void DestroyIcon(nint icon)
    {
        if (icon != 0)
            PInvoke.DestroyIcon((HICON)icon);
    }

    /// <summary>Programs with a visible, regular window, one entry per program, DispHub excluded.</summary>
    public static unsafe IReadOnlyList<RunningProgram> WithWindows()
    {
        var found = new List<RunningProgram>();
        GCHandle handle = GCHandle.Alloc(found);
        try
        {
            PInvoke.EnumWindows(&OnWindow, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return [.. found
            .Where(p => !Hidden.Contains(System.IO.Path.GetFileName(p.Path)) && !string.Equals(p.Path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            .GroupBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL OnWindow(HWND window, LPARAM data)
    {
        if (!PInvoke.IsWindowVisible(window) || !PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_OWNER).IsNull)
            return true;
        int style = PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if ((style & (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) != 0)
            return true;
        int cloaked = 0;
        if (PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded && cloaked != 0)
            return true;
        int length = PInvoke.GetWindowTextLength(window);
        if (length == 0)
            return true;

        Span<char> title = length < 512 ? stackalloc char[length + 1] : new char[length + 1];
        fixed (char* chars = title)
            length = PInvoke.GetWindowText(window, chars, title.Length);
        _ = PInvoke.GetWindowThreadProcessId(window, out uint processId);
        if (ForegroundWatcher.GetProcessPath(processId) is { } path)
        {
            var list = (List<RunningProgram>)GCHandle.FromIntPtr(data).Target!;
            list.Add(new RunningProgram(path, DisplayName(path), title[..length].ToString()));
        }
        return true;
    }
}
