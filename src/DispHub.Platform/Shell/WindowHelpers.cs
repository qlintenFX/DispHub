using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DispHub.Platform.Shell;

/// <summary>Small window tricks WPF doesn't expose.</summary>
public static class WindowHelpers
{
    /// <summary>Makes a window that never takes focus, stays out of Alt+Tab and lets clicks pass through.</summary>
    public static void MakeOverlay(nint handle)
    {
        var hwnd = (HWND)handle;
        int style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        style |= (int)(WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOPMOST);
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style);
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

    /// <summary>Lets Windows page out memory this process isn't using right now.</summary>
    public static void TrimWorkingSet() => PInvoke.EmptyWorkingSet(PInvoke.GetCurrentProcess());
}
