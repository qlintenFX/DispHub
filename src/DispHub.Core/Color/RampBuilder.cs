// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Color;

/// <summary>Builds gamma ramps from <see cref="ColorSettings"/>. Pure and deterministic.</summary>
public static class RampBuilder
{
    /// <summary>
    /// Order: gamma, contrast around mid-grey, brightness, clamp, then colour temperature last so the
    /// white point is exactly the temperature multipliers. Every channel is non-decreasing.
    /// </summary>
    public static GammaRamp Build(ColorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ColorSettings s = settings.Clamp();
        if (s.HasNeutralRamp)
            return GammaRamp.Identity();

        double inverseGamma = 1.0 / s.Gamma;
        double contrast = 1.0 + (s.Contrast / 100.0);
        double brightness = s.Brightness / 100.0;
        (double r, double g, double b) = Blackbody.Multipliers(s.TemperatureK);
        double[] multipliers = [r, g, b];

        var ramp = new GammaRamp();
        for (int i = 0; i < GammaRamp.Size; i++)
        {
            double y = Math.Pow(i / 255.0, inverseGamma);
            y = 0.5 + ((y - 0.5) * contrast);
            // Positive brightness lifts shadows by up to 25 %; negative dims everything by up to 50 %.
            y = brightness >= 0 ? y + (brightness * 0.5 * (1 - y)) : y * (1 + brightness);
            y = Math.Clamp(y, 0, 1);

            for (int c = 0; c < 3; c++)
                ramp.Channel(c)[i] = ToUShort(y * multipliers[c]);
        }

        MakeMonotonic(ramp);
        return ramp;
    }

    /// <summary>
    /// Returns <paramref name="target"/> if it is within <paramref name="limits"/>, otherwise the closest
    /// blend between identity and target (<c>identity + t * (target - identity)</c>) that is.
    /// </summary>
    public static GammaRamp FitToLimits(GammaRamp target, RampLimits limits)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.Accepts(target))
            return target;

        GammaRamp identity = GammaRamp.Identity();
        GammaRamp best = identity;
        double low = 0;
        double high = 1;
        for (int step = 0; step < 12; step++)
        {
            double t = (low + high) / 2;
            GammaRamp candidate = Blend(identity, target, t);
            if (limits.Accepts(candidate))
            {
                best = candidate;
                low = t;
            }
            else
            {
                high = t;
            }
        }
        return best;
    }

    public static GammaRamp Blend(GammaRamp from, GammaRamp to, double t)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        var result = new GammaRamp();
        for (int c = 0; c < 3; c++)
        {
            ReadOnlySpan<ushort> a = from.Channel(c);
            ReadOnlySpan<ushort> b = to.Channel(c);
            Span<ushort> output = result.Channel(c);
            for (int i = 0; i < GammaRamp.Size; i++)
                output[i] = (ushort)Math.Round(a[i] + (t * (b[i] - a[i])));
        }
        return result;
    }

    private static void MakeMonotonic(GammaRamp ramp)
    {
        for (int c = 0; c < 3; c++)
        {
            Span<ushort> channel = ramp.Channel(c);
            for (int i = 1; i < channel.Length; i++)
            {
                if (channel[i] < channel[i - 1])
                    channel[i] = channel[i - 1];
            }
        }
    }

    private static ushort ToUShort(double value) => (ushort)Math.Round(Math.Clamp(value, 0, 1) * ushort.MaxValue);
}
