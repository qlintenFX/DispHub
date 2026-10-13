using DispHub.Core.Model;

namespace DispHub.Core.Schedule;

/// <summary>A slot on a particular day, with its actual start time.</summary>
public sealed record ScheduleOccurrence(ScheduleSlot Slot, DateTime Start);

/// <summary>Where "now" falls in the schedule: the slot that is running, the one before it and the next one.</summary>
public sealed record SchedulePosition(ScheduleOccurrence Current, ScheduleOccurrence? Previous, ScheduleOccurrence Next);

/// <summary>
/// Turns schedule slots into concrete start times: per weekday, at a clock time or relative to sunrise or sunset.
/// The rule engine and the schedule's day view both use this, so what's drawn is what happens.
/// </summary>
public static class ScheduleTimeline
{
    public const int MaxSunOffsetMinutes = 240;

    /// <summary>Used without a location, and on days the sun doesn't rise or set (polar night, midnight sun).</summary>
    public static readonly TimeOnly FallbackSunrise = new(7, 0);
    public static readonly TimeOnly FallbackSunset = new(19, 0);

    public static ScheduleDays DayFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => ScheduleDays.Monday,
        DayOfWeek.Tuesday => ScheduleDays.Tuesday,
        DayOfWeek.Wednesday => ScheduleDays.Wednesday,
        DayOfWeek.Thursday => ScheduleDays.Thursday,
        DayOfWeek.Friday => ScheduleDays.Friday,
        DayOfWeek.Saturday => ScheduleDays.Saturday,
        _ => ScheduleDays.Sunday,
    };

    public static (DateTime Sunrise, DateTime Sunset) SunOn(DateOnly day, GeoLocation? location, TimeZoneInfo zone) =>
        location is not null && SunCalculator.SunTimes(day, location.Latitude, location.Longitude, zone) is { } times
            ? times
            : (day.ToDateTime(FallbackSunrise), day.ToDateTime(FallbackSunset));

    /// <summary>When <paramref name="slot"/> starts on <paramref name="day"/>, kept within that day.</summary>
    public static DateTime StartOn(ScheduleSlot slot, DateOnly day, GeoLocation? location, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (slot.Anchor == ScheduleAnchor.Time)
            return day.ToDateTime(slot.Start);
        (DateTime sunrise, DateTime sunset) = SunOn(day, location, zone);
        DateTime start = (slot.Anchor == ScheduleAnchor.Sunrise ? sunrise : sunset).AddMinutes(slot.OffsetMinutes);
        DateTime first = day.ToDateTime(TimeOnly.MinValue);
        DateTime last = day.ToDateTime(new TimeOnly(23, 59));
        return start < first ? first : start > last ? last : start;
    }

    /// <summary>The slots that run on <paramref name="day"/>, in order of their start.</summary>
    public static IReadOnlyList<ScheduleOccurrence> ForDay(ScheduleConfig schedule, DateOnly day, TimeZoneInfo zone, Func<Guid, bool>? isValid = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ScheduleDays flag = DayFlag(day.DayOfWeek);
        return schedule.Slots
            .Where(s => (s.Days & flag) != 0 && (isValid?.Invoke(s.ProfileId) ?? true))
            .Select(s => new ScheduleOccurrence(s, StartOn(s, day, schedule.Location, zone)))
            .OrderBy(o => o.Start)
            .ToList();
    }

    /// <summary>Finds the running slot. Null when the schedule has no usable slots.</summary>
    public static SchedulePosition? Locate(ScheduleConfig schedule, DateTime now, TimeZoneInfo zone, Func<Guid, bool>? isValid = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        DateOnly today = DateOnly.FromDateTime(now);
        // A week back is always enough to find the running slot, and a week ahead to find the next one.
        var occurrences = new List<ScheduleOccurrence>();
        for (int offset = -7; offset <= 7; offset++)
            occurrences.AddRange(ForDay(schedule, today.AddDays(offset), zone, isValid));

        int index = occurrences.FindLastIndex(o => o.Start <= now);
        if (index < 0)
            return null;
        ScheduleOccurrence current = occurrences[index];
        ScheduleOccurrence? previous = index > 0 ? occurrences[index - 1] : null;
        ScheduleOccurrence next = index + 1 < occurrences.Count ? occurrences[index + 1] : current with { Start = current.Start.AddDays(7) };
        return new SchedulePosition(current, previous, next);
    }
}
