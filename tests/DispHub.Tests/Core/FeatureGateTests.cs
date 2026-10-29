// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Licensing;

namespace DispHub.Tests.Core;

public sealed class FeatureGateTests
{
    [Fact]
    public void Trial_UnlocksEverythingPro()
    {
        var trial = new FeatureGate(IsPro: false, InTrial: true);
        Assert.True(trial.HasPro);
        Assert.True(trial.Schedules);
        Assert.True(trial.PerMonitorProfiles);
        Assert.Equal(FeatureGate.ProfileCeiling, trial.MaxProfiles);
        Assert.True(trial.CanAddAppRule(5));
    }

    [Fact]
    public void Free_KeepsThreeProfilesAndOneAppRule()
    {
        var free = new FeatureGate(IsPro: false);
        Assert.False(free.HasPro);
        Assert.True(free.CanUseProfile(2));
        Assert.False(free.CanUseProfile(3));
        Assert.True(free.CanAddAppRule(0));
        Assert.False(free.CanAddAppRule(1));
        Assert.True(free.CanUseAppRule(0));
        Assert.False(free.CanUseAppRule(1));
        Assert.False(free.Schedules);
    }

    [Fact]
    public void Pro_StopsAtTheCeiling()
    {
        var pro = new FeatureGate(IsPro: true);
        Assert.True(pro.CanAddProfile(FeatureGate.ProfileCeiling - 1));
        Assert.False(pro.CanAddProfile(FeatureGate.ProfileCeiling));
    }

    [Fact]
    public void TrialDaysLeft_CountsAPartDayAsOne()
    {
        var start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset end = FeatureGate.TrialEnds(start);
        Assert.Equal(start.AddDays(7), end);
        Assert.Equal(7, FeatureGate.TrialDaysLeft(end, start));
        Assert.Equal(1, FeatureGate.TrialDaysLeft(end, end.AddHours(-2)));
        Assert.Equal(0, FeatureGate.TrialDaysLeft(end, end));
        Assert.Equal(0, FeatureGate.TrialDaysLeft(end, end.AddDays(3)));
    }
}
