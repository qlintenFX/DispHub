// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Hotkeys;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace DispHub.Platform.Shell;

/// <summary>
/// What a hotkey would type instead. Windows sends AltGr as Ctrl+Alt, so on keyboards with AltGr (Belgian, French,
/// German, Spanish, Nordic and many more) a Ctrl+Alt hotkey would also catch AltGr+key and swallow the character it
/// types, such as @, # or { on the digit keys.
/// </summary>
public static class KeyboardTyping
{
    private const byte Down = 0x80;
    private const uint KeepKeyboardState = 0x4;

    /// <summary>The character <paramref name="gesture"/> types on any of the user's keyboard layouts, or null.</summary>
    public static unsafe string? Character(HotkeyGesture gesture)
    {
        // Only Ctrl+Alt is AltGr, and Windows doesn't type anything while Win is down.
        if (!gesture.Modifiers.HasFlag(HotkeyModifiers.Control) || !gesture.Modifiers.HasFlag(HotkeyModifiers.Alt) ||
            gesture.Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            return null;
        }

        Span<byte> keys = stackalloc byte[256];
        keys[(int)VIRTUAL_KEY.VK_CONTROL] = keys[(int)VIRTUAL_KEY.VK_LCONTROL] = Down;
        keys[(int)VIRTUAL_KEY.VK_MENU] = keys[(int)VIRTUAL_KEY.VK_RMENU] = Down;
        if (gesture.Modifiers.HasFlag(HotkeyModifiers.Shift))
            keys[(int)VIRTUAL_KEY.VK_SHIFT] = keys[(int)VIRTUAL_KEY.VK_LSHIFT] = Down;

        int count = PInvoke.GetKeyboardLayoutList(0, null);
        if (count <= 0)
            return null;
        var layouts = new HKL[count];
        count = PInvoke.GetKeyboardLayoutList(layouts);
        char* typed = stackalloc char[8];
        fixed (byte* state = keys)
        {
            foreach (HKL layout in layouts.AsSpan(0, Math.Max(count, 0)))
            {
                uint scanCode = PInvoke.MapVirtualKeyEx((uint)gesture.VirtualKey, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC, layout);
                int length = PInvoke.ToUnicodeEx((uint)gesture.VirtualKey, scanCode, state, typed, 8, KeepKeyboardState, layout);
                // A dead key (an accent waiting for the next letter) comes back as -1, with the accent in the buffer.
                if (length != 0 && !char.IsControl(typed[0]))
                    return new string(typed, 0, Math.Max(length, 1));
            }
        }
        return null;
    }
}
