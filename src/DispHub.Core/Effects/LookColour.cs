// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Effects;

/// <summary>Colours used by looks: hues for split toning.</summary>
public static class LookColour
{
    /// <summary>A hue in degrees at full saturation and brightness, as RGB from 0 to 1.</summary>
    public static (double R, double G, double B) Hue(double degrees)
    {
        double h = (((degrees % 360) + 360) % 360) / 60;
        double x = 1 - Math.Abs((h % 2) - 1);
        return (int)h switch
        {
            0 => (1, x, 0),
            1 => (x, 1, 0),
            2 => (0, 1, x),
            3 => (0, x, 1),
            4 => (x, 0, 1),
            _ => (1, 0, x),
        };
    }

    /// <summary>
    /// What a split-tone tint multiplies a colour by: (1, 1, 1) at amount 0, moving towards the hue as the amount
    /// grows. Brightness stays the same, so a blue tint doesn't darken the picture.
    /// </summary>
    public static (double R, double G, double B) Tint(double hue, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        (double r, double g, double b) = Hue(hue);
        r = 1 + ((r - 1) * amount);
        g = 1 + ((g - 1) * amount);
        b = 1 + ((b - 1) * amount);
        double luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
        return (Math.Min(r / luma, 3), Math.Min(g / luma, 3), Math.Min(b / luma, 3));
    }
}
