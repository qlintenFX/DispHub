// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;

namespace DispHub.Core.Hotkeys;

/// <summary>Modifier flags with the same values as the Win32 MOD_* constants.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A global hotkey such as "Ctrl+Alt+F1". Stored in config as its string form.</summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>
    /// A gesture is usable globally if it has Ctrl, Alt or Win, or is a function key. Shift alone is not
    /// enough: it would steal ordinary typing (e.g. Shift+Arrow text selection) system-wide.
    /// </summary>
    public bool IsValid =>
        KeyNames.IsKnown(VirtualKey) &&
        ((Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0 ||
         KeyNames.IsFunctionKey(VirtualKey));

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win))
            parts.Add("Win");
        parts.Add(KeyNames.GetName(VirtualKey));
        return string.Join('+', parts);
    }

    public static bool TryParse([NotNullWhen(true)] string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var modifiers = HotkeyModifiers.None;
        int? key = null;
        foreach (string raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            HotkeyModifiers modifier = raw.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Control,
                "ALT" => HotkeyModifiers.Alt,
                "SHIFT" => HotkeyModifiers.Shift,
                "WIN" or "WINDOWS" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None,
            };

            if (modifier != HotkeyModifiers.None)
            {
                modifiers |= modifier;
            }
            else if (key is null && KeyNames.TryGetKey(raw, out int vk))
            {
                key = vk;
            }
            else
            {
                return false;
            }
        }

        if (key is null)
            return false;

        gesture = new HotkeyGesture(modifiers, key.Value);
        return gesture.IsValid;
    }
}
