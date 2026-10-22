// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;

namespace DispHub.Core.Model;

public static class DefaultConfig
{
    /// <summary>Names for the starter profiles; the app sets these to the user's language before first run.</summary>
    public static (string Standard, string Gaming, string Night) Names { get; set; } = ("Standard", "Gaming", "Night");

    public static AppConfig Create()
    {
        var standard = new Profile { Name = Names.Standard, Color = ColorSettings.Neutral, Hotkey = "Ctrl+Alt+1" };
        var config = new AppConfig
        {
            Profiles =
            [
                standard,
                new Profile
                {
                    Name = Names.Gaming,
                    Color = new ColorSettings(Brightness: 5, Contrast: 5, Gamma: 1.15, Vibrance: 70),
                    Hotkey = "Ctrl+Alt+2",
                },
                new Profile
                {
                    Name = Names.Night,
                    Color = new ColorSettings(Brightness: -10, TemperatureK: 3600),
                    Hotkey = "Ctrl+Alt+3",
                },
            ],
            ActiveProfileId = standard.Id,
        };
        config.Hotkeys.TogglePause = "Ctrl+Alt+0";
        return config;
    }
}
