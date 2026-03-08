using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using DisplayHub.Helpers;
using DisplayHub.Services.Logging;

namespace DisplayHub.Windows;

/// <summary>
/// Taskbar-docked widget window. Follows FluentFlyout's SetParent pattern:
/// the Window covers the full taskbar area, the Control is positioned within
/// the Canvas, and SetWindowRgn limits the click region to the control.
/// </summary>
public partial class TaskbarWidgetWindow : Window
{
    private IntPtr _handle;
    private IntPtr _taskbarHandle;
    private IntPtr _trayHandle;
    private DispatcherTimer? _positionTimer;
    private bool _isDockedInTaskbar;
    private bool _positionUpdateInProgress;

    public TaskbarWidgetWindow()
    {
        InitializeComponent();
        WidgetControl.WidgetClicked += OnWidgetClicked;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromDependencyObject(this);
        source.AddHook(WndProc);
    }

    /// <summary>Block messages that cause taskbar freezes (FluentFlyout pattern).</summary>
    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case 0x003D: // WM_GETOBJECT
            case 0x0018: // WM_SHOWWINDOW
            case 0x0046: // WM_WINDOWPOSCHANGING
            case 0x0083: // WM_NCCALCSIZE
            case 0x0281: // WM_IME_SETCONTEXT
            case 0x0282: // WM_IME_NOTIFY
                handled = true;
                return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        SetupWindow();
    }

    public void UpdateDisplay(string profileName, bool isActive, bool dcMode)
    {
        Dispatcher.Invoke(() => WidgetControl.UpdateState(profileName, isActive, dcMode));
    }

    private void SetupWindow()
    {
        try
        {
            _taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (_taskbarHandle == IntPtr.Zero)
            {
                Logger.Log("Taskbar not found — floating widget");
                PositionFloating();
                Visibility = Visibility.Visible;
                return;
            }

            // Make this a child of the taskbar (FluentFlyout pattern)
            int style = NativeMethods.GetWindowLong(_handle, NativeMethods.GWL_STYLE);
            style = (int)(((uint)style & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD);
            NativeMethods.SetWindowLong(_handle, NativeMethods.GWL_STYLE, style);

            NativeMethods.SetParent(_handle, _taskbarHandle);
            _isDockedInTaskbar = true;

            // Find the system tray notification area handle
            _trayHandle = NativeMethods.FindWindowEx(_taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);

            CalculateAndSetPosition();
            StartPositionTimer();
            Logger.Log("Widget docked into taskbar");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to dock widget", ex);
            PositionFloating();
            Visibility = Visibility.Visible;
        }
    }

    private void CalculateAndSetPosition()
    {
        if (!_isDockedInTaskbar || _taskbarHandle == IntPtr.Zero) return;
        if (_positionUpdateInProgress) return;
        _positionUpdateInProgress = true;

        try
        {
            double dpiScale = NativeMethods.GetDpiForWindow(_taskbarHandle) / 96.0;
            if (dpiScale <= 0) dpiScale = 1.0;

            NativeMethods.GetWindowRect(_taskbarHandle, out var taskbarRect);
            int taskbarWidth = taskbarRect.Width;
            int taskbarHeight = taskbarRect.Height;

            // Re-parent if needed (e.g. after explorer restart)
            if (NativeMethods.GetParent(_handle) != _taskbarHandle)
                NativeMethods.SetParent(_handle, _taskbarHandle);

            // Convert screen coords to client coords for SetWindowPos
            var containerPos = new NativeMethods.POINT
            {
                X = taskbarRect.Left, Y = taskbarRect.Top
            };
            NativeMethods.ScreenToClient(_taskbarHandle, ref containerPos);

            // Set the window to cover the full taskbar area
            NativeMethods.SetWindowPos(_handle, IntPtr.Zero,
                containerPos.X, containerPos.Y,
                taskbarWidth, taskbarHeight,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_ASYNCWINDOWPOS | NativeMethods.SWP_SHOWWINDOW);

            // Widget dimensions
            int widgetPhysicalW = (int)(130 * dpiScale);
            int widgetPhysicalH = (int)(28 * dpiScale);
            int manualPadding = (int)(MainWindow.SettingsManager.TaskbarWidgetPadding * dpiScale);

            // Calculate vertical position (centered, -1 like FluentFlyout)
            int widgetTop = (taskbarHeight - widgetPhysicalH) / 2 - 1;

            // Calculate horizontal position based on alignment
            int widgetLeft = CalculateHorizontalPosition(
                taskbarRect, taskbarWidth, widgetPhysicalW, manualPadding, dpiScale);

            // Position the control within the Canvas
            Dispatcher.Invoke(() =>
            {
                Canvas.SetLeft(WidgetControl, widgetLeft / dpiScale);
                Canvas.SetTop(WidgetControl, widgetTop / dpiScale);
                WidgetControl.Width = widgetPhysicalW / dpiScale;
                WidgetControl.Height = widgetPhysicalH / dpiScale;
            });

            // Set the click region so only the widget area is interactive
            IntPtr rgn = NativeMethods.CreateRectRgn(
                widgetLeft, widgetTop, widgetLeft + widgetPhysicalW, widgetTop + widgetPhysicalH);
            NativeMethods.SetWindowRgn(_handle, rgn, true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Widget position update failed", ex);
        }
        finally
        {
            _positionUpdateInProgress = false;
        }
    }

    /// <summary>
    /// Calculate horizontal position following FluentFlyout's logic:
    /// Left  = left edge + padding
    /// Center = center of taskbar
    /// Right = left of TrayNotifyWnd (system tray area), so it doesn't collide
    /// </summary>
    private int CalculateHorizontalPosition(
        NativeMethods.RECT taskbarRect, int taskbarWidth, int widgetWidth, int manualPadding, double dpiScale)
    {
        int position = MainWindow.SettingsManager.TaskbarWidgetPosition;

        switch (position)
        {
            case 0: // Left
                return 20 + manualPadding;

            case 1: // Center
                return (taskbarWidth - widgetWidth) / 2 + manualPadding;

            case 2: // Right — position next to system tray (FluentFlyout pattern)
                try
                {
                    if (_trayHandle == IntPtr.Zero)
                        _trayHandle = NativeMethods.FindWindowEx(_taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);

                    if (_trayHandle != IntPtr.Zero)
                    {
                        NativeMethods.GetWindowRect(_trayHandle, out var trayRect);
                        // Position just left of the tray area
                        return trayRect.Left - taskbarRect.Left - widgetWidth - 1 + manualPadding;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to get tray position", ex);
                }
                // Fallback: right edge with padding
                return taskbarWidth - widgetWidth - 20 + manualPadding;

            default:
                return 20;
        }
    }

    private void PositionFloating()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 10;
        Top = workArea.Bottom - Height - 10;
    }

    private void StartPositionTimer()
    {
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _positionTimer.Tick += (_, _) =>
        {
            Dispatcher.BeginInvoke(CalculateAndSetPosition, DispatcherPriority.Background);
        };
        _positionTimer.Start();
    }

    public void StopAndClose()
    {
        _positionTimer?.Stop();
        _positionTimer = null;
        if (_isDockedInTaskbar && _handle != IntPtr.Zero)
        {
            NativeMethods.SetParent(_handle, IntPtr.Zero);
            _isDockedInTaskbar = false;
        }
        Close();
    }

    private void OnWidgetClicked()
    {
        if (Application.Current.MainWindow is MainWindow mw)
            mw.OpenSettingsWindow();
    }

    public void RefreshPosition() => CalculateAndSetPosition();
}
