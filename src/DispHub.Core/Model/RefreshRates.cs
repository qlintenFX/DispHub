// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Model;

public static class RefreshRates
{
    /// <summary>The usual floor: below it (48 Hz video modes, for one) the desktop looks choppy.</summary>
    public const int ComfortableMinimum = 60;

    /// <summary>
    /// The rate <paramref name="choice"/> means on a display offering <paramref name="rates"/>, or null when there is
    /// nothing to switch. Lowest stays at 60 Hz or above when the display offers that, like laptop makers' battery modes.
    /// </summary>
    public static int? Pick(IReadOnlyCollection<int> rates, RefreshChoice choice)
    {
        ArgumentNullException.ThrowIfNull(rates);
        if (rates.Count < 2)
            return null;
        return choice switch
        {
            RefreshChoice.Highest => rates.Max(),
            RefreshChoice.Lowest => rates.Where(r => r >= ComfortableMinimum).DefaultIfEmpty(rates.Min()).Min(),
            _ => null,
        };
    }
}
