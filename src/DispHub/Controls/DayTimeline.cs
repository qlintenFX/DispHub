using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DispHub.Controls;

/// <summary>One stretch of the day: a profile from <see cref="Start"/> to <see cref="End"/> (minutes since midnight).</summary>
/// <param name="SlotId">The schedule change that starts this stretch; null when the day continues yesterday's last one.</param>
/// <param name="Colour">A colour for the profile, from what it does to a photo (warmer, dimmer...).</param>
/// <param name="FadeMinutes">How long the stretch takes to fade in from the one before it.</param>
/// <param name="Picture">The preview photo in the profile's colours, as on the profile cards.</param>
public sealed record TimelineSegment(Guid? SlotId, string Label, Color Colour, double Start, double End, double FadeMinutes, ImageSource? Picture = null);

/// <summary>
/// A 24-hour bar showing which profile runs when: each stretch shows the preview photo in that profile's colours
/// (as on the profile cards), fades blend one photo into the next, and sunrise, sunset and "now" are marked.
/// Names sit on a small dark label so they read on any photo. Clicking a stretch selects the change that starts it.
/// </summary>
public sealed class DayTimeline : FrameworkElement
{
    private const double MinutesPerDay = 24 * 60;
    private const double TrackTop = 26;
    private const double TrackHeight = 40;
    private const double LabelsHeight = 22;

    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IReadOnlyList<TimelineSegment>), typeof(DayTimeline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SunriseProperty = DependencyProperty.Register(
        nameof(Sunrise), typeof(double?), typeof(DayTimeline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SunsetProperty = DependencyProperty.Register(
        nameof(Sunset), typeof(double?), typeof(DayTimeline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty NowProperty = DependencyProperty.Register(
        nameof(Now), typeof(double?), typeof(DayTimeline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedSlotProperty = DependencyProperty.Register(
        nameof(SelectedSlot), typeof(Guid?), typeof(DayTimeline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public DayTimeline()
    {
        Focusable = false;
        Cursor = Cursors.Hand;
    }

    public IReadOnlyList<TimelineSegment>? Segments
    {
        get => (IReadOnlyList<TimelineSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double? Sunrise { get => (double?)GetValue(SunriseProperty); set => SetValue(SunriseProperty, value); }
    public double? Sunset { get => (double?)GetValue(SunsetProperty); set => SetValue(SunsetProperty, value); }
    public double? Now { get => (double?)GetValue(NowProperty); set => SetValue(NowProperty, value); }
    public Guid? SelectedSlot { get => (Guid?)GetValue(SelectedSlotProperty); set => SetValue(SelectedSlotProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width, TrackTop + TrackHeight + LabelsHeight + 2);

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        DrawingContext dc = drawingContext;
        double width = ActualWidth;
        if (width <= 0)
            return;
        Brush text = Resource("TextFillColorPrimaryBrush", Brushes.White);
        Brush muted = Resource("TextFillColorSecondaryBrush", Brushes.Gray);
        Brush accent = Resource("AccentFillColorDefaultBrush", Brushes.DodgerBlue);
        Brush stroke = Resource("CardStrokeColorDefaultBrush", Brushes.DimGray);
        var track = new Rect(0, TrackTop, width, TrackHeight);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        dc.PushClip(new RectangleGeometry(track, 6, 6));
        dc.DrawRectangle(Resource("ControlFillColorSecondaryBrush", Brushes.DimGray), null, track);
        IReadOnlyList<TimelineSegment> segments = Segments ?? [];
        for (int i = 0; i < segments.Count; i++)
        {
            TimelineSegment segment = segments[i];
            double x0 = X(segment.Start), x1 = X(segment.End);
            if (x1 <= x0)
                continue;
            var area = new Rect(x0, TrackTop, x1 - x0, TrackHeight);
            double fadeEnd = X(Math.Min(segment.Start + segment.FadeMinutes, segment.End));
            bool fades = segment.FadeMinutes > 0 && i > 0 && fadeEnd > x0;

            // A fade shows the photo before it underneath, and this one coming in over it.
            if (fades)
                dc.DrawRectangle(Fill(segments[i - 1], track), null, new Rect(x0, TrackTop, fadeEnd - x0, TrackHeight));
            Brush fill = Fill(segment, track);
            if (fades)
            {
                var mask = new LinearGradientBrush(Colors.Transparent, Colors.Black, new Point(x0, 0), new Point(fadeEnd, 0)) { MappingMode = BrushMappingMode.Absolute };
                dc.PushOpacityMask(Frozen(mask));
                dc.DrawRectangle(fill, null, area);
                dc.Pop();
            }
            else
            {
                dc.DrawRectangle(fill, null, area);
                if (i > 0)
                    dc.DrawRectangle(Resource("CardBackgroundFillColorDefaultBrush", Brushes.Black), null, new Rect(x0 - 1, TrackTop, 2, TrackHeight));
            }

            // The profile's name on a small dark label, if it fits.
            FormattedText label = Text(segment.Label, 12, Brushes.White, dpi, FontWeights.SemiBold);
            var plate = new Rect(Math.Max(x0, fades ? fadeEnd : x0) + 6, TrackTop + ((TrackHeight - label.Height - 4) / 2), label.Width + 12, label.Height + 4);
            if (plate.Right + 4 < x1)
            {
                dc.DrawRoundedRectangle(Frozen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0))), null, plate, 4, 4);
                dc.DrawText(label, new Point(plate.X + 6, plate.Y + 2));
            }

            if (segment.SlotId is { } id && id == SelectedSlot)
                dc.DrawRoundedRectangle(null, new Pen(accent, 2.5), Inset(area, 1.25), 5, 5);
        }
        dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(stroke, 1), track, 6, 6);

        // Hour labels every 6 hours (every 3 when there's room for them).
        int step = width >= 640 ? 3 : 6;
        for (int hour = 0; hour <= 24; hour += step)
        {
            double x = X(hour * 60);
            FormattedText label = Text(HourLabel(hour), 11, muted, dpi);
            double lx = Math.Clamp(x - (label.Width / 2), 0, width - label.Width);
            dc.DrawText(label, new Point(lx, TrackTop + TrackHeight + 4));
        }

        // Sunrise and sunset above the bar.
        DrawSun(dc, Sunrise, "", muted, dpi);
        DrawSun(dc, Sunset, "", muted, dpi);

        if (Now is { } now)
        {
            double x = X(now);
            dc.DrawLine(new Pen(text, 2), new Point(x, TrackTop - 3), new Point(x, TrackTop + TrackHeight + 2));
            dc.DrawEllipse(text, null, new Point(x, TrackTop - 4), 3.5, 3.5);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);
        Point p = e.GetPosition(this);
        ToolTip = SegmentAt(p) is { } segment ? $"{segment.Label}  {TimeLabel(segment.Start)} – {TimeLabel(segment.End)}" : null;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonDown(e);
        if (SegmentAt(e.GetPosition(this))?.SlotId is { } slot)
        {
            SelectedSlot = slot;
            e.Handled = true;
        }
    }

    private double X(double minutes) => minutes / MinutesPerDay * ActualWidth;

    private TimelineSegment? SegmentAt(Point p)
    {
        if (p.Y < TrackTop || p.Y > TrackTop + TrackHeight || Segments is null)
            return null;
        double minutes = p.X / Math.Max(ActualWidth, 1) * MinutesPerDay;
        return Segments.LastOrDefault(s => s.Start <= minutes && minutes < s.End);
    }

    private void DrawSun(DrawingContext dc, double? minutes, string glyph, Brush brush, double dpi)
    {
        if (minutes is not { } m)
            return;
        double x = X(m);
        FormattedText icon = Text(glyph, 13, brush, dpi, family: "Segoe Fluent Icons, Segoe MDL2 Assets");
        FormattedText time = Text(TimeLabel(m), 11, brush, dpi);
        double total = icon.Width + 4 + time.Width;
        double left = Math.Clamp(x - (icon.Width / 2), 0, ActualWidth - total);
        dc.DrawText(icon, new Point(left, 4));
        dc.DrawText(time, new Point(left + icon.Width + 4, 4));
        dc.DrawLine(new Pen(brush, 1) { DashStyle = DashStyles.Dot }, new Point(x, 20), new Point(x, TrackTop));
    }

    /// <summary>The stretch's photo, laid out across the whole bar so neighbouring stretches line up.</summary>
    private static Brush Fill(TimelineSegment segment, Rect track) =>
        segment.Picture is { } picture
            ? Frozen(new ImageBrush(picture) { Stretch = Stretch.UniformToFill, ViewboxUnits = BrushMappingMode.RelativeToBoundingBox, Viewport = track, ViewportUnits = BrushMappingMode.Absolute })
            : Frozen(new SolidColorBrush(segment.Colour));

    private static Rect Inset(Rect r, double by) => new(r.X + by, r.Y + by, Math.Max(0, r.Width - (2 * by)), Math.Max(0, r.Height - (2 * by)));

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private Brush Resource(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private static FormattedText Text(string value, double size, Brush brush, double dpi, FontWeight? weight = null, string family = "Segoe UI Variable Text, Segoe UI") =>
        new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily(family), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, brush, dpi);

    private static string HourLabel(int hour) =>
        new TimeOnly(hour % 24, 0).ToString("t", CultureInfo.CurrentCulture);

    /// <summary>A time of day in the user's format, e.g. 21:30 or 9:30 PM.</summary>
    /// <summary>When something next happens, as Windows says it: "at 21:00", "tomorrow at 07:00", "on Monday at 07:00".</summary>
    public static string WhenLabel(DateTime when, DateTime now)
    {
        string time = TimeLabel(when.TimeOfDay.TotalMinutes);
        return (when.Date - now.Date).Days switch
        {
            0 => Localization.Loc.Format("Schedule_AtTime", time),
            1 => Localization.Loc.Format("Schedule_TomorrowAt", time),
            _ => Localization.Loc.Format("Schedule_DayAt", when.ToString("dddd", CultureInfo.CurrentCulture), time),
        };
    }

    public static string TimeLabel(double minutes)
    {
        int m = (int)Math.Round(minutes) % (24 * 60);
        return new TimeOnly(m / 60, m % 60).ToString("t", CultureInfo.CurrentCulture);
    }
}
