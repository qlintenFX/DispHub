using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DispHub.Core.Color;

namespace DispHub.Controls;

/// <summary>
/// Renders a small landscape through a profile's actual colour maths (vibrance, then the gamma ramp), so each profile
/// card shows what it will look like: warmer, brighter, punchier or dimmer.
/// </summary>
public static class ColorPreview
{
    public const int Width = 336;
    public const int Height = 168;

    private static readonly Dictionary<ColorSettings, BitmapSource> Cache = [];
    private static byte[]? _scene;

    public static BitmapSource Render(ColorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (Cache.TryGetValue(settings, out BitmapSource? cached))
            return cached;

        byte[] source = _scene ??= DrawScene();
        byte[] pixels = new byte[source.Length];
        GammaRamp ramp = RampBuilder.Build(settings);
        ReadOnlySpan<ushort> red = ramp.Red, green = ramp.Green, blue = ramp.Blue;
        double saturation = settings.Vibrance / (double)ColorSettings.NeutralVibrance;

        for (int i = 0; i < source.Length; i += 4)
        {
            double b = source[i], g = source[i + 1], r = source[i + 2];
            if (Math.Abs(saturation - 1) > 0.001)
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

        BitmapSource bitmap = BitmapSource.Create(Width, Height, 192, 192, PixelFormats.Bgra32, null, pixels, Width * 4);
        bitmap.Freeze();
        if (Cache.Count > 64)
            Cache.Clear();
        Cache[settings] = bitmap;
        return bitmap;
    }

    private static int Clamp(double value) => (int)Math.Clamp(Math.Round(value), 0, 255);

    /// <summary>Sunset over hills: bright sky, a warm sun, saturated mid-tones and dark foreground detail.</summary>
    private static byte[] DrawScene()
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            double w = Width, h = Height;
            var sky = new LinearGradientBrush { StartPoint = new Point(0.5, 0), EndPoint = new Point(0.5, 1) };
            sky.GradientStops.Add(new GradientStop(Color.FromRgb(0x1D, 0x4E, 0xD8), 0));
            sky.GradientStops.Add(new GradientStop(Color.FromRgb(0x7C, 0x5C, 0xE0), 0.35));
            sky.GradientStops.Add(new GradientStop(Color.FromRgb(0xF4, 0x72, 0x7C), 0.58));
            sky.GradientStops.Add(new GradientStop(Color.FromRgb(0xFD, 0xBA, 0x74), 0.72));
            dc.DrawRectangle(sky, null, new Rect(0, 0, w, h));

            var sun = new RadialGradientBrush(Color.FromRgb(0xFF, 0xF7, 0xD6), Color.FromRgb(0xFB, 0x92, 0x3C));
            dc.DrawEllipse(sun, null, new Point(w * 0.68, h * 0.6), h * 0.13, h * 0.13);

            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x6D, 0x4A, 0xB8)), null,
                Hills(w, h, [(0.0, 0.62), (0.18, 0.5), (0.36, 0.6), (0.55, 0.47), (0.78, 0.6), (1.0, 0.52)]));
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E)), null,
                Hills(w, h, [(0.0, 0.7), (0.22, 0.62), (0.45, 0.72), (0.7, 0.63), (1.0, 0.71)]));
            var ground = new LinearGradientBrush(Color.FromRgb(0x2F, 0x9E, 0x44), Color.FromRgb(0x05, 0x2E, 0x16), 90);
            dc.DrawGeometry(ground, null, Hills(w, h, [(0.0, 0.8), (0.3, 0.74), (0.62, 0.82), (1.0, 0.76)]));
        }

        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        byte[] pixels = new byte[Width * Height * 4];
        bitmap.CopyPixels(pixels, Width * 4, 0);
        return pixels;
    }

    private static StreamGeometry Hills(double w, double h, (double X, double Y)[] points)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext g = geometry.Open())
        {
            g.BeginFigure(new Point(0, h), isFilled: true, isClosed: true);
            g.LineTo(new Point(0, points[0].Y * h), true, true);
            for (int i = 1; i < points.Length; i++)
            {
                (double px, double py) = points[i - 1];
                (double x, double y) = points[i];
                double mid = (px + x) / 2;
                g.BezierTo(new Point(mid * w, py * h), new Point(mid * w, y * h), new Point(x * w, y * h), true, true);
            }
            g.LineTo(new Point(w, h), true, true);
        }
        geometry.Freeze();
        return geometry;
    }
}
