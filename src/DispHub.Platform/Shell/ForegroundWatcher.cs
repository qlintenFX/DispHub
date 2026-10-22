// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.Accessibility;

namespace DispHub.Platform.Shell;

public interface IForegroundWatcher : IDisposable
{
    /// <summary>Full path of the foreground program's executable (or just its file name), or null if unknown.</summary>
    string? Current { get; }

    /// <summary>Raised on the UI thread when a different program comes to the foreground.</summary>
    event EventHandler? Changed;

    void Start();
}

/// <summary>
/// Watches which program is in the foreground for per-app profiles. Hooks EVENT_SYSTEM_FOREGROUND and
/// EVENT_SYSTEM_MINIMIZEEND (a window restored from the taskbar doesn't always report the first), out of context:
/// hooking frequent events causes frame-time spikes in games on recent NVIDIA drivers.
/// Windows can still miss an event, for example while a game switches to full screen, so a check once a second
/// compares the foreground process off the UI thread and only reports when it differs.
/// Must be started on a thread with a message loop (the UI thread).
/// </summary>
public sealed unsafe class ForegroundWatcher : IForegroundWatcher
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static ForegroundWatcher? _instance;
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private UnhookWinEventSafeHandle? _foregroundHook;
    private UnhookWinEventSafeHandle? _restoreHook;
    private Timer? _timer;
    private SynchronizationContext? _ui;
    private uint _processId;

    public string? Current { get; private set; }

    public event EventHandler? Changed;

    public void Start()
    {
        if (_foregroundHook is not null)
            return;
        _instance = this;
        _ui = SynchronizationContext.Current;
        _foregroundHook = Hook(PInvoke.EVENT_SYSTEM_FOREGROUND);
        _restoreHook = Hook(PInvoke.EVENT_SYSTEM_MINIMIZEEND);
        _timer = new Timer(_ => CheckInBackground(), null, CheckInterval, CheckInterval);
        Update(PInvoke.GetForegroundWindow());
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        _foregroundHook?.Dispose();
        _foregroundHook = null;
        _restoreHook?.Dispose();
        _restoreHook = null;
        if (ReferenceEquals(_instance, this))
            _instance = null;
    }

    public static string? GetProcessPath(uint processId)
    {
        using var process = new SafeProcessHandle(
            PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId),
            ownsHandle: true);
        if (process.IsInvalid)
            return null;

        Span<char> buffer = stackalloc char[1024];
        uint length = (uint)buffer.Length;
        return PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, buffer, ref length)
            ? buffer[..(int)length].ToString()
            : null;
    }

    /// <summary>The executable's path, or at least its file name when Windows won't open the process.</summary>
    private static string? GetProgram(uint processId)
    {
        if (GetProcessPath(processId) is { } path)
            return path;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName + ".exe";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null; // It has already exited.
        }
    }

    private static UnhookWinEventSafeHandle Hook(uint eventType) =>
        PInvoke.SetWinEventHook(eventType, eventType, null, &OnEvent, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);

    /// <summary>Runs on a pool thread: cheap calls only, and the UI thread is woken only for a new process.</summary>
    private void CheckInBackground()
    {
        HWND window = PInvoke.GetForegroundWindow();
        _ = PInvoke.GetWindowThreadProcessId(window, out uint processId);
        if (processId != 0 && processId != _ownProcessId && processId != Volatile.Read(ref _processId))
            _ui?.Post(_ => Update(window), null);
    }

    private void Update(HWND window)
    {
        if (window.IsNull)
            return;
        _ = PInvoke.GetWindowThreadProcessId(window, out uint processId);

        // DispHub's own windows don't count: the app behind them keeps its profile.
        if (processId == 0 || processId == _ownProcessId || processId == _processId)
            return;
        Volatile.Write(ref _processId, processId);
        string? program = GetProgram(processId);
        if (string.Equals(program, Current, StringComparison.OrdinalIgnoreCase))
            return;
        Current = program;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnEvent(HWINEVENTHOOK hook, uint eventType, HWND hwnd, int objectId, int childId, uint thread, uint time)
    {
        try
        {
            // A restored window isn't always the foreground one yet; ask Windows which one is.
            _instance?.Update(eventType == PInvoke.EVENT_SYSTEM_FOREGROUND ? hwnd : PInvoke.GetForegroundWindow());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Never let an exception escape into the native caller.
        }
    }
}
