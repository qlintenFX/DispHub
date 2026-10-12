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

    /// <summary>Paused means DispHub has restored the original colours and ignores rules until resumed.</summary>
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

    public Guid ProfileId { get; set; }
}

/// <summary>Time-of-day profile switching (Pro).</summary>
public sealed class ScheduleConfig
{
    public bool Enabled { get; set; }
    public List<ScheduleSlot> Slots { get; set; } = [];
}

public sealed class ScheduleSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TimeOnly Start { get; set; }
    public Guid ProfileId { get; set; }

    /// <summary>Minutes to fade from the previous slot's profile into this one. 0 switches instantly.</summary>
    public int FadeMinutes { get; set; }
}

public sealed class HotkeyConfig
{
    public string? TogglePause { get; set; }
    public string? NextProfile { get; set; }
    public string? PreviousProfile { get; set; }
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
}

public sealed class UiConfig
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public bool CloseToTray { get; set; } = true;
    public TrayClickAction TrayLeftClick { get; set; } = TrayClickAction.OpenWindow;
    public bool ShowOsd { get; set; } = true;

    /// <summary>UI language as a culture name such as "de"; null follows Windows.</summary>
    public string? Language { get; set; }
    public int OsdDurationMs { get; set; } = 1500;

    /// <summary>The photo the profile previews show, by name; null uses the default.</summary>
    public string? PreviewPicture { get; set; }

    /// <summary>The one-time "still running" notification was shown.</summary>
    public bool TrayHintShown { get; set; }
}
