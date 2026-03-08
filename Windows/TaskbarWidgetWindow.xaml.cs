using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using DisplayHub.Helpers;
using DisplayHub.Services.Logging;

namespace DisplayHub.Windows;

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
        Dispatcher.Invoke(() =>
        {
            bool blur = MainWindow.SettingsManager.TaskbarWidgetBackgroundBlur;
            WidgetControl.UpdateState(profileName, isActive, dcMode, blur);
        });
    }

    public void SetWidgetVisible(bool visible)
    {
        Dispatcher.Invoke(() =>
        {
            WidgetControl.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (visible)
                CalculateAndSetPosition();
        });
    }

    public void ApplySettings()
    {
        Dispatcher.Invoke(() =>
        {
            _trayHandle = IntPtr.Zero; // Force refresh
            CalculateAndSetPosition();
        });
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

            int style = NativeMethods.GetWindowLong(_handle, NativeMethods.GWL_STYLE);
            style = (int)(((uint)style & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD);
            NativeMethods.SetWindowLong(_handle, NativeMethods.GWL_STYLE, style);
            NativeMethods.SetParent(_handle, _taskbarHandle);
            _isDockedInTaskbar = true;

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

            // Re-parent if needed (explorer restart)
            if (NativeMethods.GetParent(_handle) != _taskbarHandle)
                NativeMethods.SetParent(_handle, _taskbarHandle);

            var containerPos = new NativeMethods.POINT
            {
                X = taskbarRect.Left, Y = taskbarRect.Top
            };
            NativeMethods.ScreenToClient(_taskbarHandle, ref containerPos);

            NativeMethods.SetWindowPos(_handle, IntPtr.Zero,
                containerPos.X, containerPos.Y,
                taskbarWidth, taskbarHeight,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_ASYNCWINDOWPOS | NativeMethods.SWP_SHOWWINDOW);

            int widgetPhysicalW = (int)(130 * dpiScale);
            int widgetPhysicalH = (int)(28 * dpiScale);

            int widgetTop = (taskbarHeight - widgetPhysicalH) / 2 - 1;

            int widgetLeft = CalculateHorizontalPosition(
                taskbarRect, taskbarWidth, widgetPhysicalW, dpiScale);

            Dispatcher.Invoke(() =>
            {
                Canvas.SetLeft(WidgetControl, widgetLeft / dpiScale);
                Canvas.SetTop(WidgetControl, widgetTop / dpiScale);
                WidgetControl.Width = widgetPhysicalW / dpiScale;
                WidgetControl.Height = widgetPhysicalH / dpiScale;
            });

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

    private int CalculateHorizontalPosition(
        NativeMethods.RECT taskbarRect, int taskbarWidth, int widgetWidth, double dpiScale)
    {
        int position = MainWindow.SettingsManager.TaskbarWidgetPosition;
        bool autoPadding = MainWindow.SettingsManager.TaskbarWidgetAutoPadding;
        int manualPadding = (int)(MainWindow.SettingsManager.TaskbarWidgetManualPadding * dpiScale);

        switch (position)
        {
            case 0: // Left
            {
                // Always detect system button area as minimum safe offset
                int baseOffset = 60;
                IntPtr sysButtonArea = NativeMethods.FindWindowEx(
                    _taskbarHandle, IntPtr.Zero, "Windows.UI.Composition.DesktopWindowContentBridge", null);
                if (sysButtonArea != IntPtr.Zero)
                {
                    NativeMethods.GetWindowRect(sysButtonArea, out var btnRect);
                    int btnRight = btnRect.Right - taskbarRect.Left;
                    if (btnRight > baseOffset)
                        baseOffset = btnRight + 4;
                }
                return baseOffset + manualPadding;
            }

            case 1: // Center
                return (taskbarWidth - widgetWidth) / 2 + manualPadding;

            case 2: // Right (to the left of system tray)
            {
                try
                {
                    if (_trayHandle == IntPtr.Zero)
                        _trayHandle = NativeMethods.FindWindowEx(
                            _taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);

                    if (_trayHandle != IntPtr.Zero)
                    {
                        NativeMethods.GetWindowRect(_trayHandle, out var trayRect);
                        int autoPos = trayRect.Left - taskbarRect.Left - widgetWidth - 1;
                        return autoPadding
                            ? autoPos + manualPadding
                            : taskbarWidth - widgetWidth - 20 + manualPadding;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to get tray position", ex);
                }
                return taskbarWidth - widgetWidth - 20 + manualPadding;
            }

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
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
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
        if (!MainWindow.SettingsManager.TaskbarWidgetClickable) return;
        if (Application.Current.MainWindow is MainWindow mw)
            mw.OpenSettingsWindow();
    }

    public void RefreshPosition()
    {
        _trayHandle = IntPtr.Zero; // Force tray handle refresh
        CalculateAndSetPosition();
    }
}
