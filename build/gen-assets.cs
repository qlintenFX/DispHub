#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false
#:property PublishTrimmed=false
#:property TreatWarningsAsErrors=false
#:property AnalysisLevel=none

// Writes every icon size the app, the MSIX package, the Store and the README need from the master artwork in
// branding/ (icon.png, the monitor on a transparent canvas, and icon-shadow.png, its soft shadow).
// Run from the repo root: dotnet run build/gen-assets.cs
//
// Sizes are resampled with Lanczos-3 in premultiplied alpha (no dark or light fringes), and small sizes are
// placed so the monitor's left and right edges land on whole pixels, which keeps 16-48 px icons crisp.
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

string root = Directory.GetCurrentDirectory();
string branding = Path.Combine(root, "branding");
string appAssets = Path.Combine(root, "src", "DispHub", "Assets");
string packageAssets = Path.Combine(root, "packaging", "Assets");

var thread = new Thread(() =>
{
    var icon = Image.Load(Path.Combine(branding, "icon.png"));
    var shadow = Image.Load(Path.Combine(branding, "icon-shadow.png"));
    var bounds = icon.OpaqueBounds();

    // Square icon: the monitor fitted into the canvas; the shadow only at sizes where it reads as depth, not blur.
    Image Icon(int size, double fill = 1.0)
    {
        int margin = size <= 24 ? 0 : size <= 48 ? 1 : (int)Math.Round(size * 0.04);
        double scale = ((size * fill) - (2 * margin)) / Math.Max(bounds.Width, bounds.Height);
        int width = (int)Math.Round(bounds.Width * scale);
        int height = (int)Math.Round(bounds.Height * scale);
        double sx = (double)width / bounds.Width, sy = (double)height / bounds.Height;
        int x = (size - width) / 2, y = (size - height) / 2;
        var canvas = new Image(size, size);
        if (size >= 96)
        {
            // Same transform as the monitor, applied to the whole shadow canvas.
            int cw = (int)Math.Round(shadow.Width * sx), ch = (int)Math.Round(shadow.Height * sy);
            canvas.Over(shadow.Resize(cw, ch), x - (int)Math.Round(bounds.X * sx), y - (int)Math.Round(bounds.Y * sy));
        }
        canvas.Over(icon.Crop(bounds).Resize(width, height), x, y);
        return canvas;
    }

    // App icon (multi-size .ico: window, taskbar and notification area) and PNGs for the window and About page.
    int[] icoSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
    WriteIco(Path.Combine(appAssets, "DispHub.ico"), icoSizes.Select(s => Icon(s)).ToArray());
    // Shown in the notification area while DispHub is paused: the same monitor, greyed out.
    WriteIco(Path.Combine(appAssets, "DispHub-paused.ico"), icoSizes.Where(s => s <= 64).Select(s => Icon(s).Desaturated(0.55)).ToArray());
    Icon(256).Save(Path.Combine(appAssets, "DispHub.png"));
    Icon(64).Save(Path.Combine(appAssets, "DispHub-64.png"));

    // MSIX visual assets, all unplated (transparent), as Windows 11 shows them.
    foreach ((string scale, double factor) in new[] { ("100", 1.0), ("125", 1.25), ("150", 1.5), ("200", 2.0), ("400", 4.0) })
    {
        Icon(Round(44 * factor)).Save(Path.Combine(packageAssets, $"Square44x44Logo.scale-{scale}.png"));
        Icon(Round(150 * factor), fill: 0.66).Save(Path.Combine(packageAssets, $"Square150x150Logo.scale-{scale}.png"));
        Wide(Round(310 * factor), Round(150 * factor)).Save(Path.Combine(packageAssets, $"Wide310x150Logo.scale-{scale}.png"));
        Icon(Round(50 * factor)).Save(Path.Combine(packageAssets, $"StoreLogo.scale-{scale}.png"));
    }
    foreach (int size in new[] { 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 })
    {
        Image image = Icon(size);
        image.Save(Path.Combine(packageAssets, $"Square44x44Logo.targetsize-{size}.png"));
        image.Save(Path.Combine(packageAssets, $"Square44x44Logo.targetsize-{size}_altform-unplated.png"));
        image.Save(Path.Combine(packageAssets, $"Square44x44Logo.targetsize-{size}_altform-lightunplated.png"));
    }

    // Store listing icon and the README wordmark (dark text for light themes, light text for dark themes).
    Icon(1024, fill: 0.92).Save(Path.Combine(root, "store", "icon-1024.png"));
    Wordmark(Icon(192), dark: true).Save(Path.Combine(root, "docs", "images", "wordmark-light.png"));
    Wordmark(Icon(192), dark: false).Save(Path.Combine(root, "docs", "images", "wordmark-dark.png"));
    // The same light wordmark for the app, shown on the Home page's picture.
    Wordmark(Icon(192), dark: false).Save(Path.Combine(appAssets, "wordmark.png"));

    Image Wide(int width, int height)
    {
        var canvas = new Image(width, height);
        int side = (int)Math.Round(height * 0.7 / 0.66);
        canvas.Over(Icon(side, fill: 0.66), (width - side) / 2, (height - side) / 2);
        return canvas;
    }
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
Console.WriteLine("Assets written.");

static int Round(double value) => (int)Math.Round(value);

// The icon followed by "DispHub" in Segoe UI Variable, "Hub" in the screen's gradient.
static Image Wordmark(Image icon, bool dark)
{
    const double fontSize = 128;
    var typeface = new Typeface(new FontFamily("Segoe UI Variable Display"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    Brush ink = new SolidColorBrush(dark ? Color.FromRgb(0x14, 0x18, 0x22) : Color.FromRgb(0xF4, 0xF6, 0xFA));
    var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x14, 0x8C, 0xF5), 0.0));
    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x1E, 0xD2, 0xE6), 0.3));
    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x8C, 0xDC, 0x50), 0.55));
    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xF5, 0xC8, 0x2D), 0.78));
    gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0xFA, 0x78, 0x1E), 1.0));

    var disp = new FormattedText("Disp", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, ink, 1);
    var hub = new FormattedText("Hub", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, gradient, 1);
    double gap = 36;
    int width = (int)Math.Ceiling(icon.Width + gap + disp.WidthIncludingTrailingWhitespace + hub.Width + 8);
    int height = icon.Height;

    var visual = new DrawingVisual();
    using (DrawingContext dc = visual.RenderOpen())
    {
        // Centre the capitals on the monitor's screen, which sits above the middle of the icon.
        double baseline = (height * 0.43) + (typeface.CapsHeight * fontSize / 2);
        double x = icon.Width + gap;
        dc.DrawText(disp, new Point(x, baseline - disp.Baseline));
        dc.DrawText(hub, new Point(x + disp.WidthIncludingTrailingWhitespace, baseline - hub.Baseline));
    }
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    var canvas = Image.From(bitmap);
    canvas.Over(icon, 0, 0);
    return canvas;
}

