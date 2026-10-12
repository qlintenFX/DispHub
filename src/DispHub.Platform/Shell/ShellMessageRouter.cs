using Windows.Win32;
using Windows.Win32.Foundation;

namespace DispHub.Platform.Shell;

/// <summary>
/// Turns the hidden window's raw messages into events. A real top-level window is needed (not a message-only
/// one) because display, power and taskbar broadcasts aren't sent to message-only windows.
/// </summary>
public sealed class ShellMessageRouter : IDisposable
{
    private readonly HWND _hwnd;
    private readonly uint _taskbarCreated;
    private readonly bool _sessionNotifications;

    public ShellMessageRouter(nint windowHandle, HotkeyRegistrar hotkeys)
    {
        _hwnd = (HWND)windowHandle;
        Hotkeys = hotkeys;
        _taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
        _sessionNotifications = PInvoke.WTSRegisterSessionNotification(_hwnd, PInvoke.NOTIFY_FOR_THIS_SESSION);
    }

    public HotkeyRegistrar Hotkeys { get; }

    /// <summary>The notification icon whose messages arrive at this window, once it exists.</summary>
    public TrayIcon? Tray { get; set; }

    public event EventHandler? DisplayChanged;
    public event EventHandler? Resumed;
    public event EventHandler? SessionUnlocked;
    public event EventHandler? TaskbarCreated;
    public event EventHandler? DpiChanged;

    /// <summary>Call from the window procedure. Returns true when the message was fully handled.</summary>
    public bool HandleMessage(uint message, nuint wParam, nint lParam)
    {
        if (Hotkeys.HandleMessage(message, wParam))
            return true;
        if (Tray?.HandleMessage(message, wParam, lParam) == true)
            return true;

        if (message == PInvoke.WM_DISPLAYCHANGE)
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        else if (message == PInvoke.WM_POWERBROADCAST && wParam == PInvoke.PBT_APMRESUMEAUTOMATIC)
            Resumed?.Invoke(this, EventArgs.Empty);
        else if (message == PInvoke.WM_WTSSESSION_CHANGE && wParam == PInvoke.WTS_SESSION_UNLOCK)
            SessionUnlocked?.Invoke(this, EventArgs.Empty);
        else if (message == _taskbarCreated && _taskbarCreated != 0)
            TaskbarCreated?.Invoke(this, EventArgs.Empty);
        else if (message == PInvoke.WM_DPICHANGED)
            DpiChanged?.Invoke(this, EventArgs.Empty);

        return false;
    }

    public void Dispose()
    {
        if (_sessionNotifications)
            PInvoke.WTSUnRegisterSessionNotification(_hwnd);
    }
}
