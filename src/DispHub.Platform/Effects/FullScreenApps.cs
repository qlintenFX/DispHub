// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;

namespace DispHub.Platform.Effects;

/// <summary>Finds full-screen apps and games, so overlays can step aside where they could add latency.</summary>
public static class FullScreenApps
{
    /// <summary>True while a full-screen Direct3D game or a presentation is running.</summary>
    public static bool IsGameOrPresentationRunning() =>
        PInvoke.SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE state).Succeeded &&
        state is QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN or QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE;

    /// <summary>The bounds of the foreground window if it covers a whole screen (and isn't the desktop), in pixels.</summary>
    public static Rectangle? ForegroundFullScreenBounds(IEnumerable<Rectangle> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        HWND window = PInvoke.GetForegroundWindow();
        if (window.IsNull || IsDesktop(window) || !PInvoke.GetWindowRect(window, out RECT rect))
            return null;
        var bounds = Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
        return monitors.FirstOrDefault(m => bounds.Contains(m)) is { IsEmpty: false } covered ? covered : null;
    }

    private static bool IsDesktop(HWND window)
    {
        Span<char> name = stackalloc char[32];
        int length = PInvoke.GetClassName(window, name);
        return name[..Math.Max(length, 0)] is "Progman" or "WorkerW" or "Shell_TrayWnd";
    }
}
