// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using System.Runtime.InteropServices;
using DispHub.Core.Model;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DispHub.Platform.Shell;

/// <summary>Small window tricks WPF doesn't expose.</summary>
public static partial class WindowHelpers
{
    /// <summary>Makes a window that never takes focus, stays out of Alt+Tab and lets clicks pass through.</summary>
    public static void MakeOverlay(nint handle)
    {
        var hwnd = (HWND)handle;
        int style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        style |= (int)(WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOPMOST);
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style);
    }

    /// <summary>Keeps a window out of Alt+Tab and the taskbar.</summary>
    public static void MakeToolWindow(nint handle)
    {
        var hwnd = (HWND)handle;
        int style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style | (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
    }

    /// <summary>The work area (screen minus taskbar) of the monitor under the mouse, in physical pixels.</summary>
    public static Rectangle WorkAreaUnderCursor()
    {
        _ = PInvoke.GetCursorPos(out Point cursor);
        HMONITOR monitor = PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!PInvoke.GetMonitorInfo(monitor, ref info))
            return Rectangle.Empty;
        RECT work = info.rcWork;
        return Rectangle.FromLTRB(work.left, work.top, work.right, work.bottom);
    }

    /// <summary>The monitor under the mouse and its work area (without the taskbar), in physical pixels.</summary>
    public static (Rectangle Monitor, Rectangle Work) ScreenUnderCursor()
    {
        _ = PInvoke.GetCursorPos(out Point cursor);
        HMONITOR monitor = PInvoke.MonitorFromPoint(cursor, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!PInvoke.GetMonitorInfo(monitor, ref info))
            return (Rectangle.Empty, Rectangle.Empty);
        RECT m = info.rcMonitor, w = info.rcWork;
        return (Rectangle.FromLTRB(m.left, m.top, m.right, m.bottom), Rectangle.FromLTRB(w.left, w.top, w.right, w.bottom));
    }

    /// <summary>Moves and resizes a window in physical pixels and shows it on top, keeping its activation.</summary>
    public static void PlaceOnTop(nint handle, Rectangle bounds) =>
        PInvoke.SetWindowPos(
            (HWND)handle,
            HWND.HWND_TOPMOST,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

    /// <summary>Moves and resizes a window in physical pixels without activating it, and shows it.</summary>
    public static void ShowAt(nint handle, Rectangle bounds) =>
        PInvoke.SetWindowPos(
            (HWND)handle,
            HWND.HWND_TOPMOST,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

    public static void BringToFront(nint handle) => PInvoke.SetForegroundWindow((HWND)handle);

    /// <summary>
    /// Sets the material Windows 11 draws behind a window's see-through areas, and a dark or light frame to match.
    /// Solid turns the material off; the window then paints its own background.
    /// </summary>
    public static unsafe void SetMaterial(nint handle, WindowBackdrop backdrop, bool dark)
    {
        var hwnd = (HWND)handle;
        BOOL useDark = dark;
        _ = PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, &useDark, (uint)sizeof(BOOL));
        DWM_SYSTEMBACKDROP_TYPE type = backdrop switch
        {
            WindowBackdrop.Mica => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
            WindowBackdrop.MicaAlt => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TABBEDWINDOW,
            WindowBackdrop.Acrylic => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
            _ => DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE,
        };
        _ = PInvoke.DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, &type, (uint)sizeof(DWM_SYSTEMBACKDROP_TYPE));
    }

    /// <summary>
    /// Blurs what's behind a window and lays <paramref name="tint"/> over it, its alpha setting how solid it looks:
    /// the acrylic Windows' own flyouts use, with a strength of our choosing (the system materials have a fixed one).
    /// Null turns it off. Turn the system material off first (<see cref="SetMaterial"/> with Solid).
    /// </summary>
    public static unsafe void SetAcrylic(nint handle, Color? tint)
    {
        // DWM draws the blur under the window's own pixels, so the frame must not reach into the client area: a
        // frame extended for a system material (as WPF-UI does) covers the blur with black.
        var noFrame = default(global::Windows.Win32.UI.Controls.MARGINS);
        _ = PInvoke.DwmExtendFrameIntoClientArea((HWND)handle, in noFrame);
        var accent = new AccentPolicy
        {
            AccentState = tint is null ? AccentDisabled : AccentAcrylicBlurBehind,
            // ABGR.
            GradientColor = tint is { } c ? (uint)(c.A << 24 | c.B << 16 | c.G << 8 | c.R) : 0,
        };
        var data = new WindowCompositionAttributeData
        {
            Attribute = WcaAccentPolicy,
            Data = &accent,
            SizeOfData = sizeof(AccentPolicy),
        };
        _ = SetWindowCompositionAttribute(handle, &data);
    }

    /// <summary>The size in pixels Windows draws small icons at (notification area, title bars) on this system's DPI.</summary>
    public static int SmallIconSize() =>
        PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, PInvoke.GetDpiForSystem());

    /// <summary>How long since the last keyboard, mouse or touch input anywhere on this PC.</summary>
    public static TimeSpan IdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        return PInvoke.GetLastInputInfo(ref info)
            ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime))
            : TimeSpan.Zero;
    }

    /// <summary>Lets Windows page out memory this process isn't using right now.</summary>
    public static void TrimWorkingSet() => PInvoke.EmptyWorkingSet(PInvoke.GetCurrentProcess());

    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;
    private const int AccentAcrylicBlurBehind = 4;

    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    private unsafe struct WindowCompositionAttributeData
    {
        public int Attribute;
        public void* Data;
        public int SizeOfData;
    }

    // The one hand-written import: SetWindowCompositionAttribute isn't in the Win32 metadata, so CsWin32 can't make
    // it. Undocumented but unchanged since Windows 10; the shell and apps such as FluentFlyout use it for acrylic.
    [LibraryImport("user32.dll")]
    private static unsafe partial int SetWindowCompositionAttribute(nint hwnd, WindowCompositionAttributeData* data);
}
