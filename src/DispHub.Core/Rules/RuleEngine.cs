using System.Globalization;
using DispHub.Core.Color;
using DispHub.Core.Model;
using DispHub.Core.Schedule;

namespace DispHub.Core.Rules;

/// <summary>A profile the user picked by hand while a rule was active. Ends when that rule's context ends.</summary>
public sealed record ManualOverride(Guid ProfileId, string ContextKey);

public sealed record RuleInput(
    AppConfig Config,
    IReadOnlyList<string> Monitors,
    DateTime LocalNow,
    string? ForegroundExecutable,
    ManualOverride? Manual,
    bool IsPro,
    TimeZoneInfo? TimeZone = null);

public enum TargetReason
{
    Paused,
    Excluded,
    Manual,
    AppRule,
    Schedule,
    Active,
}

/// <summary>What one monitor should show. Null settings mean "restore the original colours".</summary>
public sealed record MonitorTarget(ColorSettings? Settings, Guid? ProfileId, TargetReason Reason);

public sealed record RuleOutput(
    IReadOnlyDictionary<string, MonitorTarget> Monitors,
    string ContextKey,
    Guid? ProfileId,
    TargetReason Reason,
    DateTime? NextEvaluation);

/// <summary>
/// Decides which colours each monitor gets. Pure: same input, same output. Precedence:
/// paused, excluded monitor, manual pick (within its context), app rule, schedule, active profile.
/// </summary>
public static class RuleEngine
{
    public const string BaseContext = "base";

    private const string AppContextPrefix = "app:";

    /// <summary>True for the context of an app rule (the app in front decides), as opposed to the schedule or none.</summary>
    public static bool IsAppContext(string contextKey) =>
        contextKey?.StartsWith(AppContextPrefix, StringComparison.Ordinal) ?? false;

    public static RuleOutput Resolve(RuleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        AppConfig config = input.Config;
        var monitors = new Dictionary<string, MonitorTarget>(StringComparer.OrdinalIgnoreCase);

        if (config.IsPaused || config.IsOff)
        {
            foreach (string monitor in input.Monitors)
                monitors[monitor] = new MonitorTarget(null, null, TargetReason.Paused);
            return new RuleOutput(monitors, BaseContext, null, TargetReason.Paused, null);
        }

        AppRule? appRule = input.IsPro ? FindAppRule(config, input.ForegroundExecutable) : null;
        ScheduleState? schedule = appRule is null && input.IsPro ? FindScheduleSlot(config, input.LocalNow, input.TimeZone ?? TimeZoneInfo.Local) : null;

        string context = appRule is not null ? AppContextPrefix + appRule.Id
            : schedule is not null ? schedule.ContextKey
            : BaseContext;

        Profile active = FindProfile(config, config.ActiveProfileId) ?? config.Profiles[0];
        Profile? manual = input.Manual is not null && input.Manual.ContextKey == context
            ? FindProfile(config, input.Manual.ProfileId)
            : null;

        (Profile profile, TargetReason reason) = manual is not null ? (manual, TargetReason.Manual)
            : appRule is not null && FindProfile(config, appRule.ProfileId) is { } ruleProfile ? (ruleProfile, TargetReason.AppRule)
            : schedule is not null ? (schedule.Profile, TargetReason.Schedule)
            : (active, TargetReason.Active);

        foreach (string monitor in input.Monitors)
        {
            if (config.Monitors.TryGetValue(monitor, out MonitorConfig? monitorConfig) && monitorConfig.Excluded)
            {
                monitors[monitor] = new MonitorTarget(null, null, TargetReason.Excluded);
                continue;
            }

            ColorSettings settings = profile.SettingsFor(monitor, input.IsPro);
            if (reason == TargetReason.Schedule && schedule!.FadeFrom is { } from && schedule.FadeProgress < 1)
                settings = ColorBlend.Lerp(from.SettingsFor(monitor, input.IsPro), settings, schedule.FadeProgress);

            monitors[monitor] = new MonitorTarget(settings, profile.Id, reason);
        }

        DateTime? next = schedule?.NextEvaluation;
        return new RuleOutput(monitors, context, profile.Id, reason, next);
    }

    public static Profile? FindProfile(AppConfig config, Guid? id)
    {
        ArgumentNullException.ThrowIfNull(config);
        return id is null ? null : config.Profiles.Find(p => p.Id == id.Value);
    }

    /// <summary>True when <paramref name="rule"/> names <paramref name="executablePath"/> by file name or full path.</summary>
    public static bool Matches(AppRule rule, string? executablePath)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (string.IsNullOrEmpty(executablePath) || string.IsNullOrWhiteSpace(rule.Executable))
            return false;

        string wanted = rule.Executable.Trim();
        if (HasFolder(wanted) && HasFolder(executablePath))
            return string.Equals(Path.GetFullPath(wanted), Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase);

        // A rule by name, or a program whose folder Windows wouldn't tell us: compare the file names.
        return string.Equals(Path.GetFileName(wanted), Path.GetFileName(executablePath), StringComparison.OrdinalIgnoreCase);

        static bool HasFolder(string path) => path.Contains('\\', StringComparison.Ordinal) || path.Contains('/', StringComparison.Ordinal);
    }

    private static AppRule? FindAppRule(AppConfig config, string? executable) =>
        executable is null ? null : config.AppRules.Find(r => r.Enabled && Matches(r, executable));

    private sealed record ScheduleState(
        Profile Profile,
        Profile? FadeFrom,
        double FadeProgress,
        string ContextKey,
        DateTime NextEvaluation);

    private static ScheduleState? FindScheduleSlot(AppConfig config, DateTime now, TimeZoneInfo zone)
    {
        if (!config.Schedule.Enabled ||
            ScheduleTimeline.Locate(config.Schedule, now, zone, id => FindProfile(config, id) is not null) is not { } position)
        {
            return null;
        }

        ScheduleOccurrence current = position.Current;
        Profile profile = FindProfile(config, current.Slot.ProfileId)!;
        Profile? previous = position.Previous is { } before ? FindProfile(config, before.Slot.ProfileId) : null;

        double progress = 1;
        DateTime next = position.Next.Start;
        if (current.Slot.FadeMinutes > 0 && previous is not null && previous.Id != profile.Id)
        {
            progress = (now - current.Start).TotalMinutes / current.Slot.FadeMinutes;
            if (progress < 1 && now.AddSeconds(2) < next)
                next = now.AddSeconds(2);
        }

        string key = string.Create(CultureInfo.InvariantCulture, $"sched:{current.Slot.Id}:{current.Start:yyyyMMddHHmm}");
        return new ScheduleState(profile, previous, Math.Clamp(progress, 0, 1), key, next);
    }
}
