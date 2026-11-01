// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Services;

namespace DispHub.Tests.App;

public sealed class ReviewPromptTests
{
    private static readonly DateTimeOffset FirstRun = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldAsk_NotInTheFirstWeek()
    {
        Assert.False(ReviewPrompt.ShouldAsk(FirstRun.AddDays(6), FirstRun, switches: 50, isPro: true, trialEndShown: null));
    }

    [Fact]
    public void ShouldAsk_NotBeforeRealUse()
    {
        Assert.False(ReviewPrompt.ShouldAsk(FirstRun.AddDays(30), FirstRun, switches: 3, isPro: true, trialEndShown: null));
    }

    [Fact]
    public void ShouldAsk_ProAfterAWeek()
    {
        Assert.True(ReviewPrompt.ShouldAsk(FirstRun.AddDays(8), FirstRun, switches: 10, isPro: true, trialEndShown: null));
    }

    [Fact]
    public void ShouldAsk_NotWithTheTrialEndedMessage()
    {
        // The trial ended a week in; the message was just shown on this window activation.
        DateTimeOffset now = FirstRun.AddDays(7).AddHours(2);
        Assert.False(ReviewPrompt.ShouldAsk(now, FirstRun, switches: 20, isPro: false, trialEndShown: now));
        Assert.False(ReviewPrompt.ShouldAsk(now, FirstRun, switches: 20, isPro: false, trialEndShown: null));
    }

    [Fact]
    public void ShouldAsk_FreeUsersAFewDaysAfterTheTrialEnded()
    {
        DateTimeOffset shown = FirstRun.AddDays(7);
        Assert.False(ReviewPrompt.ShouldAsk(shown.AddDays(2), FirstRun, switches: 20, isPro: false, trialEndShown: shown));
        Assert.True(ReviewPrompt.ShouldAsk(shown.AddDays(3), FirstRun, switches: 20, isPro: false, trialEndShown: shown));
    }
}
