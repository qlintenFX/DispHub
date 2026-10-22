// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Model;

namespace DispHub.Core.Schedule;

/// <summary>
/// The two simple schedules the Schedule page offers, like Windows' Night light: a night profile from sunset to
/// sunrise, or between two clock times. Both are two slots, night first. Anything else is a custom schedule.
/// </summary>
public static class ScheduleShapes
{
    /// <summary>Which simple mode <paramref name="slots"/> fit, or <see cref="ScheduleMode.Custom"/>.</summary>
    public static ScheduleMode ShapeOf(IReadOnlyList<ScheduleSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (slots.Count != 2 || slots.Any(s => s.Days != ScheduleDays.Every || s.OffsetMinutes != 0))
            return ScheduleMode.Custom;
        if (slots.All(s => s.Anchor == ScheduleAnchor.Time))
            return ScheduleMode.SetHours;
        if (slots.Any(s => s.Anchor == ScheduleAnchor.Sunset) && slots.Any(s => s.Anchor == ScheduleAnchor.Sunrise))
            return ScheduleMode.SunsetToSunrise;
        return ScheduleMode.Custom;
    }

    /// <summary>
    /// Brings a schedule in line with its mode: a simple mode whose slots no longer fit becomes custom (or the
    /// simple mode they do fit), and the night slot goes first: the sunset one, or the warmer profile.
    /// </summary>
    public static void Normalize(ScheduleConfig schedule, IReadOnlyList<Profile> profiles)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(profiles);
        if (!Enum.IsDefined(schedule.Mode))
            schedule.Mode = ScheduleMode.Custom;
        if (schedule.Mode == ScheduleMode.Custom || schedule.Slots.Count == 0)
            return;

        schedule.Mode = ShapeOf(schedule.Slots);
        if (schedule.Mode == ScheduleMode.Custom)
            return;

        ScheduleSlot first = schedule.Slots[0], second = schedule.Slots[1];
        bool swap = schedule.Mode == ScheduleMode.SunsetToSunrise
            ? first.Anchor != ScheduleAnchor.Sunset
            : Kelvin(second) < Kelvin(first);
        if (swap)
            schedule.Slots = [second, first];

        int Kelvin(ScheduleSlot slot) => profiles.FirstOrDefault(p => p.Id == slot.ProfileId)?.Color.TemperatureK ?? 6500;
    }
}
