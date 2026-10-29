// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DispHub.Platform.Shell;

namespace DispHub.Services;

/// <summary>Program icons for the Apps page, extracted once per program.</summary>
public static class AppIcons
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lazy<ImageSource?> Generic = new(() => ToImage(ProgramInfo.GenericIcon(64)));

    /// <summary>
    /// The icon of the program at <paramref name="path"/>. With only a file name, or a folder it has since moved from,
    /// the program is looked for by name; failing that, Windows' own program icon, so every app has one.
    /// </summary>
    public static ImageSource? For(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (Cache.TryGetValue(path, out ImageSource? cached))
            return cached;
        ImageSource? image = Path.IsPathRooted(path) && File.Exists(path) ? ToImage(ProgramInfo.ExtractIcon(path, 64)) : null;
        if (image is null && ProgramInfo.Find(path) is { } found)
            image = ToImage(ProgramInfo.ExtractIcon(found, 64));
        return Cache[path] = image ?? Generic.Value;
    }

    private static BitmapSource? ToImage(nint icon)
    {
        if (icon == 0)
            return null;
        try
        {
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        finally
        {
            ProgramInfo.DestroyIcon(icon);
        }
    }
}
