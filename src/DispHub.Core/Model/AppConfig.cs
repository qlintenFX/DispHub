// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json.Serialization;
using DispHub.Core.Color;

namespace DispHub.Core.Model;

/// <summary>Everything the user chose. Saved as config.json.</summary>
public sealed class AppConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public List<Profile> Profiles { get; set; } = [];

    /// <summary>The profile applied when no automation rule is active.</summary>
    public Guid? ActiveProfileId { get; set; }

    /// <summary>
    /// The master switch is off: original colours and no automation, until it's turned back on.
    /// The power button in the window and the quick panel, the tray menu and the on/off hotkey use it.
    /// </summary>
    public bool IsOff { get; set; }

    /// <summary>Profiles are off (the switch on the Profiles page): original colours and no automation.</summary>
    public bool IsPaused { get; set; }

    /// <summary>Per-monitor options keyed by monitor device path (stable across reboots).</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, MonitorConfig> Monitors { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<AppRule> AppRules { get; set; } = [];
    public ScheduleConfig Schedule { get; set; } = new();
    public HotkeyConfig Hotkeys { get; set; } = new();
    public UiConfig Ui { get; set; } = new();
}

public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Profile";
    public ColorSettings Color { get; set; } = ColorSettings.Neutral;

    /// <summary>Hotkey in <see cref="Hotkeys.HotkeyGesture"/> string form, e.g. "Ctrl+Alt+1".</summary>
    public string? Hotkey { get; set; }

    /// <summary>Optional per-monitor settings that replace <see cref="Color"/> on that monitor (Pro).</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, ColorSettings> MonitorOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ColorSettings SettingsFor(string monitorKey, bool allowOverrides) =>
        allowOverrides && MonitorOverrides.TryGetValue(monitorKey, out ColorSettings? settings) ? settings : Color;
}

public sealed class MonitorConfig
{
    /// <summary>Friendly name remembered for display while the monitor is disconnected.</summary>
    public string? Name { get; set; }

    /// <summary>When true DispHub leaves this monitor untouched.</summary>
    public bool Excluded { get; set; }
}

/// <summary>Switches to a profile while a given program is in the foreground (Pro).</summary>
public sealed class AppRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; } = true;

    /// <summary>Executable file name such as "cs2.exe", or a full path.</summary>
    public string Executable { get; set; } = "";

    /// <summary>Where the program was found, for its name and icon; matching uses <see cref="Executable"/>.</summary>
    public string? Path { get; set; }

    public Guid ProfileId { get; set; }
}

/// <summary>Time-of-day profile switching (Pro).</summary>
public sealed class ScheduleConfig
{
    public bool Enabled { get; set; }

    /// <summary>
    /// How the Schedule page shows <see cref="Slots"/>. The two simple modes are always exactly two slots: the
    /// night profile first, then the day profile.
    /// </summary>
    public ScheduleMode Mode { get; set; }

    public List<ScheduleSlot> Slots { get; set; } = [];

    /// <summary>Where sunrise and sunset are calculated for; null until the user sets it.</summary>
    public GeoLocation? Location { get; set; }
}

public enum ScheduleMode
{
    /// <summary>A night profile from sunset, a day profile from sunrise.</summary>
    SunsetToSunrise,

    /// <summary>A night profile between two clock times.</summary>
    SetHours,

    /// <summary>Any number of changes, each at a time or at sunrise or sunset, on chosen days.</summary>
    Custom,
}

/// <summary>What a slot's start time is measured from.</summary>
public enum ScheduleAnchor
{
    Time,
    Sunrise,
    Sunset,
}

[Flags]
public enum ScheduleDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    Every = Weekdays | Weekend,
}

public sealed class ScheduleSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The clock time the slot starts, when <see cref="Anchor"/> is <see cref="ScheduleAnchor.Time"/>.</summary>
    public TimeOnly Start { get; set; }

    public ScheduleAnchor Anchor { get; set; }

    /// <summary>Minutes after sunrise or sunset (negative: before) when anchored to the sun.</summary>
    public int OffsetMinutes { get; set; }

    public ScheduleDays Days { get; set; } = ScheduleDays.Every;

    public Guid ProfileId { get; set; }

    /// <summary>Minutes to fade from the previous slot's profile into this one. 0 switches instantly.</summary>
    public int FadeMinutes { get; set; }
}

public sealed class GeoLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>A place name to show, such as a city.</summary>
    public string? Name { get; set; }
}

public sealed class HotkeyConfig
{
    public string? TogglePause { get; set; }
    public string? NextProfile { get; set; }
    public string? PreviousProfile { get; set; }
    public string? QuickPanel { get; set; }
}

public enum WindowBackdrop
{
    Mica,
    MicaAlt,

    /// <summary>Blurs what's behind the window.</summary>
    Acrylic,
    Solid,
}

public enum OsdPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public enum TrayClickAction
{
    OpenWindow,
    ShowProfileMenu,
    QuickPanel,
}

public sealed class UiConfig
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public bool CloseToTray { get; set; } = true;
    public TrayClickAction TrayLeftClick { get; set; } = TrayClickAction.QuickPanel;
    public bool ShowOsd { get; set; } = true;

    /// <summary>UI language as a culture name such as "de"; null follows Windows.</summary>
    public string? Language { get; set; }
    public int OsdDurationMs { get; set; } = 1500;

    /// <summary>The photo the profile previews show, by name; null uses the default.</summary>
    public string? PreviewPicture { get; set; }

    /// <summary>The one-time "still running" notification was shown.</summary>
    public bool TrayHintShown { get; set; }

    /// <summary>The first-run walkthrough was finished or closed.</summary>
    public bool OnboardingDone { get; set; }

    /// <summary>The page the window opens on, by name; null for Profiles.</summary>
    public string? StartPage { get; set; }

    /// <summary>Accent colour as #RRGGBB; null follows Windows.</summary>
    public string? AccentColor { get; set; }

    public WindowBackdrop Backdrop { get; set; } = WindowBackdrop.Mica;

    /// <summary>
    /// How solid the acrylic of the quick panel and on-screen messages is, from 0 (only the blurred background shows) to 1 (no see-through).
    /// Pro; free users always get <see cref="DefaultPanelOpacity"/>.
    /// </summary>
    public double PanelOpacity { get; set; } = DefaultPanelOpacity;

    /// <summary>The acrylic strength Windows' own flyouts are closest to (FluentFlyout's 175 of 255).</summary>
    public const double DefaultPanelOpacity = 0.7;

    /// <summary>Show the navigation as icons only.</summary>
    public bool CompactNavigation { get; set; }

    /// <summary>Text and control size, 0.9 to 1.25.</summary>
    public double TextScale { get; set; } = 1.0;

    /// <summary>Turn off animated scrolling and transitions, whatever Windows' animation setting is.</summary>
    public bool ReduceMotion { get; set; }

    public OsdPosition OsdPosition { get; set; } = OsdPosition.BottomCenter;
}
