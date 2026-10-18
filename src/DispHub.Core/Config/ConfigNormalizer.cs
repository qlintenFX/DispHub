using DispHub.Core.Color;
using DispHub.Core.Effects;
using DispHub.Core.Hotkeys;
using DispHub.Core.Model;
using DispHub.Core.Schedule;

namespace DispHub.Core.Config;

/// <summary>Repairs anything in a loaded config that the rest of the app would trip over.</summary>
public static class ConfigNormalizer
{
    public const int MaxNameLength = 40;

    /// <summary>Fixes <paramref name="config"/> in place and returns a description of each fix.</summary>
    public static IReadOnlyList<string> Normalize(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var fixes = new List<string>();

        config.Profiles ??= [];
        config.Monitors ??= new(StringComparer.OrdinalIgnoreCase);
        config.AppRules ??= [];
        config.Schedule ??= new();
        config.Schedule.Slots ??= [];
        config.Hotkeys ??= new();
        config.Ui ??= new();
        config.Effects ??= new();
        NormalizeEffects(config.Effects, fixes);

        config.Profiles.RemoveAll(p => p is null);
        if (config.Profiles.Count == 0)
        {
            config.Profiles.AddRange(DefaultConfig.Create().Profiles);
            fixes.Add("No profiles found; restored the default profiles.");
        }

        var seenIds = new HashSet<Guid>();
        foreach (Profile profile in config.Profiles)
        {
            if (profile.Id == Guid.Empty || !seenIds.Add(profile.Id))
            {
                profile.Id = Guid.NewGuid();
                seenIds.Add(profile.Id);
                fixes.Add($"Gave profile '{profile.Name}' a new ID.");
            }

            string name = (profile.Name ?? "").Trim();
            if (name.Length == 0)
                name = "Profile";
            if (name.Length > MaxNameLength)
                name = name[..MaxNameLength];
            profile.Name = name;

            ColorSettings color = (profile.Color ?? ColorSettings.Neutral).Clamp();
            if (color != profile.Color)
                fixes.Add($"Adjusted out-of-range values in '{profile.Name}'.");
            profile.Color = color;

            profile.MonitorOverrides = new Dictionary<string, ColorSettings>(
                (profile.MonitorOverrides ?? []).Where(o => o.Value is not null)
                    .Select(o => KeyValuePair.Create(o.Key, o.Value.Clamp())),
                StringComparer.OrdinalIgnoreCase);

            if (profile.Effect is not null && profile.Effect != LookLibrary.None && LookLibrary.Find(config.Effects, profile.Effect) is null)
            {
                fixes.Add($"Removed a missing screen effect from '{profile.Name}'.");
                profile.Effect = null;
            }

            if (profile.Hotkey is not null && !HotkeyGesture.TryParse(profile.Hotkey, out _))
            {
                fixes.Add($"Removed invalid hotkey '{profile.Hotkey}' from '{profile.Name}'.");
                profile.Hotkey = null;
            }
        }

        if (config.ActiveProfileId is null || !seenIds.Contains(config.ActiveProfileId.Value))
            config.ActiveProfileId = config.Profiles[0].Id;

        int removedRules = config.AppRules.RemoveAll(r =>
            r is null || string.IsNullOrWhiteSpace(r.Executable) || !seenIds.Contains(r.ProfileId));
        if (removedRules > 0)
            fixes.Add($"Removed {removedRules} app rule(s) pointing at missing profiles.");

        int removedSlots = config.Schedule.Slots.RemoveAll(s => s is null || !seenIds.Contains(s.ProfileId));
        if (removedSlots > 0)
            fixes.Add($"Removed {removedSlots} schedule slot(s) pointing at missing profiles.");
        foreach (ScheduleSlot slot in config.Schedule.Slots)
        {
            slot.FadeMinutes = Math.Clamp(slot.FadeMinutes, 0, 180);
            slot.OffsetMinutes = Math.Clamp(slot.OffsetMinutes, -ScheduleTimeline.MaxSunOffsetMinutes, ScheduleTimeline.MaxSunOffsetMinutes);
            if ((slot.Days & ScheduleDays.Every) == ScheduleDays.None)
                slot.Days = ScheduleDays.Every;
        }
        ScheduleShapes.Normalize(config.Schedule, config.Profiles);
        if (config.Schedule.Location is { } location &&
            (!double.IsFinite(location.Latitude) || !double.IsFinite(location.Longitude) ||
             Math.Abs(location.Latitude) > 90 || Math.Abs(location.Longitude) > 180))
        {
            config.Schedule.Location = null;
            fixes.Add("Removed an invalid schedule location.");
        }

        config.Hotkeys.TogglePause = ValidHotkeyOrNull(config.Hotkeys.TogglePause);
        config.Hotkeys.NextProfile = ValidHotkeyOrNull(config.Hotkeys.NextProfile);
        config.Hotkeys.PreviousProfile = ValidHotkeyOrNull(config.Hotkeys.PreviousProfile);
        config.Hotkeys.ToggleEffects = ValidHotkeyOrNull(config.Hotkeys.ToggleEffects);
        config.Hotkeys.QuickPanel = ValidHotkeyOrNull(config.Hotkeys.QuickPanel);
        config.Ui.OsdDurationMs = Math.Clamp(config.Ui.OsdDurationMs, 500, 10_000);
        if (!Enum.IsDefined(config.Ui.Backdrop))
            config.Ui.Backdrop = WindowBackdrop.Mica;
        config.Ui.TextScale = double.IsFinite(config.Ui.TextScale) ? Math.Clamp(config.Ui.TextScale, 0.9, 1.25) : 1.0;
        config.Ui.Transparency = double.IsFinite(config.Ui.Transparency) ? Math.Clamp(config.Ui.Transparency, 0, 1) : 1;
        // Only the offered colours: an older or hand-edited code becomes the closest one.
        config.Ui.AccentColor = Accents.Nearest(config.Ui.AccentColor)?.Hex;

        return fixes;
    }

