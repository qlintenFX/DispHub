// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

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

    public static ImageSource? For(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (Cache.TryGetValue(path, out ImageSource? cached))
            return cached;
        ImageSource? image = null;
        nint icon = ProgramInfo.ExtractIcon(path, 64);
        if (icon != 0)
        {
            try
            {
                BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                image = bitmap;
            }
            finally
            {
                ProgramInfo.DestroyIcon(icon);
            }
        }
        return Cache[path] = image;
    }
}
