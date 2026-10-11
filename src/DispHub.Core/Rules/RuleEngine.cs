using System.Globalization;
using DispHub.Core.Color;
using DispHub.Core.Model;

namespace DispHub.Core.Rules;

/// <summary>A profile the user picked by hand while a rule was active. Ends when that rule's context ends.</summary>
public sealed record ManualOverride(Guid ProfileId, string ContextKey);

public sealed record RuleInput(
    AppConfig Config,
    IReadOnlyList<string> Monitors,
    DateTime LocalNow,
    string? ForegroundExecutable,
    ManualOverride? Manual,
    bool IsPro);

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

    public static RuleOutput Resolve(RuleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        AppConfig config = input.Config;
        var monitors = new Dictionary<string, MonitorTarget>(StringComparer.OrdinalIgnoreCase);

        if (config.IsPaused)
        {
            foreach (string monitor in input.Monitors)
                monitors[monitor] = new MonitorTarget(null, null, TargetReason.Paused);
            return new RuleOutput(monitors, BaseContext, null, TargetReason.Paused, null);
        }

        AppRule? appRule = input.IsPro ? FindAppRule(config, input.ForegroundExecutable) : null;
        ScheduleState? schedule = appRule is null && input.IsPro ? FindScheduleSlot(config, input.LocalNow) : null;

        string context = appRule is not null ? $"app:{appRule.Id}"
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
        return wanted.Contains('\\', StringComparison.Ordinal) || wanted.Contains('/', StringComparison.Ordinal)
            ? string.Equals(Path.GetFullPath(wanted), Path.GetFullPath(executablePath), StringComparison.OrdinalIgnoreCase)
            : string.Equals(wanted, Path.GetFileName(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    private static AppRule? FindAppRule(AppConfig config, string? executable) =>
        executable is null ? null : config.AppRules.Find(r => r.Enabled && Matches(r, executable));

    private sealed record ScheduleState(
        Profile Profile,
        Profile? FadeFrom,
        double FadeProgress,
        string ContextKey,
        DateTime NextEvaluation);

    private static ScheduleState? FindScheduleSlot(AppConfig config, DateTime now)
    {
        if (!config.Schedule.Enabled)
            return null;

        List<ScheduleSlot> slots = config.Schedule.Slots
            .Where(s => FindProfile(config, s.ProfileId) is not null)
            .OrderBy(s => s.Start)
            .ToList();
        if (slots.Count == 0)
            return null;

        TimeOnly time = TimeOnly.FromDateTime(now);
        int index = slots.FindLastIndex(s => s.Start <= time);
        DateTime startDay = now.Date;
        if (index < 0)
        {
            // Before the first slot of the day: yesterday's last slot is still running.
            index = slots.Count - 1;
            startDay = startDay.AddDays(-1);
        }

        ScheduleSlot slot = slots[index];
        DateTime slotStart = startDay + slot.Start.ToTimeSpan();
        ScheduleSlot nextSlot = slots[(index + 1) % slots.Count];
        DateTime nextStart = (index + 1 < slots.Count ? startDay : startDay.AddDays(1)) + nextSlot.Start.ToTimeSpan();
        if (nextStart <= now)
            nextStart = nextStart.AddDays(1);

        Profile profile = FindProfile(config, slot.ProfileId)!;
        Profile? previous = slots.Count > 1 ? FindProfile(config, slots[(index - 1 + slots.Count) % slots.Count].ProfileId) : null;

        double progress = 1;
        DateTime next = nextStart;
        if (slot.FadeMinutes > 0 && previous is not null && previous.Id != profile.Id)
        {
            progress = (now - slotStart).TotalMinutes / slot.FadeMinutes;
            if (progress < 1)
                next = now.AddSeconds(2) < nextStart ? now.AddSeconds(2) : nextStart;
        }

        string key = string.Create(CultureInfo.InvariantCulture, $"sched:{slot.Id}:{slotStart:yyyyMMdd}");
        return new ScheduleState(profile, previous, Math.Clamp(progress, 0, 1), key, next);
    }
}
