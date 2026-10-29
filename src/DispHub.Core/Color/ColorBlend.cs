// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Color;

/// <summary>Interpolates between two settings, used for schedule fades.</summary>
public static class ColorBlend
{
    public static ColorSettings Lerp(ColorSettings from, ColorSettings to, double t)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        t = Math.Clamp(t, 0, 1);
        if (t <= 0)
            return from;
        if (t >= 1)
            return to;

        // Temperature is interpolated in mireds so the fade looks even to the eye.
        double fromMired = 1e6 / from.TemperatureK;
        double toMired = 1e6 / to.TemperatureK;
        int kelvin = (int)Math.Round(1e6 / (fromMired + ((toMired - fromMired) * t)));

        return new ColorSettings(
            (int)Math.Round(from.Brightness + ((to.Brightness - from.Brightness) * t)),
            (int)Math.Round(from.Contrast + ((to.Contrast - from.Contrast) * t)),
            Math.Round(from.Gamma + ((to.Gamma - from.Gamma) * t), 3),
            kelvin,
            (int)Math.Round(from.Vibrance + ((to.Vibrance - from.Vibrance) * t)),
            (int)Math.Round(from.ShadowBoost + ((to.ShadowBoost - from.ShadowBoost) * t)));
    }
}
