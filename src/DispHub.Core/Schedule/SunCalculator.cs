// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Schedule;

/// <summary>
/// Sunrise and sunset from NOAA's solar equations (gml.noaa.gov/grad/solcalc/solareqns.PDF), accurate to about a
/// minute away from the poles. Uses the standard 90.833° zenith, which allows for refraction and the sun's size.
/// </summary>
public static class SunCalculator
{
    private const double Zenith = 90.833;

    /// <summary>
    /// Sunrise and sunset in UTC for <paramref name="date"/> at a location (degrees, north and east positive).
    /// Null when the sun doesn't rise or doesn't set that day (polar night or midnight sun).
    /// </summary>
    public static (DateTime Sunrise, DateTime Sunset)? SunTimesUtc(DateOnly date, double latitude, double longitude)
    {
        double gamma = 2 * Math.PI / (DateTime.IsLeapYear(date.Year) ? 366 : 365) * (date.DayOfYear - 1);
        double equationOfTime = 229.18 * (0.000075 + (0.001868 * Math.Cos(gamma)) - (0.032077 * Math.Sin(gamma))
            - (0.014615 * Math.Cos(2 * gamma)) - (0.040849 * Math.Sin(2 * gamma)));
        double declination = 0.006918 - (0.399912 * Math.Cos(gamma)) + (0.070257 * Math.Sin(gamma))
            - (0.006758 * Math.Cos(2 * gamma)) + (0.000907 * Math.Sin(2 * gamma))
            - (0.002697 * Math.Cos(3 * gamma)) + (0.00148 * Math.Sin(3 * gamma));

        double lat = latitude * Math.PI / 180;
        double cosHourAngle = (Math.Cos(Zenith * Math.PI / 180) / (Math.Cos(lat) * Math.Cos(declination)))
            - (Math.Tan(lat) * Math.Tan(declination));
        if (cosHourAngle is < -1 or > 1)
            return null;
        double hourAngle = Math.Acos(cosHourAngle) * 180 / Math.PI;

        DateTime midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        double sunrise = 720 - (4 * (longitude + hourAngle)) - equationOfTime;
        double sunset = 720 - (4 * (longitude - hourAngle)) - equationOfTime;
        return (midnight.AddMinutes(sunrise), midnight.AddMinutes(sunset));
    }

    /// <summary>Sunrise and sunset in the given time zone's local time.</summary>
    public static (DateTime Sunrise, DateTime Sunset)? SunTimes(DateOnly date, double latitude, double longitude, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return LocalEvent(date, latitude, longitude, zone, sunrise: true) is { } rise &&
               LocalEvent(date, latitude, longitude, zone, sunrise: false) is { } set
            ? (rise, set)
            : null;
    }

    // Far from the time zone's meridian (near the date line) the event can belong to the neighbouring UTC day.
    private static DateTime? LocalEvent(DateOnly date, double latitude, double longitude, TimeZoneInfo zone, bool sunrise)
    {
        foreach (int shift in (int[])[0, -1, 1])
        {
            if (SunTimesUtc(date.AddDays(shift), latitude, longitude) is not { } utc)
                continue;
            DateTime local = TimeZoneInfo.ConvertTimeFromUtc(sunrise ? utc.Sunrise : utc.Sunset, zone);
            if (DateOnly.FromDateTime(local) == date)
                return local;
        }
        return null;
    }
}
