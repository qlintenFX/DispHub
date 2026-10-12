using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DispHub.Controls;

/// <summary>One stretch of the day: a profile from <see cref="Start"/> to <see cref="End"/> (minutes since midnight).</summary>
/// <param name="SlotId">The schedule change that starts this stretch; null when the day continues yesterday's last one.</param>
/// <param name="Colour">A colour for the profile, from what it does to a photo (warmer, dimmer...).</param>
/// <param name="FadeMinutes">How long the stretch takes to fade in from the one before it.</param>
public sealed record TimelineSegment(Guid? SlotId, string Label, Color Colour, double Start, double End, double FadeMinutes);

/// <summary>
/// A 24-hour bar showing which profile runs when: one calm colour per profile, fades blending into each other,
/// and sunrise, sunset and "now" marked. Clicking a stretch selects the change that starts it. It only shows the
/// day; the changes are edited in the list under it.
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
            Brush fill = Frozen(new SolidColorBrush(segment.Colour));
            double fadeEnd = X(Math.Min(segment.Start + segment.FadeMinutes, segment.End));
            if (segment.FadeMinutes > 0 && i > 0 && fadeEnd > x0)
            {
                // The fade: from the previous profile's colour into this one's.
                dc.DrawRectangle(new LinearGradientBrush(segments[i - 1].Colour, segment.Colour, new Point(0, 0), new Point(1, 0)), null,
                    new Rect(x0, TrackTop, fadeEnd - x0, TrackHeight));
                dc.DrawRectangle(fill, null, new Rect(fadeEnd, TrackTop, Math.Max(0, x1 - fadeEnd), TrackHeight));
            }
            else
            {
                dc.DrawRectangle(fill, null, area);
            }

            // The profile's name, if it fits, in a colour that reads on the fill.
            Brush ink = Luminance(segment.Colour) > 0.55 ? Brushes.Black : Brushes.White;
            FormattedText label = Text(segment.Label, 12, ink, dpi, FontWeights.SemiBold);
            if (label.Width + 16 < area.Width)
                dc.DrawText(label, new Point(x0 + 8, TrackTop + ((TrackHeight - label.Height) / 2)));

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

    private static double Luminance(Color c) => ((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B)) / 255;

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
    public static string TimeLabel(double minutes)
    {
        int m = (int)Math.Round(minutes) % (24 * 60);
        return new TimeOnly(m / 60, m % 60).ToString("t", CultureInfo.CurrentCulture);
    }
}