    private static void NormalizeEffects(EffectsConfig effects, List<string> fixes)
    {
        effects.Look = (effects.Look ?? Look.Neutral).Clamp();
        effects.MaxFps = Math.Clamp(effects.MaxFps, 0, 240);
        effects.Strength = double.IsFinite(effects.Strength) ? Math.Clamp(effects.Strength, 0, LookParameters.MaxStrength) : 1;
        effects.SavedLooks ??= [];
        effects.SavedLooks.RemoveAll(s => s is null);
        var seen = new HashSet<Guid>();
        foreach (SavedLook saved in effects.SavedLooks)
        {
            if (saved.Id == Guid.Empty || !seen.Add(saved.Id))
            {
                saved.Id = Guid.NewGuid();
                seen.Add(saved.Id);
                fixes.Add($"Gave saved look '{saved.Name}' a new ID.");
            }
            saved.Name = CleanName(saved.Name, "Look");
            saved.Look = (saved.Look ?? Look.Neutral).Clamp();
            if (Looks.Find(saved.BasedOn) is null)
                saved.BasedOn = null;
        }
        if (effects.Preset is not null && LookLibrary.Find(effects, effects.Preset) is null)
            effects.Preset = null;
    }

    /// <summary>Trims a name, drops control characters and limits its length.</summary>
    public static string CleanName(string? name, string fallback)
    {
        string cleaned = new string((name ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (cleaned.Length == 0)
            return fallback;
        return cleaned.Length > MaxNameLength ? cleaned[..MaxNameLength] : cleaned;
    }

    private static string? ValidHotkeyOrNull(string? hotkey) =>
        HotkeyGesture.TryParse(hotkey, out HotkeyGesture gesture) ? gesture.ToString() : null;
}
