// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Color;

/// <summary>A driver's vibrance range. <see cref="Default"/> is the driver's neutral level.</summary>
public readonly record struct VibranceRange(int Min, int Max, int Default)
{
    public bool IsValid => Max > Min && Default >= Min && Default <= Max;
}

/// <summary>
/// Maps DispHub's 0-100 vibrance (50 = driver default) to a driver range, piecewise around the default:
/// 0..50 covers Min..Default and 50..100 covers Default..Max.
/// </summary>
public static class VibranceMapping
{
    public static int ToDriverLevel(int percent, VibranceRange range)
    {
        int p = Math.Clamp(percent, ColorSettings.VibranceMin, ColorSettings.VibranceMax);
        const int Mid = ColorSettings.NeutralVibrance;
        double level = p <= Mid
            ? range.Min + ((range.Default - range.Min) * (p / (double)Mid))
            : range.Default + ((range.Max - range.Default) * ((p - Mid) / (double)(ColorSettings.VibranceMax - Mid)));
        return (int)Math.Round(level);
    }

    public static int ToPercent(int level, VibranceRange range)
    {
        int l = Math.Clamp(level, range.Min, range.Max);
        const int Mid = ColorSettings.NeutralVibrance;
        if (l <= range.Default)
        {
            return range.Default == range.Min
                ? Mid
                : (int)Math.Round(Mid * (l - range.Min) / (double)(range.Default - range.Min));
        }
        return Mid + (int)Math.Round((ColorSettings.VibranceMax - Mid) * (l - range.Default) / (double)(range.Max - range.Default));
    }
}
