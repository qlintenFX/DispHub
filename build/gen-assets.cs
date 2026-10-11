#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false
#:property PublishTrimmed=false
#:property TreatWarningsAsErrors=false
#:property AnalysisLevel=none

// Draws the DispHub icon and writes every size the app and the MSIX package need.
// Run from the repo root: dotnet run build/gen-assets.cs
//
// The icon is a monitor whose screen shows a vivid gradient meeting the original dark screen along a wave:
// "your display, tuned". It is drawn as vectors so every size from 16 px to 1024 px stays crisp.
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

string root = Directory.GetCurrentDirectory();
string appAssets = Path.Combine(root, "src", "DispHub", "Assets");
string packageAssets = Path.Combine(root, "packaging", "Assets");
Directory.CreateDirectory(appAssets);
Directory.CreateDirectory(packageAssets);

var thread = new Thread(() =>
{
    // App icon (multi-size .ico) and a PNG for in-app use.
    WriteIco(Path.Combine(appAssets, "DispHub.ico"), [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]);
    SavePng(Icon(256, plate: false), Path.Combine(appAssets, "DispHub.png"));

    // MSIX visual assets. Tiles use the light rounded plate; taskbar/Start icons are unplated.
    foreach ((string scale, double factor) in new[] { ("100", 1.0), ("125", 1.25), ("150", 1.5), ("200", 2.0), ("400", 4.0) })
    {
        SavePng(Icon(Round(44 * factor), plate: false), Path.Combine(packageAssets, $"Square44x44Logo.scale-{scale}.png"));
        SavePng(Icon(Round(150 * factor), plate: true), Path.Combine(packageAssets, $"Square150x150Logo.scale-{scale}.png"));
        SavePng(Wide(Round(310 * factor), Round(150 * factor)), Path.Combine(packageAssets, $"Wide310x150Logo.scale-{scale}.png"));
        SavePng(Icon(Round(50 * factor), plate: true), Path.Combine(packageAssets, $"StoreLogo.scale-{scale}.png"));
    }

    foreach (int size in new[] { 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 })
    {
        BitmapSource icon = Icon(size, plate: false);
        SavePng(icon, Path.Combine(packageAssets, $"Square44x44Logo.targetsize-{size}.png"));
        SavePng(icon, Path.Combine(packageAssets, $"Square44x44Logo.targetsize-{size}_altform-unplated.png"));
        SavePng(icon, Path.Combine(packageAssets, $"Square44x44Logo.targetsize-{size}_altform-lightunplated.png"));
    }

    // Large master for the Store listing.
    SavePng(Icon(1024, plate: true), Path.Combine(root, "store", "icon-1024.png"));
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();
Console.WriteLine("Assets written.");

static int Round(double value) => (int)Math.Round(value);

static Color Hex(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

static BitmapSource Icon(int size, bool plate)
{
    var visual = new DrawingVisual();
    using (DrawingContext dc = visual.RenderOpen())
    {
        double s = size;
        if (plate)
        {
            var background = new LinearGradientBrush(Hex(0xFBFCFD), Hex(0xE6E9EE), 90);
            dc.DrawRoundedRectangle(background, new Pen(new SolidColorBrush(Hex(0xD3D8DF)), Math.Max(1, s / 256)),
                new Rect(0.5, 0.5, s - 1, s - 1), s * 0.22, s * 0.22);
        }

        // Plated icons leave room around the monitor; unplated ones fill the canvas.
        double area = plate ? 0.68 : 1.0;
        double origin = (1 - area) / 2 * s;
        DrawMonitor(dc, origin, origin + (plate ? s * 0.02 : 0), s * area, size);
    }
    return Render(visual, size, size);
}

static void DrawMonitor(DrawingContext dc, double x, double y, double w, int pixelSize)
{
    bool tiny = pixelSize <= 24;
    double bezel = w * (tiny ? 0.07 : 0.05);
    double bodyHeight = w * 0.66;
    var body = new Rect(x, y + (w * 0.06), w, bodyHeight);

    // Soft shadow under the monitor (skipped at tiny sizes where it only blurs the outline).
    if (!tiny)
    {
        for (int i = 4; i >= 1; i--)
        {
            double spread = w * 0.012 * i;
            var shadow = new Rect(body.X - spread + (w * 0.01), body.Y + (w * 0.03) - spread, body.Width + (2 * spread) - (w * 0.02), body.Height + (2 * spread));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(10, 20, 30, 50)), null, shadow, (w * 0.075) + spread, (w * 0.075) + spread);
        }
    }

    // Bezel.
    var bezelBrush = new LinearGradientBrush(Hex(0xFFFFFF), Hex(0xD9DDE3), 90);
    dc.DrawRoundedRectangle(bezelBrush, new Pen(new SolidColorBrush(Hex(0xB9C0CA)), Math.Max(0.75, w / 220)),
        body, w * 0.075, w * 0.075);

    // Screen: vivid gradient on the left, the original dark screen on the right, split by a wave.
    var screen = new Rect(body.X + bezel, body.Y + bezel, body.Width - (2 * bezel), body.Height - (2 * bezel));
    double r = w * 0.035;
    dc.PushClip(new RectangleGeometry(screen, r, r));

    var dark = new LinearGradientBrush(Hex(0x48515F), Hex(0x232831), new Point(0, 0), new Point(1, 1));
    dc.DrawRectangle(dark, null, screen);

    var vivid = new LinearGradientBrush { StartPoint = new Point(0.15, 0), EndPoint = new Point(0.35, 1) };
    vivid.GradientStops.Add(new GradientStop(Hex(0x2DD4F0), 0.0));
    vivid.GradientStops.Add(new GradientStop(Hex(0x3B82F6), 0.32));
    vivid.GradientStops.Add(new GradientStop(Hex(0x2CC2B2), 0.56));
    vivid.GradientStops.Add(new GradientStop(Hex(0xF5B32B), 0.8));
    vivid.GradientStops.Add(new GradientStop(Hex(0xF97316), 1.0));

    double sx = screen.X, sy = screen.Y, sw = screen.Width, sh = screen.Height;
    var wave = new StreamGeometry();
    using (StreamGeometryContext g = wave.Open())
    {
        g.BeginFigure(new Point(sx - 1, sy - 1), isFilled: true, isClosed: true);
        g.LineTo(new Point(sx + (sw * 0.70), sy - 1), true, false);
        g.BezierTo(
            new Point(sx + (sw * 0.48), sy + (sh * 0.30)),
            new Point(sx + (sw * 0.70), sy + (sh * 0.62)),
            new Point(sx + (sw * 0.44), sy + sh + 1),
            true,
            false);
        g.LineTo(new Point(sx - 1, sy + sh + 1), true, false);
    }
    wave.Freeze();
    dc.DrawGeometry(vivid, null, wave);

    // A thin highlight along the wave edge makes the split read as a sweep, not a crack.
    if (!tiny)
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), Math.Max(0.6, w / 160)), wave);

    dc.Pop();

    // Stand.
    var standBrush = new LinearGradientBrush(Hex(0xF3F4F6), Hex(0xC9CED6), 90);
    var standPen = new Pen(new SolidColorBrush(Hex(0xB9C0CA)), Math.Max(0.75, w / 260));
    double neckWidth = w * (tiny ? 0.22 : 0.16);
    var neck = new Rect(x + ((w - neckWidth) / 2), body.Bottom - (w * 0.01), neckWidth, w * 0.13);
    dc.DrawRectangle(standBrush, null, neck);
    double baseWidth = w * (tiny ? 0.56 : 0.46);
    var foot = new Rect(x + ((w - baseWidth) / 2), neck.Bottom - (w * 0.015), baseWidth, w * (tiny ? 0.08 : 0.06));
    dc.DrawRoundedRectangle(standBrush, standPen, foot, w * 0.03, w * 0.03);
}

static BitmapSource Wide(int width, int height)
{
    var visual = new DrawingVisual();
    using (DrawingContext dc = visual.RenderOpen())
    {
        var background = new LinearGradientBrush(Hex(0xFBFCFD), Hex(0xE6E9EE), 90);
        dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
        double w = height * 0.7;
        DrawMonitor(dc, (width - w) / 2, (height - (w * 0.92)) / 2, w, (int)w);
    }
    return Render(visual, width, height);
}

static BitmapSource Render(Visual visual, int width, int height)
{
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    bitmap.Freeze();
    return bitmap;
}

static void SavePng(BitmapSource bitmap, string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using FileStream stream = File.Create(path);
    encoder.Save(stream);
}

static void WriteIco(string path, int[] sizes)
{
    var images = sizes.Select(size =>
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Icon(size, plate: false)));
        using var memory = new MemoryStream();
        encoder.Save(memory);
        return (size, data: memory.ToArray());
    }).ToList();

    using var file = new BinaryWriter(File.Create(path));
    file.Write((short)0);
    file.Write((short)1);
    file.Write((short)images.Count);
    int offset = 6 + (16 * images.Count);
    foreach ((int size, byte[] data) in images)
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
    foreach ((_, byte[] data) in images)
        file.Write(data);
}
