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
            _widgetButtonBounds = null;
            _taskbarFrameBounds = null;
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

    // UI Automation element detection - non-blocking with caching
    // Returns cached values immediately; refreshes cache in background

    private (bool Found, Rect Bounds) GetTaskbarXamlElementRect(
        ref AutomationElement? cache, ref Rect? cachedBounds, string automationId)
    {
        if (_taskbarHandle == IntPtr.Zero) return (false, Rect.Empty);

        // Return cached bounds if available
        if (cache != null && cachedBounds.HasValue)
            return (true, cachedBounds.Value);

        // Kick off async refresh if not already running
        if (!_pendingAutomationTasks.TryGetValue(automationId, out var pending) || pending.IsCompleted)
        {
            var localCache = cache;
            var findTask = Task.Run(() =>
            {
                try
                {
                    if (localCache == null)
                    {
                        var root = AutomationElement.FromHandle(_taskbarHandle);
                        localCache = root.FindFirst(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
                    }
                    return localCache?.Current.BoundingRectangle ?? Rect.Empty;
                }
                catch { return Rect.Empty; }
            });

            _pendingAutomationTasks[automationId] = findTask;

            // Fire-and-forget update of cache when complete
            _ = findTask.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result != Rect.Empty)
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        if (automationId == "WidgetsButton")
                        {
                            _widgetButtonElement = localCache;
                            _widgetButtonBounds = t.Result;
                        }
                        else if (automationId == "TaskbarFrame")
                        {
                            _taskbarFrameElement = localCache;
                            _taskbarFrameBounds = t.Result;
                        }
                    });
                }
            }, TaskScheduler.Default);
        }

        return (false, Rect.Empty);
    }

    private Rect? _widgetButtonBounds;
    private Rect? _taskbarFrameBounds;

    private (bool, Rect) GetWidgetsButtonRect()
        => GetTaskbarXamlElementRect(ref _widgetButtonElement, ref _widgetButtonBounds, "WidgetsButton");

    private (bool, Rect) GetTaskbarFrameRect()
        => GetTaskbarXamlElementRect(ref _taskbarFrameElement, ref _taskbarFrameBounds, "TaskbarFrame");

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
            case 0: // Left - position after Widgets button (FluentFlyout approach)
            {
                int widgetLeft = 20;

                if (autoPadding)
                {
                    // Use UI Automation to find WidgetsButton
                    var (found, wbRect) = GetWidgetsButtonRect();
                    // Make sure it's on the left side, otherwise ignore
                    if (found && wbRect.Right < (taskbarRect.Left + taskbarRect.Right) / 2.0)
                    {
                        widgetLeft = (int)(wbRect.Right - taskbarRect.Left) + 2;
                    }
                }

                return widgetLeft + manualPadding;
            }

            case 1: // Center - true center of the taskbar (FluentFlyout approach)
            {
                int widgetLeft = (taskbarWidth - widgetWidth) / 2;
                return widgetLeft + manualPadding;
            }

            case 2: // Right - next to system tray with auto padding for widgets button
            {
                int widgetLeft = taskbarWidth - widgetWidth - 20;

                // Try to position next to widgets button if auto padding is enabled
                if (autoPadding)
                {
                    var (found, wbRect) = GetWidgetsButtonRect();
                    // Make sure it's on the right side, otherwise fall back to tray
                    if (found && wbRect.Left > (taskbarRect.Left + taskbarRect.Right) / 2.0)
                    {
                        widgetLeft = (int)(wbRect.Left - taskbarRect.Left) - widgetWidth - 2;
                        return widgetLeft + manualPadding;
                    }
                }

                // Fall back to position next to system tray
                try
                {
                    if (_trayHandle == IntPtr.Zero)
                        _trayHandle = NativeMethods.FindWindowEx(
                            _taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);

                    if (_trayHandle != IntPtr.Zero)
                    {
                        NativeMethods.GetWindowRect(_trayHandle, out var trayRect);
                        widgetLeft = trayRect.Left - taskbarRect.Left - widgetWidth - 4;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to get tray position", ex);
                }

                return widgetLeft + manualPadding;
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
        _widgetButtonBounds = null;
        _taskbarFrameBounds = null;
        CalculateAndSetPosition();
    }
}
