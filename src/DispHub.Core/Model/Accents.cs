// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

namespace DispHub.Core.Model;

/// <summary>One accent colour the user can pick instead of following Windows.</summary>
public sealed record Accent(string Key, string Hex);

/// <summary>The accent colours DispHub offers, all taken from Windows' own palette.</summary>
public static class Accents
{
    public static IReadOnlyList<Accent> All { get; } =
    [
        new("blue", "#0078D4"),
        new("purple", "#8764B8"),
        new("pink", "#E3008C"),
        new("red", "#E81123"),
        new("orange", "#F7630C"),
        new("yellow", "#FFB900"),
        new("green", "#10893E"),
        new("teal", "#038387"),
        new("graphite", "#69797E"),
    ];

    /// <summary>The offered colour closest to <paramref name="hex"/>, or null for none or an invalid code.</summary>
    public static Accent? Nearest(string? hex)
    {
        if (!TryParse(hex, out (int R, int G, int B) wanted))
            return null;
        return All.MinBy(a =>
        {
            _ = TryParse(a.Hex, out (int R, int G, int B) c);
            return Square(c.R - wanted.R) + Square(c.G - wanted.G) + Square(c.B - wanted.B);
        });

        static int Square(int x) => x * x;
    }

    /// <summary>
    /// The three shades controls use: lighter ones on a dark theme, darker ones on a light theme. Like the shades
    /// Windows makes from its accent, they keep the colour's saturation and only change its lightness.
    /// </summary>
    public static (string Primary, string Secondary, string Tertiary) Shades(string hex, bool dark)
    {
        if (!TryParse(hex, out (int R, int G, int B) rgb))
            throw new ArgumentException($"Not a #RRGGBB colour: {hex}", nameof(hex));
        (double h, double s, double l) = ToHsl(rgb.R / 255.0, rgb.G / 255.0, rgb.B / 255.0);
        string Shade(double lightness) => ToHex(h, s, Math.Clamp(lightness, 0, 1));
        return dark
            ? (Shade(l + ((1 - l) * 0.12)), Shade(l + ((1 - l) * 0.47)), Shade(l + ((1 - l) * 0.65)))
            : (Shade(l * 0.86), Shade(l * 0.69), Shade(l * 0.48));
    }

    private static (double H, double S, double L) ToHsl(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, d = max - min;
        if (d == 0)
            return (0, 0, l);
        double s = d / (1 - Math.Abs((2 * l) - 1));
        double h = max == r ? ((g - b) / d % 6) : max == g ? ((b - r) / d) + 2 : ((r - g) / d) + 4;
        return (((h * 60) + 360) % 360, s, l);
    }

    private static string ToHex(double h, double s, double l)
    {
        double c = (1 - Math.Abs((2 * l) - 1)) * s, x = c * (1 - Math.Abs((h / 60 % 2) - 1)), m = l - (c / 2);
        (double r, double g, double b) = (int)(h / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        static int Byte(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return string.Create(CultureInfo.InvariantCulture, $"#{Byte(r + m):X2}{Byte(g + m):X2}{Byte(b + m):X2}");
    }

    private static bool TryParse(string? hex, out (int R, int G, int B) rgb)
    {
        rgb = default;
        if (hex is not { Length: 7 } || hex[0] != '#' || !int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
            return false;
        rgb = ((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
        return true;
    }
}
