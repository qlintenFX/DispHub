// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using DispHub.Models;

namespace DispHub.Services.Hotkeys;

public sealed class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private int _nextId = 1;
    private readonly Dictionary<int, Profile> _profileHotkeys = new();
    private readonly IntPtr _hwnd;
    private bool _disposed;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;
    public IntPtr Hwnd => _hwnd;

    public HotkeyManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    /// <summary>Register a profile hotkey. Returns the assigned ID, or -1 on failure.</summary>
    public int RegisterHotkey(Profile profile)
    {
        if (profile.HotKeyValue == 0) return -1;

        int id = _nextId++;
        if (RegisterHotKey(_hwnd, id, profile.HotKeyModifierValue, (uint)profile.HotKeyValue))
        {
            _profileHotkeys[id] = profile;
            profile.HotkeyId = id;
            return id;
        }
        return -1;
    }

    /// <summary>Register a raw (non-profile) hotkey. Returns the assigned ID, or -1 on failure.</summary>
    public int RegisterRawHotkey(uint vk, uint modifiers)
    {
        int id = _nextId++;
        return RegisterHotKey(_hwnd, id, modifiers, vk) ? id : -1;
    }

    public bool UnregisterRawHotkey(int id)
    {
        return id > 0 && UnregisterHotKey(_hwnd, id);
    }

    public bool UnregisterHotkey(int id)
    {
        if (id <= 0) return false;
        bool ok = UnregisterHotKey(_hwnd, id);
        _profileHotkeys.Remove(id);
        return ok;
    }

    public void ProcessHotkey(IntPtr wParam)
    {
        int id = wParam.ToInt32();
        if (_profileHotkeys.TryGetValue(id, out var profile))
            HotkeyPressed?.Invoke(this, new HotkeyEventArgs(profile));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (int id in _profileHotkeys.Keys)
            UnregisterHotKey(_hwnd, id);
        _profileHotkeys.Clear();
    }
}

public sealed class HotkeyEventArgs(Profile profile) : EventArgs
{
    public Profile Profile { get; } = profile;
}
