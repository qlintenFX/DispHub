using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DisplayHub.Controls;
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
        Loaded += TaskbarWidgetWindow_Loaded;
    }

    private void TaskbarWidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        DockIntoTaskbar();
    }

    public void UpdateDisplay(string profileName, bool isActive)
    {
        Dispatcher.Invoke(() => WidgetControl.UpdateProfile(profileName, isActive));
    }

    private void DockIntoTaskbar()
    {
        try
        {
            _taskbarHandle = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (_taskbarHandle == IntPtr.Zero)
            {
                Logger.Log("Taskbar not found, floating widget mode");
                PositionFloating();
                return;
            }

            // Change window style from popup to child
            int style = NativeMethods.GetWindowLong(_handle, NativeMethods.GWL_STYLE);
            style = (int)((uint)style & ~NativeMethods.WS_POPUP | NativeMethods.WS_CHILD);
            NativeMethods.SetWindowLong(_handle, NativeMethods.GWL_STYLE, style);

            // Parent into taskbar
            NativeMethods.SetParent(_handle, _taskbarHandle);
            _isDockedInTaskbar = true;

            UpdatePosition();
            StartPositionTimer();

            Logger.Log("Widget docked into taskbar");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to dock widget into taskbar", ex);
            PositionFloating();
        }
    }

    private void UpdatePosition()
    {
        if (!_isDockedInTaskbar || _taskbarHandle == IntPtr.Zero) return;

        try
        {
            NativeMethods.GetWindowRect(_taskbarHandle, out var taskbarRect);
            int taskbarWidth = taskbarRect.Width;
            int taskbarHeight = taskbarRect.Height;

            uint dpi = NativeMethods.GetDpiForWindow(_handle);
            double scale = dpi / 96.0;
            int widgetWidth = (int)(120 * scale);
            int widgetHeight = (int)(40 * scale);
            int padding = (int)(MainWindow.SettingsManager.TaskbarWidgetPadding * scale);

            int x = MainWindow.SettingsManager.TaskbarWidgetPosition switch
            {
                0 => padding,                                      // Left
                2 => taskbarWidth - widgetWidth - padding,         // Right
                _ => (taskbarWidth - widgetWidth) / 2,             // Center
            };
            int y = (taskbarHeight - widgetHeight) / 2;

            NativeMethods.SetWindowPos(_handle, IntPtr.Zero, x, y, widgetWidth, widgetHeight,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

            // Set click region
            IntPtr rgn = NativeMethods.CreateRectRgn(0, 0, widgetWidth, widgetHeight);
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
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _positionTimer.Tick += (_, _) => UpdatePosition();
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
        // Open settings window on click
        if (Application.Current.MainWindow is MainWindow mw)
            mw.OpenSettingsWindow();
    }

    public void RefreshPosition() => UpdatePosition();
}
