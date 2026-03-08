using System.Runtime.InteropServices;
using DisplayHub.Models;

namespace DisplayHub.Services.Hotkeys;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private int _nextHotkeyId = 1;
    private readonly Dictionary<int, Profile> _registeredHotkeys = new();
    private readonly IntPtr _hwnd;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;
    public IntPtr Hwnd => _hwnd;

    public HotkeyManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public int RegisterHotkey(Profile profile)
    {
        if (profile.HotKeyValue == 0) return -1;

        uint modifiers = profile.HotKeyModifierValue;
        uint key = (uint)profile.HotKeyValue;

        int id = _nextHotkeyId++;
        if (RegisterHotKey(_hwnd, id, modifiers, key))
        {
            _registeredHotkeys[id] = profile;
            profile.HotkeyId = id;
            return id;
        }
        return -1;
    }

    public int RegisterRawHotkey(uint vk, uint modifiers)
    {
        int id = _nextHotkeyId++;
        if (RegisterHotKey(_hwnd, id, modifiers, vk))
            return id;
        return -1;
    }

    public bool UnregisterRawHotkey(int id)
    {
        if (id <= 0) return false;
        return UnregisterHotKey(_hwnd, id);
    }

    public bool UnregisterHotkey(int id)
    {
        if (id <= 0) return false;
        bool result = UnregisterHotKey(_hwnd, id);
        if (result) _registeredHotkeys.Remove(id);
        return result;
    }

    public void UnregisterAllHotkeys()
    {
        foreach (int id in _registeredHotkeys.Keys)
            UnregisterHotKey(_hwnd, id);
        _registeredHotkeys.Clear();
    }

    public void ProcessHotkey(IntPtr wParam)
    {
        int id = wParam.ToInt32();
        if (_registeredHotkeys.TryGetValue(id, out Profile? profile) && profile != null)
            HotkeyPressed?.Invoke(this, new HotkeyEventArgs(profile));
    }

    public void Dispose() => UnregisterAllHotkeys();
}

public class HotkeyEventArgs : EventArgs
{
    public Profile Profile { get; }
    public HotkeyEventArgs(Profile profile) { Profile = profile; }
}
