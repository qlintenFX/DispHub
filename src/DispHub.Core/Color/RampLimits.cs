// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Color;

/// <summary>
/// The rules Windows applies before it accepts a gamma ramp. By default each entry may differ from the identity
/// ramp by at most 128 of 256 steps (measured on Windows 11 25H2: anything further makes SetDeviceGammaRamp fail).
/// The <c>GdiIcmGammaRange</c> registry value (256) widens that band to the full range.
/// </summary>
public sealed record RampLimits(int MaxDeviation)
{
    /// <summary>Windows' default band, minus one step of margin.</summary>
    public static RampLimits Default { get; } = new(MaxDeviation: 127);

    /// <summary>When GdiIcmGammaRange is 256 any monotonic ramp is accepted.</summary>
    public static RampLimits Extended { get; } = new(MaxDeviation: 256);

    public static RampLimits ForGammaRange(int? gdiIcmGammaRange) =>
        gdiIcmGammaRange is >= 256 ? Extended : Default;

    public bool Accepts(GammaRamp ramp)
    {
        ArgumentNullException.ThrowIfNull(ramp);
        for (int c = 0; c < 3; c++)
        {
            ReadOnlySpan<ushort> channel = ramp.Channel(c);
            for (int i = 0; i < channel.Length; i++)
            {
                if (i > 0 && channel[i] < channel[i - 1])
                    return false;
                if (Math.Abs((channel[i] >> 8) - i) > MaxDeviation)
                    return false;
            }
        }
        return true;
    }
}
