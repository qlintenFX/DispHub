using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Core.Schedule;

namespace DispHub.Tests.Core;

public sealed class SunCalculatorTests
{
    private static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
    private static readonly TimeZoneInfo Sydney = TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time");

    [Theory]
    // Published times (timeanddate.com) for Amsterdam and Sydney; NOAA's equations stay within a few minutes.
    [InlineData(2026, 6, 21, "05:18", "22:06")]
    [InlineData(2026, 12, 21, "08:48", "16:29")]
    [InlineData(2026, 3, 20, "06:44", "18:53")]
    public void Amsterdam_MatchesPublishedTimes(int year, int month, int day, string sunrise, string sunset)
    {
        var times = SunCalculator.SunTimes(new DateOnly(year, month, day), 52.37, 4.90, Amsterdam);

        Assert.NotNull(times);
        AssertClose(sunrise, times.Value.Sunrise);
        AssertClose(sunset, times.Value.Sunset);
    }

    [Fact]
    public void SouthernHemisphere_Summer()
    {
        var times = SunCalculator.SunTimes(new DateOnly(2026, 1, 1), -33.87, 151.21, Sydney);

        Assert.NotNull(times);
        AssertClose("05:47", times.Value.Sunrise);
        AssertClose("20:09", times.Value.Sunset);
    }

    [Theory]
    [InlineData(6, 21)] // Midnight sun.
    [InlineData(12, 21)] // Polar night.
    public void InsideTheArcticCircle_NoSunriseOrSunset(int month, int day) =>
        Assert.Null(SunCalculator.SunTimesUtc(new DateOnly(2026, month, day), 69.65, 18.96));

    private static void AssertClose(string expected, DateTime actual)
    {
        TimeOnly want = TimeOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        double minutes = Math.Abs((TimeOnly.FromDateTime(actual) - want).TotalMinutes);
        Assert.True(Math.Min(minutes, 1440 - minutes) <= 4, $"expected about {expected}, got {actual:HH:mm}");
    }
}

public sealed class ScheduleTimelineTests
{
    private static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    // 2026-10-12 is a Monday.
    private static readonly DateOnly Monday = new(2026, 10, 12);

    [Fact]
    public void WeekendSlot_KeepsRunningIntoMondayUntilTheNextSlot()
    {
        var weekday = new ScheduleSlot { Start = new TimeOnly(9, 0), Days = ScheduleDays.Weekdays, ProfileId = Guid.NewGuid() };
        var weekend = new ScheduleSlot { Start = new TimeOnly(11, 0), Days = ScheduleDays.Weekend, ProfileId = Guid.NewGuid() };
        var schedule = new ScheduleConfig { Enabled = true, Slots = [weekday, weekend] };

        SchedulePosition? early = ScheduleTimeline.Locate(schedule, Monday.ToDateTime(new TimeOnly(8, 0)), Amsterdam);
        SchedulePosition? later = ScheduleTimeline.Locate(schedule, Monday.ToDateTime(new TimeOnly(10, 0)), Amsterdam);

        Assert.Same(weekend, early!.Current.Slot);
        Assert.Equal(Monday.AddDays(-1).ToDateTime(new TimeOnly(11, 0)), early.Current.Start);
        Assert.Equal(Monday.ToDateTime(new TimeOnly(9, 0)), early.Next.Start);
        Assert.Same(weekday, later!.Current.Slot);
    }

    [Fact]
    public void SunsetSlot_StartsRelativeToThatDaysSunset()
    {
        var slot = new ScheduleSlot { Anchor = ScheduleAnchor.Sunset, OffsetMinutes = -30, ProfileId = Guid.NewGuid() };
        var location = new GeoLocation { Latitude = 52.37, Longitude = 4.90 };

        DateTime start = ScheduleTimeline.StartOn(slot, Monday, location, Amsterdam);
        DateTime sunset = SunCalculator.SunTimes(Monday, 52.37, 4.90, Amsterdam)!.Value.Sunset;

        Assert.Equal(sunset.AddMinutes(-30), start);
    }

    [Fact]
    public void SunSlot_WithoutLocation_UsesTheFallbackTimes()
    {
        var slot = new ScheduleSlot { Anchor = ScheduleAnchor.Sunrise, OffsetMinutes = 15, ProfileId = Guid.NewGuid() };

        DateTime start = ScheduleTimeline.StartOn(slot, Monday, null, Amsterdam);

        Assert.Equal(Monday.ToDateTime(ScheduleTimeline.FallbackSunrise).AddMinutes(15), start);
    }

    [Fact]
    public void SlotOnNoDay_IsIgnored()
    {
        var slot = new ScheduleSlot { Start = new TimeOnly(9, 0), Days = ScheduleDays.Saturday, ProfileId = Guid.NewGuid() };
        var schedule = new ScheduleConfig { Slots = [slot] };

        Assert.Empty(ScheduleTimeline.ForDay(schedule, Monday, Amsterdam));
        Assert.Single(ScheduleTimeline.ForDay(schedule, Monday.AddDays(5), Amsterdam));
    }

    [Fact]
    public void RuleEngine_FadesIntoAnEveningSlot()
    {
        var day = new Profile { Name = "Day" };
        var night = new Profile { Name = "Night", Color = new DispHub.Core.Color.ColorSettings(TemperatureK: 3600) };
        var config = new AppConfig
        {
            Profiles = [day, night],
            ActiveProfileId = day.Id,
            Schedule = new ScheduleConfig
            {
                Enabled = true,
                Slots =
                [
                    new ScheduleSlot { Start = new TimeOnly(7, 0), ProfileId = day.Id },
                    new ScheduleSlot { Start = new TimeOnly(21, 0), ProfileId = night.Id, FadeMinutes = 30 },
                ],
            },
        };

        RuleOutput output = RuleEngine.Resolve(new RuleInput(
            config, ["m1"], Monday.ToDateTime(new TimeOnly(21, 15)), null, null, IsPro: true, Amsterdam));

        Assert.Equal(TargetReason.Schedule, output.Reason);
        Assert.Equal(night.Id, output.ProfileId);
        // Halfway through the fade; temperature blends in mired, as warmth is perceived.
        Assert.Equal(DispHub.Core.Color.ColorBlend.Lerp(day.Color, night.Color, 0.5), output.Monitors["m1"].Settings);
        Assert.Equal(Monday.ToDateTime(new TimeOnly(21, 15, 2)), output.NextEvaluation);
    }
}
