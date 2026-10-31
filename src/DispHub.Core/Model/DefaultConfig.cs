// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Core.Hotkeys;

namespace DispHub.Core.Model;

public static class DefaultConfig
{
    /// <summary>Names for the starter profiles; the app sets these to the user's language before first run.</summary>
    public static (string Standard, string Gaming, string Night) Names { get; set; } = ("Standard", "Gaming", "Night");

    /// <summary>
    /// Whether a hotkey would type a character on the user's keyboard (AltGr is Ctrl+Alt to Windows); the app sets
    /// it before first run, so the starter hotkeys leave typing alone.
    /// </summary>
    public static Func<HotkeyGesture, bool> TypesCharacter { get; set; } = _ => false;

    public static AppConfig Create() => Create(TypesCharacter);

    /// <summary>The starter settings, with starter hotkeys that <paramref name="typesCharacter"/> says leave typing alone.</summary>
    public static AppConfig Create(Func<HotkeyGesture, bool> typesCharacter)
    {
        ArgumentNullException.ThrowIfNull(typesCharacter);
        Func<char, HotkeyGesture>? set = StarterSets.FirstOrDefault(s => StarterDigits.All(d => !typesCharacter(s(d))));
        string? Starter(char digit) => (set is not null ? set(digit) : FirstFree(digit, typesCharacter))?.ToString();

        var standard = new Profile { Name = Names.Standard, Color = ColorSettings.Neutral, Hotkey = Starter('1') };
        var config = new AppConfig
        {
            Profiles =
            [
                standard,
                new Profile
                {
                    Name = Names.Gaming,
                    Color = new ColorSettings(Brightness: 5, Contrast: 5, Gamma: 1.15, Vibrance: 70),
                    Hotkey = Starter('2'),
                },
                new Profile
                {
                    Name = Names.Night,
                    Color = new ColorSettings(Brightness: -10, TemperatureK: 3600),
                    Hotkey = Starter('3'),
                },
            ],
            ActiveProfileId = standard.Id,
        };
        config.Hotkeys.TogglePause = Starter('0');
        return config;
    }

    private const string StarterDigits = "1230";
    private const int F1 = 0x70;

    // The starters, all four in one pattern so they're easy to remember: Ctrl+Alt and the digit; with Shift too where
    // AltGr types characters with the digits (Belgian, French); or the function keys where it types characters with
    // Shift as well (US-International: AltGr+1 is ¡ and AltGr+Shift+1 is ¹). Function keys never type anything.
    private static readonly Func<char, HotkeyGesture>[] StarterSets =
    [
        digit => new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, digit),
        digit => new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, digit),
        digit => new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, F1 + (digit == '0' ? 9 : digit - '1')),
    ];

    /// <summary>For a keyboard where no pattern is free for all four: the first one free for this key, if any.</summary>
    private static HotkeyGesture? FirstFree(char digit, Func<HotkeyGesture, bool> typesCharacter)
    {
        foreach (Func<char, HotkeyGesture> set in StarterSets)
        {
            HotkeyGesture gesture = set(digit);
            if (!typesCharacter(gesture))
                return gesture;
        }
        return null;
    }
}
