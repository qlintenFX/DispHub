// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;

namespace DispHub.Core.Model;

/// <summary>Where the main window opens, in screen pixels, worked out from the work area of its monitor.</summary>
public static class WindowFit
{
    /// <summary>The most of the work area a window takes the first time, so desktop shows all around it.</summary>
    public const double MaxShare = 0.85;

    /// <summary>
    /// The first time: <paramref name="preferred"/> when it fits in <see cref="MaxShare"/> of the work area, smaller
    /// when not (but no smaller than <paramref name="minimum"/>, unless the screen is), centred on the work area.
    /// </summary>
    public static Rectangle FirstOpen(Rectangle workArea, Size preferred, Size minimum)
    {
        int width = Fit(preferred.Width, workArea.Width, minimum.Width);
        int height = Fit(preferred.Height, workArea.Height, minimum.Height);
        return new Rectangle(
            workArea.X + ((workArea.Width - width) / 2),
            workArea.Y + ((workArea.Height - height) / 2),
            width,
            height);

        static int Fit(int preferred, int available, int minimum) =>
            Math.Min(available, Math.Max(Math.Min(preferred, (int)(available * MaxShare)), minimum));
    }

    /// <summary>
    /// A window put back where it was: no larger than the work area it is on now and fully inside it, as the screen
    /// may have changed since (another resolution, a monitor unplugged, the taskbar moved).
    /// </summary>
    public static Rectangle Restore(Rectangle bounds, Rectangle workArea, Size minimum)
    {
        int width = Math.Min(workArea.Width, Math.Max(bounds.Width, minimum.Width));
        int height = Math.Min(workArea.Height, Math.Max(bounds.Height, minimum.Height));
        int left = Math.Clamp(bounds.X, workArea.Left, workArea.Right - width);
        int top = Math.Clamp(bounds.Y, workArea.Top, workArea.Bottom - height);
        return new Rectangle(left, top, width, height);
    }
}
