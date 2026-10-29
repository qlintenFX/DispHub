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
        string? Starter(char digit) => StarterHotkey(digit, typesCharacter);

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

    /// <summary>Ctrl+Alt and the digit, or with Shift too where AltGr types a character with it; none if both do.</summary>
    private static string? StarterHotkey(char digit, Func<HotkeyGesture, bool> typesCharacter)
    {
        foreach (HotkeyModifiers modifiers in new[] { HotkeyModifiers.Control | HotkeyModifiers.Alt, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift })
        {
            var gesture = new HotkeyGesture(modifiers, digit);
            if (!typesCharacter(gesture))
                return gesture.ToString();
        }
        return null;
    }
}