static void WriteIco(string path, Image[] images)
{
    var frames = images.Select(image => (size: image.Width, data: image.EncodePng())).ToList();
    using var file = new BinaryWriter(File.Create(path));
    file.Write((short)0);
    file.Write((short)1);
    file.Write((short)frames.Count);
    int offset = 6 + (16 * frames.Count);
    foreach ((int size, byte[] data) in frames)
    {
        file.Write((byte)(size >= 256 ? 0 : size));
        file.Write((byte)(size >= 256 ? 0 : size));
        file.Write((byte)0);
        file.Write((byte)0);
        file.Write((short)1);
        file.Write((short)32);
        file.Write(data.Length);
        file.Write(offset);
        offset += data.Length;
    }
    foreach ((_, byte[] data) in frames)
        file.Write(data);
}

/// <summary>A premultiplied BGRA float image.</summary>
sealed class Image(int width, int height)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public float[] Pixels { get; } = new float[width * height * 4];

    public static Image Load(string path)
    {
        var decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return From(decoder.Frames[0]);
    }

    public static Image From(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        var image = new Image(converted.PixelWidth, converted.PixelHeight);
        var bytes = new byte[image.Pixels.Length];
        converted.CopyPixels(bytes, image.Width * 4, 0);
        for (int i = 0; i < bytes.Length; i++)
            image.Pixels[i] = bytes[i] / 255f;
        return image;
    }

    public Int32Rect OpaqueBounds(float threshold = 0.5f)
    {
        int x0 = Width, y0 = Height, x1 = -1, y1 = -1;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (Pixels[((y * Width) + x) * 4 + 3] >= threshold)
                {
                    x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
                }
        return new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    /// <summary>Grey version, faded towards transparent by <paramref name="opacity"/>.</summary>
    public Image Desaturated(double opacity)
    {
        var result = new Image(Width, Height);
        for (int i = 0; i < Pixels.Length; i += 4)
        {
            float luma = (0.0722f * Pixels[i]) + (0.7152f * Pixels[i + 1]) + (0.2126f * Pixels[i + 2]);
            float k = (float)opacity;
            result.Pixels[i] = result.Pixels[i + 1] = result.Pixels[i + 2] = luma * k;
            result.Pixels[i + 3] = Pixels[i + 3] * k;
        }
        return result;
    }

    public Image Crop(Int32Rect r)
    {
        var result = new Image(r.Width, r.Height);
        for (int y = 0; y < r.Height; y++)
            Array.Copy(Pixels, (((r.Y + y) * Width) + r.X) * 4, result.Pixels, y * r.Width * 4, r.Width * 4);
        return result;
    }

    public Image Resize(int width, int height) => ResizeAxis(width, horizontal: true).ResizeAxis(height, horizontal: false);

    private Image ResizeAxis(int size, bool horizontal)
    {
        int source = horizontal ? Width : Height;
        int other = horizontal ? Height : Width;
        var result = horizontal ? new Image(size, Height) : new Image(Width, size);
        double scale = (double)size / source;
        double filterScale = Math.Min(scale, 1);
        double support = 3 / filterScale;
        var taps = new List<(int Index, float Weight)>();
        for (int d = 0; d < size; d++)
        {
            double center = (d + 0.5) / scale;
            taps.Clear();
            double sum = 0;
            for (int s = (int)Math.Floor(center - support); s <= (int)Math.Ceiling(center + support); s++)
            {
                double weight = Lanczos((s + 0.5 - center) * filterScale);
                if (weight == 0)
                    continue;
                taps.Add((Math.Clamp(s, 0, source - 1), (float)weight));
                sum += weight;
            }
            for (int o = 0; o < other; o++)
            {
                float b = 0, g = 0, r = 0, a = 0;
                foreach ((int s, float w) in taps)
                {
                    int i = (horizontal ? (o * Width) + s : (s * Width) + o) * 4;
                    b += Pixels[i] * w; g += Pixels[i + 1] * w; r += Pixels[i + 2] * w; a += Pixels[i + 3] * w;
                }
                float k = (float)(1 / sum);
                a = Math.Clamp(a * k, 0, 1);
                int j = (horizontal ? (o * size) + d : (d * Width) + o) * 4;
                result.Pixels[j] = Math.Clamp(b * k, 0, a);
                result.Pixels[j + 1] = Math.Clamp(g * k, 0, a);
                result.Pixels[j + 2] = Math.Clamp(r * k, 0, a);
                result.Pixels[j + 3] = a;
            }
        }
        return result;

        static double Lanczos(double x)
        {
            if (x == 0)
                return 1;
            if (Math.Abs(x) >= 3)
                return 0;
            double px = Math.PI * x;
            return 3 * Math.Sin(px) * Math.Sin(px / 3) / (px * px);
        }
    }

    /// <summary>Draws <paramref name="top"/> over this image with its top-left corner at (x, y).</summary>
    public void Over(Image top, int x, int y)
    {
        for (int ty = 0; ty < top.Height; ty++)
        {
            int cy = y + ty;
            if (cy < 0 || cy >= Height)
                continue;
            for (int tx = 0; tx < top.Width; tx++)
            {
                int cx = x + tx;
                if (cx < 0 || cx >= Width)
                    continue;
                int s = ((ty * top.Width) + tx) * 4, d = ((cy * Width) + cx) * 4;
                float inv = 1 - top.Pixels[s + 3];
                for (int c = 0; c < 4; c++)
                    Pixels[d + c] = top.Pixels[s + c] + (Pixels[d + c] * inv);
            }
        }
    }

    public byte[] EncodePng()
    {
        var bytes = new byte[Pixels.Length];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)Math.Round(Math.Clamp(Pixels[i], 0, 1) * 255);
        var bitmap = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Pbgra32, null, bytes, Width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var memory = new MemoryStream();
        encoder.Save(memory);
        return memory.ToArray();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, EncodePng());
    }
}
