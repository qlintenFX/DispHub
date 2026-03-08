// SPDX-License-Identifier: GPL-3.0-or-later
// Widget positioning approach adapted from FluentFlyout by Hugo Li (unchihugo)
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
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

    // UI Automation element caches (FluentFlyout approach)
    private AutomationElement? _widgetButtonElement;
    private AutomationElement? _taskbarFrameElement;
    private readonly Dictionary<string, Task> _pendingAutomationTasks = [];

    public TaskbarWidgetWindow()
    {
        // Set WS_EX_NOACTIVATE before window is shown (FluentFlyout pattern)
        SourceInitialized += (_, _) =>
        {
            var helper = new WindowInteropHelper(this);
            int exStyle = NativeMethods.GetWindowLong(helper.Handle, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(helper.Handle, NativeMethods.GWL_EXSTYLE,
                exStyle | NativeMethods.WS_EX_NOACTIVATE);
        };

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
        // Prevent taskbar interface tools from querying this window (FluentFlyout pattern)
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
            _trayHandle = IntPtr.Zero;
            _widgetButtonElement = null;
            _taskbarFrameElement = null;
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
                Logger.Log("Taskbar not found - floating widget");
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

    // UI Automation element detection (FluentFlyout approach)

    private (bool Found, Rect Bounds) GetTaskbarXamlElementRect(
        ref AutomationElement? cache, string automationId)
    {
        if (_taskbarHandle == IntPtr.Zero) return (false, Rect.Empty);

        try
        {
            if (cache == null)
            {
                if (_pendingAutomationTasks.TryGetValue(automationId, out var pending) && !pending.IsCompleted)
                    return (false, Rect.Empty);

                AutomationElement? found = null;
                var findTask = Task.Run(() =>
                {
                    var root = AutomationElement.FromHandle(_taskbarHandle);
                    found = root.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
                });
                _pendingAutomationTasks[automationId] = findTask;

                if (!findTask.Wait(1000))
                    return (false, Rect.Empty);

                findTask.GetAwaiter().GetResult();
                cache = found;
            }

            if (cache == null) return (false, Rect.Empty);

            try
            {
                if (_pendingAutomationTasks.TryGetValue(automationId, out var pending2) && !pending2.IsCompleted)
                {
                    cache = null;
                    return (false, Rect.Empty);
                }

                var cachedElement = cache;
                var boundsTask = Task.Run(() => cachedElement.Current.BoundingRectangle);
                _pendingAutomationTasks[automationId] = boundsTask;

                if (!boundsTask.Wait(500))
                {
                    cache = null;
                    return (false, Rect.Empty);
                }

                var rect = boundsTask.GetAwaiter().GetResult();
                if (rect == Rect.Empty)
                {
                    cache = null;
                    return (false, Rect.Empty);
                }
                return (true, rect);
            }
            catch (ElementNotAvailableException)
            {
                cache = null;
                return (false, Rect.Empty);
            }
        }
        catch (COMException)
        {
            cache = null;
            return (false, Rect.Empty);
        }
        catch (Exception)
        {
            cache = null;
            return (false, Rect.Empty);
        }
    }

    private (bool, Rect) GetWidgetsButtonRect()
        => GetTaskbarXamlElementRect(ref _widgetButtonElement, "WidgetsButton");

    private (bool, Rect) GetTaskbarFrameRect()
        => GetTaskbarXamlElementRect(ref _taskbarFrameElement, "TaskbarFrame");

    // Position calculation

    private void CalculateAndSetPosition()
    {
        if (!_isDockedInTaskbar || _taskbarHandle == IntPtr.Zero) return;
        if (_positionUpdateInProgress) return;
        _positionUpdateInProgress = true;

        try
        {
            double dpiScale = NativeMethods.GetDpiForWindow(_taskbarHandle) / 96.0;
            if (dpiScale <= 0) dpiScale = 1.0;

            // Get taskbar bounds - prefer TaskbarFrame via UI Automation (FluentFlyout approach)
            NativeMethods.RECT taskbarRect;
            var (frameFound, frameRect) = GetTaskbarFrameRect();
            if (frameFound)
            {
                taskbarRect = new NativeMethods.RECT
                {
                    Left = (int)frameRect.Left,
                    Top = (int)frameRect.Top,
                    Right = (int)frameRect.Right,
                    Bottom = (int)frameRect.Bottom
                };
            }
            else
            {
                NativeMethods.GetWindowRect(_taskbarHandle, out taskbarRect);
            }

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
            case 0: // Left - position after Widgets button
            {
                int widgetLeft = 20;

                if (autoPadding)
                {
                    // Use UI Automation to find WidgetsButton (FluentFlyout approach)
                    var (found, wbRect) = GetWidgetsButtonRect();
                    if (found && wbRect.Right < (taskbarRect.Left + taskbarRect.Right) / 2.0)
                        widgetLeft = (int)(wbRect.Right - taskbarRect.Left) + 2;
                }

                return widgetLeft + manualPadding;
            }

            case 1: // Center - position in left gap, before Start button area
            {
                int leftBound = 20;
                var (found, wbRect) = GetWidgetsButtonRect();
                if (found && wbRect.Right < (taskbarRect.Left + taskbarRect.Right) / 2.0)
                    leftBound = (int)(wbRect.Right - taskbarRect.Left) + 2;

                // Center of the left gap between left elements and center Start area
                int centerOfTaskbar = taskbarWidth / 2;
                int gapCenter = (leftBound + centerOfTaskbar) / 2;
                int widgetLeft = gapCenter - widgetWidth / 2;

                widgetLeft = Math.Max(widgetLeft, leftBound + 4);
                widgetLeft = Math.Min(widgetLeft, centerOfTaskbar - widgetWidth - 20);

                return widgetLeft + manualPadding;
            }

            case 2: // Right - to the left of system tray
            {
                try
                {
                    if (_trayHandle == IntPtr.Zero)
                        _trayHandle = NativeMethods.FindWindowEx(
                            _taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);

                    if (_trayHandle != IntPtr.Zero)
                    {
                        NativeMethods.GetWindowRect(_trayHandle, out var trayRect);
                        int widgetLeft = trayRect.Left - taskbarRect.Left - widgetWidth - 1;
                        return widgetLeft + manualPadding;
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
        _trayHandle = IntPtr.Zero;
        _widgetButtonElement = null;
        _taskbarFrameElement = null;
        CalculateAndSetPosition();
    }
}
