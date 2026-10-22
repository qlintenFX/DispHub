// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Core.Config;
using DispHub.Core.Model;
using DispHub.Core.Schedule;

namespace DispHub.Tests.Core;

public sealed class ScheduleShapesTests
{
    private readonly Profile _day = new() { Name = "Day", Color = ColorSettings.Neutral };
    private readonly Profile _night = new() { Name = "Night", Color = new ColorSettings(TemperatureK: 3400) };

    [Fact]
    public void SunsetAndSunrise_IsTheSunMode_WithNightFirst()
    {
        var schedule = new ScheduleConfig
        {
            Slots =
            [
                new ScheduleSlot { Anchor = ScheduleAnchor.Sunrise, ProfileId = _day.Id },
                new ScheduleSlot { Anchor = ScheduleAnchor.Sunset, ProfileId = _night.Id },
            ],
        };

        ScheduleShapes.Normalize(schedule, [_day, _night]);

        Assert.Equal(ScheduleMode.SunsetToSunrise, schedule.Mode);
        Assert.Equal(ScheduleAnchor.Sunset, schedule.Slots[0].Anchor);
    }

    [Fact]
    public void TwoTimes_IsSetHours_WithTheWarmerProfileFirst()
    {
        // An older config: no mode saved, which reads as the default (sunset to sunrise).
        var schedule = new ScheduleConfig
        {
            Slots =
            [
                new ScheduleSlot { Start = new TimeOnly(7, 0), ProfileId = _day.Id },
                new ScheduleSlot { Start = new TimeOnly(21, 0), ProfileId = _night.Id },
            ],
        };

        ScheduleShapes.Normalize(schedule, [_day, _night]);

        Assert.Equal(ScheduleMode.SetHours, schedule.Mode);
        Assert.Equal(_night.Id, schedule.Slots[0].ProfileId);
        Assert.Equal(new TimeOnly(21, 0), schedule.Slots[0].Start);
    }

    [Theory]
    [InlineData(3)] // more than two changes
    [InlineData(-1)] // a change on some days only
    public void AnythingElse_IsCustom(int variant)
    {
        var schedule = new ScheduleConfig { Mode = ScheduleMode.SetHours };
        schedule.Slots.Add(new ScheduleSlot { Start = new TimeOnly(7, 0), ProfileId = _day.Id });
        schedule.Slots.Add(new ScheduleSlot { Start = new TimeOnly(21, 0), ProfileId = _night.Id, Days = variant < 0 ? ScheduleDays.Weekdays : ScheduleDays.Every });
        if (variant == 3)
            schedule.Slots.Add(new ScheduleSlot { Start = new TimeOnly(23, 0), ProfileId = _night.Id });

        ScheduleShapes.Normalize(schedule, [_day, _night]);

        Assert.Equal(ScheduleMode.Custom, schedule.Mode);
    }

    [Fact]
    public void Custom_StaysCustom_EvenWhenItLooksSimple()
    {
        var schedule = new ScheduleConfig
        {
            Mode = ScheduleMode.Custom,
            Slots = [new ScheduleSlot { Start = new TimeOnly(7, 0), ProfileId = _day.Id }, new ScheduleSlot { Start = new TimeOnly(21, 0), ProfileId = _night.Id }],
        };

        ScheduleShapes.Normalize(schedule, [_day, _night]);

        Assert.Equal(ScheduleMode.Custom, schedule.Mode);
        Assert.Equal(_day.Id, schedule.Slots[0].ProfileId);
    }

    [Fact]
    public void Normalizer_AppliesTheShapes()
    {
        AppConfig config = DefaultConfig.Create();
        config.Schedule.Mode = (ScheduleMode)42;

        ConfigNormalizer.Normalize(config);

        Assert.Equal(ScheduleMode.Custom, config.Schedule.Mode);
    }
}
