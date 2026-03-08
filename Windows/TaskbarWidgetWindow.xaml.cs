using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DisplayHub.Helpers;
using DisplayHub.Services.Logging;

namespace DisplayHub.Windows;

public partial class TaskbarWidgetWindow : Window
{
    private IntPtr _handle;
    private IntPtr _taskbarHandle;
    private DispatcherTimer? _positionTimer;
    private bool _isDockedInTaskbar;

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

    /// <summary>
    /// Block messages that cause taskbar freezes (FluentFlyout pattern).
    /// </summary>
    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case 0x003D: // WM_GETOBJECT - UI Automation queries
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
                Logger.Log("Taskbar not found — floating widget mode");
                PositionFloating();
                Visibility = Visibility.Visible;
                return;
            }

            // Change from popup to child window style
            int style = NativeMethods.GetWindowLong(_handle, NativeMethods.GWL_STYLE);
            style = (int)(((uint)style & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD);
            NativeMethods.SetWindowLong(_handle, NativeMethods.GWL_STYLE, style);

            // Parent into taskbar
            NativeMethods.SetParent(_handle, _taskbarHandle);
            _isDockedInTaskbar = true;

            CalculateAndSetPosition();
            StartPositionTimer();

            Logger.Log("Widget docked into taskbar");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to dock widget into taskbar", ex);
            PositionFloating();
            Visibility = Visibility.Visible;
        }
    }

    private void CalculateAndSetPosition()
    {
        if (!_isDockedInTaskbar || _taskbarHandle == IntPtr.Zero) return;

        try
        {
            // Get DPI scaling
            uint dpi = NativeMethods.GetDpiForWindow(_taskbarHandle);
            double dpiScale = dpi > 0 ? dpi / 96.0 : 1.0;

            // Get taskbar dimensions
            NativeMethods.GetWindowRect(_taskbarHandle, out var taskbarRect);
            int taskbarWidth = taskbarRect.Width;
            int taskbarHeight = taskbarRect.Height;

            // Convert taskbar screen coords to client coords
            var containerPos = new NativeMethods.POINT
            {
                X = taskbarRect.Left,
                Y = taskbarRect.Top
            };
            NativeMethods.ScreenToClient(_taskbarHandle, ref containerPos);

            // First, set the window to cover the full taskbar area
            NativeMethods.SetWindowPos(_handle, IntPtr.Zero,
                containerPos.X, containerPos.Y,
                taskbarWidth, taskbarHeight,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_ASYNCWINDOWPOS | NativeMethods.SWP_SHOWWINDOW);

            // Calculate widget position within the taskbar
            int widgetWidth = (int)(140 * dpiScale);
            int widgetHeight = (int)(32 * dpiScale);
            int padding = (int)(MainWindow.SettingsManager.TaskbarWidgetPadding * dpiScale);

            int widgetX = MainWindow.SettingsManager.TaskbarWidgetPosition switch
            {
                0 => padding,                                          // Left
                2 => taskbarWidth - widgetWidth - padding,             // Right
                _ => (taskbarWidth - widgetWidth) / 2,                 // Center
            };
            int widgetY = (taskbarHeight - widgetHeight) / 2;

            // Position the control within the Canvas
            Dispatcher.Invoke(() =>
            {
                System.Windows.Controls.Canvas.SetLeft(WidgetControl, widgetX / dpiScale);
                System.Windows.Controls.Canvas.SetTop(WidgetControl, widgetY / dpiScale);
                WidgetControl.Width = widgetWidth / dpiScale;
                WidgetControl.Height = widgetHeight / dpiScale;
            });

            // Set the click region so only the widget area is interactive
            IntPtr rgn = NativeMethods.CreateRectRgn(widgetX, widgetY,
                widgetX + widgetWidth, widgetY + widgetHeight);
            NativeMethods.SetWindowRgn(_handle, rgn, true);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update widget position", ex);
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
        _positionTimer.Tick += (_, _) => CalculateAndSetPosition();
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
