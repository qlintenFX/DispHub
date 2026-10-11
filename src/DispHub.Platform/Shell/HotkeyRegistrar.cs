using System.Runtime.InteropServices;
using DispHub.Core.Hotkeys;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace DispHub.Platform.Shell;

public interface IHotkeyRegistrar
{
    /// <summary>Raised on the UI thread with the action ID passed to <see cref="Sync"/>.</summary>
    event EventHandler<string>? Pressed;

    /// <summary>
    /// Makes the registered hotkeys match <paramref name="desired"/> (action ID to gesture) and returns, per action
    /// that couldn't be registered, a short reason.
    /// </summary>
    IReadOnlyDictionary<string, string> Sync(IReadOnlyDictionary<string, HotkeyGesture> desired);

    /// <summary>Temporarily releases every hotkey, e.g. while the user records a new one.</summary>
    void SuspendAll();

    void ResumeAll();
}

/// <summary>Registers global hotkeys on the app's hidden window. Use from the UI thread only.</summary>
public sealed class HotkeyRegistrar(nint windowHandle) : IHotkeyRegistrar
{
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private readonly HWND _hwnd = (HWND)windowHandle;
    private readonly Dictionary<string, (int Id, HotkeyGesture Gesture)> _registered = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _actions = [];
    private IReadOnlyDictionary<string, HotkeyGesture> _desired = new Dictionary<string, HotkeyGesture>();
    private bool _suspended;
    private int _nextId = 1;

    public event EventHandler<string>? Pressed;

    public IReadOnlyDictionary<string, string> Sync(IReadOnlyDictionary<string, HotkeyGesture> desired)
    {
        ArgumentNullException.ThrowIfNull(desired);
        _desired = desired;
        return _suspended ? new Dictionary<string, string>() : SyncNow();
    }

    public void SuspendAll()
    {
        _suspended = true;
        foreach (string action in _registered.Keys.ToList())
            Unregister(action);
    }

    public void ResumeAll()
    {
        _suspended = false;
        SyncNow();
    }

    /// <summary>Feed window messages here; returns true for hotkey messages.</summary>
    public bool HandleMessage(uint message, nuint wParam)
    {
        if (message != PInvoke.WM_HOTKEY)
            return false;
        if (!_suspended && _actions.TryGetValue((int)wParam, out string? action))
            Pressed?.Invoke(this, action);
        return true;
    }

    private Dictionary<string, string> SyncNow()
    {
        var failures = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach ((string action, (int _, HotkeyGesture gesture)) in _registered.ToList())
        {
            if (!_desired.TryGetValue(action, out HotkeyGesture wanted) || wanted != gesture)
                Unregister(action);
        }

        var claimed = _registered.ToDictionary(r => r.Value.Gesture, r => r.Key);
        foreach ((string action, HotkeyGesture gesture) in _desired)
        {
            if (_registered.ContainsKey(action))
                continue;

            if (claimed.TryGetValue(gesture, out string? owner))
            {
                failures[action] = $"Already used by {owner}";
                continue;
            }

            int id = _nextId++;
            var modifiers = (HOT_KEY_MODIFIERS)gesture.Modifiers | HOT_KEY_MODIFIERS.MOD_NOREPEAT;
            if (PInvoke.RegisterHotKey(_hwnd, id, modifiers, (uint)gesture.VirtualKey))
            {
                _registered[action] = (id, gesture);
                _actions[id] = action;
                claimed[gesture] = action;
            }
            else
            {
                failures[action] = Marshal.GetLastPInvokeError() == ErrorHotkeyAlreadyRegistered
                    ? "Used by another app"
                    : "Windows refused this hotkey";
            }
        }
        return failures;
    }

    private void Unregister(string action)
    {
        if (!_registered.Remove(action, out var entry))
            return;
        PInvoke.UnregisterHotKey(_hwnd, entry.Id);
        _actions.Remove(entry.Id);
    }
}
