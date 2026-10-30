// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows.Media;
using System.Windows.Media.Imaging;
using DispHub.Core.Color;

namespace DispHub.Controls;

/// <summary>
/// Shows a photo strip through a profile's actual colour maths (vibrance, then the gamma ramp), the same way the
/// display will change it, so each profile shows what it does: warmer, brighter, punchier or dimmer.
/// </summary>
/// <remarks>The photos are public domain; see THIRD_PARTY_NOTICES.md.</remarks>
public static class ColorPreview
{
    public const string DefaultPicture = "reef";

    /// <summary>The pictures to choose from, as their file names in Assets/Previews.</summary>
    public static readonly IReadOnlyList<string> Pictures = ["reef", "flowers", "autumn", "spring"];

    private const int MaxCached = 32;
    private static readonly Dictionary<(string, ColorSettings), BitmapSource> Cache = [];
    private static readonly Dictionary<string, (byte[] Pixels, int Width, int Height)> Sources = [];

    public static string Picture { get; private set; } = DefaultPicture;

    /// <summary>Picks the picture every preview uses; unknown names fall back to the default.</summary>
    public static void Use(string? picture) => Picture = picture is not null && Pictures.Contains(picture) ? picture : DefaultPicture;

    /// <summary>The picture as it is, for choosing one.</summary>
    public static BitmapSource Original(string picture) => Render(picture, new ColorSettings());

    public static BitmapSource Render(ColorSettings settings) => Render(Picture, settings);

    /// <summary>
    /// DispHub's own picture, the wordmark on the logo's colours (960 by 600), for the Home page. Its source is
    /// branding/home.
    /// </summary>
    public static BitmapSource Home(ColorSettings settings) => Render("home", settings);

    private static BitmapSource Render(string picture, ColorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Cache.TryGetValue((picture, settings), out BitmapSource? cached))
            return cached;

        (byte[] source, int width, int height) = Load(picture);
        byte[] pixels = new byte[source.Length];
        GammaRamp ramp = RampBuilder.Build(settings);
        ReadOnlySpan<ushort> red = ramp.Red, green = ramp.Green, blue = ramp.Blue;
        double saturation = settings.Vibrance / (double)ColorSettings.NeutralVibrance;
        bool saturate = Math.Abs(saturation - 1) > 0.001;

        for (int i = 0; i < source.Length; i += 4)
        {
            double b = source[i], g = source[i + 1], r = source[i + 2];
            if (saturate)
            {
                double luma = (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
                r = luma + ((r - luma) * saturation);
                g = luma + ((g - luma) * saturation);
                b = luma + ((b - luma) * saturation);
            }
            pixels[i] = (byte)(blue[Clamp(b)] >> 8);
            pixels[i + 1] = (byte)(green[Clamp(g)] >> 8);
            pixels[i + 2] = (byte)(red[Clamp(r)] >> 8);
            pixels[i + 3] = 255;
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        if (Cache.Count >= MaxCached)
            Cache.Clear();
        Cache[(picture, settings)] = bitmap;
        return bitmap;
    }

    private static (byte[] Pixels, int Width, int Height) Load(string picture)
    {
        if (Sources.TryGetValue(picture, out var loaded))
            return loaded;
        var decoder = BitmapDecoder.Create(
            new Uri($"pack://application:,,,/Assets/Previews/{picture}.jpg"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        return Sources[picture] = (pixels, converted.PixelWidth, converted.PixelHeight);
    }

    private static int Clamp(double value) => (int)Math.Clamp(Math.Round(value), 0, 255);
}
