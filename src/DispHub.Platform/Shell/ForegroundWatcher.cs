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
    /// <summary>Full path of the foreground program's executable, or null if unknown.</summary>
    string? Current { get; }

    /// <summary>Raised on the UI thread when a different program comes to the foreground.</summary>
    event EventHandler? Changed;

    void Start();
}

/// <summary>
/// Watches which program is in the foreground for per-app profiles. Hooks only EVENT_SYSTEM_FOREGROUND,
/// out of context: hooking more events causes frame-time spikes in games on recent NVIDIA drivers.
/// Must be started on a thread with a message loop (the UI thread).
/// </summary>
public sealed unsafe class ForegroundWatcher : IForegroundWatcher
{
    private static ForegroundWatcher? _instance;
    private UnhookWinEventSafeHandle? _hook;

    public string? Current { get; private set; }

    public event EventHandler? Changed;

    public void Start()
    {
        if (_hook is not null)
            return;
        _instance = this;
        _hook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            PInvoke.EVENT_SYSTEM_FOREGROUND,
            null,
            &OnEvent,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
    }

    public void Dispose()
    {
        _hook?.Dispose();
        _hook = null;
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnEvent(HWINEVENTHOOK hook, uint eventType, HWND hwnd, int objectId, int childId, uint thread, uint time)
    {
        ForegroundWatcher? watcher = _instance;
        if (watcher is null || hwnd.IsNull)
            return;
        try
        {
            _ = PInvoke.GetWindowThreadProcessId(hwnd, out uint processId);
            string? path = processId == 0 ? null : GetProcessPath(processId);
            if (string.Equals(path, watcher.Current, StringComparison.OrdinalIgnoreCase))
                return;
            watcher.Current = path;
            watcher.Changed?.Invoke(watcher, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Never let an exception escape into the native caller.
        }
    }
}
