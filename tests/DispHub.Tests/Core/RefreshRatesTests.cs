// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Model;

namespace DispHub.Tests.Core;

public sealed class RefreshRatesTests
{
    [Theory]
    [InlineData(new[] { 60, 120, 165 }, RefreshChoice.Highest, 165)]
    [InlineData(new[] { 165, 60, 120 }, RefreshChoice.Lowest, 60)]
    [InlineData(new[] { 48, 59, 60, 165 }, RefreshChoice.Lowest, 60)] // not the 48 Hz video mode
    [InlineData(new[] { 48, 50 }, RefreshChoice.Lowest, 48)]
    [InlineData(new[] { 60, 120 }, RefreshChoice.Unchanged, null)]
    [InlineData(new[] { 60 }, RefreshChoice.Highest, null)]
    public void Pick_FindsTheRateAChoiceMeans(int[] rates, RefreshChoice choice, int? expected) =>
        Assert.Equal(expected, RefreshRates.Pick(rates, choice));
}
