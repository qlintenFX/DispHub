#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property PublishAot=false
#:property PublishTrimmed=false
#:property TreatWarningsAsErrors=false
#:property AnalysisLevel=none

// Draws the DispHub icon and writes every size the app and the MSIX package need.
// Run from the repo root: dotnet run build/gen-assets.cs
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
    SavePng(Icon(256, plate: true), Path.Combine(appAssets, "DispHub.png"));

    // MSIX visual assets. Plated tiles use the dark rounded square; unplated taskbar icons are transparent.
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

static BitmapSource Icon(int size, bool plate)
{
    var visual = new DrawingVisual();
    using (DrawingContext dc = visual.RenderOpen())
    {
        double s = size;
        if (plate)
        {
            var background = new LinearGradientBrush(Color.FromRgb(0x2D, 0x31, 0x3D), Color.FromRgb(0x14, 0x16, 0x1C), 90);
            dc.DrawRoundedRectangle(background, null, new Rect(0, 0, s, s), s * 0.22, s * 0.22);
        }

        // Small sizes get a thicker ring so it still reads at 16 px.
        double inset = plate ? 0.14 : 0.04;
        double thickness = size <= 24 ? 0.20 : 0.14;
        DrawHueRing(dc, new Point(s / 2, s / 2), s * (0.5 - inset), s * (0.5 - inset - thickness));

        // Half-filled circle: the universal "contrast" symbol.
        double r = s * (0.5 - inset - thickness - (size <= 24 ? 0.06 : 0.08));
        var center = new Point(s / 2, s / 2);
        double stroke = Math.Max(1, s * 0.035);
        var white = new SolidColorBrush(Color.FromRgb(0xF4, 0xF5, 0xF7));
        if (!plate)
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x1B, 0x1D, 0x24)), null, center, r, r);
        dc.DrawEllipse(null, new Pen(white, stroke), center, r - (stroke / 2), r - (stroke / 2));
        var half = new PathGeometry();
        var figure = new PathFigure { StartPoint = new Point(center.X, center.Y - r), IsClosed = true };
        figure.Segments.Add(new ArcSegment(new Point(center.X, center.Y + r), new Size(r, r), 0, false, SweepDirection.Clockwise, true));
        half.Figures.Add(figure);
        dc.DrawGeometry(white, null, half);
    }
    return Render(visual, size, size);
}

static BitmapSource Wide(int width, int height)
{
    var visual = new DrawingVisual();
    using (DrawingContext dc = visual.RenderOpen())
    {
        var background = new LinearGradientBrush(Color.FromRgb(0x2D, 0x31, 0x3D), Color.FromRgb(0x14, 0x16, 0x1C), 90);
        dc.DrawRectangle(background, null, new Rect(0, 0, width, height));
        BitmapSource icon = Icon((int)(height * 0.8), plate: false);
        dc.DrawImage(icon, new Rect((width - icon.PixelWidth) / 2.0, (height - icon.PixelHeight) / 2.0, icon.PixelWidth, icon.PixelHeight));
    }
    return Render(visual, width, height);
}

static void DrawHueRing(DrawingContext dc, Point center, double outer, double inner)
{
    const int Segments = 720;
    for (int i = 0; i < Segments; i++)
    {
        // Start at the top, go clockwise; overlap each wedge slightly to hide seams.
        double a0 = ((i / (double)Segments) * 360) - 90;
        double a1 = (((i + 1.6) / Segments) * 360) - 90;
        var geometry = new PathGeometry();
        var figure = new PathFigure { StartPoint = Polar(center, outer, a0), IsClosed = true };
        figure.Segments.Add(new ArcSegment(Polar(center, outer, a1), new Size(outer, outer), 0, false, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(Polar(center, inner, a1), true));
        figure.Segments.Add(new ArcSegment(Polar(center, inner, a0), new Size(inner, inner), 0, false, SweepDirection.Counterclockwise, true));
        geometry.Figures.Add(figure);
        dc.DrawGeometry(new SolidColorBrush(Hue(i * 360.0 / Segments)), null, geometry);
    }
}

static Point Polar(Point c, double r, double degrees)
{
    double rad = degrees * Math.PI / 180;
    return new Point(c.X + (r * Math.Cos(rad)), c.Y + (r * Math.Sin(rad)));
}

static Color Hue(double h)
{
    // HSV with S = 0.85, V = 1.
    double s = 0.85, v = 1.0;
    double c = v * s, x = c * (1 - Math.Abs(((h / 60) % 2) - 1)), m = v - c;
    (double r, double g, double b) = h switch
    {
        < 60 => (c, x, 0.0),
        < 120 => (x, c, 0.0),
        < 180 => (0.0, c, x),
        < 240 => (0.0, x, c),
        < 300 => (x, 0.0, c),
        _ => (c, 0.0, x),
    };
    return Color.FromRgb((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
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
        encoder.Frames.Add(BitmapFrame.Create(Icon(size, plate: size >= 48)));
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
