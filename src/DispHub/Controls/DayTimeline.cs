using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DispHub.Controls;

/// <summary>One stretch of the day: a profile from <see cref="Start"/> to <see cref="End"/> (minutes since midnight).</summary>
/// <param name="SlotId">The schedule slot that starts this segment; null when the day continues yesterday's last slot.</param>
/// <param name="FadeMinutes">How long the segment takes to fade in from the one before it.</param>
public sealed record TimelineSegment(Guid? SlotId, string Label, ImageSource? Picture, double Start, double End, double FadeMinutes);

/// <summary>A boundary handle moved to a new time (minutes since midnight, snapped to 5 minutes).</summary>
public sealed class TimelineDragEventArgs(Guid slotId, double minutes) : EventArgs
{
    public Guid SlotId { get; } = slotId;
    public double Minutes { get; } = minutes;
}

/// <summary>
/// A 24-hour bar showing which profile runs when. Segments show their profile's preview strip, fades blend into
/// each other, and sunrise, sunset and "now" are marked. Segment starts can be dragged to change a slot's time.
/// </summary>
public sealed class DayTimeline : FrameworkElement
{
    private const double MinutesPerDay = 24 * 60;
    private const double TrackTop = 30;
    private const double TrackHeight = 56;
    private const double LabelsHeight = 22;
    private const double SnapMinutes = 5;

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

    private Guid? _dragging;
    private double _dragMinutes;
    private Guid? _hoverHandle;

    public DayTimeline()
    {
        Focusable = false;
        Cursor = Cursors.Arrow;
    }

    /// <summary>Raised once when a drag ends.</summary>
    public event EventHandler<TimelineDragEventArgs>? SlotMoved;

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
        new(double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width, TrackTop + TrackHeight + LabelsHeight + 4);

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        DrawingContext dc = drawingContext;
        double width = ActualWidth;
        if (width <= 0)
            return;
        Brush text = Resource("TextFillColorPrimaryBrush", Brushes.White);
        Brush muted = Resource("TextFillColorTertiaryBrush", Brushes.Gray);
        Brush accent = Resource("AccentFillColorDefaultBrush", Brushes.DodgerBlue);
        Brush stroke = Resource("CardStrokeColorDefaultBrush", Brushes.DimGray);
        var track = new Rect(0, TrackTop, width, TrackHeight);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        // Track background and segments, clipped to rounded corners.
        dc.PushClip(new RectangleGeometry(track, 8, 8));
        dc.DrawRectangle(Resource("ControlFillColorSecondaryBrush", Brushes.DimGray), null, track);
        IReadOnlyList<TimelineSegment> segments = Segments ?? [];
        for (int i = 0; i < segments.Count; i++)
        {
            TimelineSegment segment = segments[i];
            double x0 = X(Shown(segment)), x1 = X(segment.End);
            if (x1 <= x0)
                continue;
            var area = new Rect(x0, TrackTop, x1 - x0, TrackHeight);
            Brush fill = Fill(segment);
            double fadeEnd = X(Math.Min(Shown(segment) + segment.FadeMinutes, segment.End));
            if (segment.FadeMinutes > 0 && i > 0 && fadeEnd > x0)
            {
                // The fade: the previous profile continues underneath while this one fades in over it.
                dc.DrawRectangle(Fill(segments[i - 1]), null, new Rect(x0, TrackTop, fadeEnd - x0, TrackHeight));
                // The mask is relative to this segment: transparent at its start, opaque where the fade ends.
                dc.PushOpacityMask(new LinearGradientBrush(Colors.Transparent, Colors.Black, new Point(0, 0), new Point((fadeEnd - x0) / area.Width, 0)));
                dc.DrawRectangle(fill, null, area);
                dc.Pop();
            }
            else
            {
                dc.DrawRectangle(fill, null, area);
            }

            // Name chip, if it fits.
            FormattedText label = Text(segment.Label, 12, Brushes.White, dpi, FontWeights.SemiBold);
            if (label.Width + 20 < area.Width)
            {
                var chip = new Rect(x0 + 8, TrackTop + 8, label.Width + 12, label.Height + 4);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, chip, 4, 4);
                dc.DrawText(label, new Point(chip.X + 6, chip.Y + 2));
            }

            if (segment.SlotId is { } id && id == SelectedSlot)
                dc.DrawRoundedRectangle(null, new Pen(accent, 2.5), Inset(area, 1.25), 6, 6);
        }
        dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(stroke, 1), track, 8, 8);

        // Drag handles at each slot start.
        foreach (TimelineSegment segment in segments.Where(s => s.SlotId is not null))
        {
            double x = X(Shown(segment));
            bool hot = segment.SlotId == _hoverHandle || segment.SlotId == _dragging;
            var handle = new Rect(x - 3, TrackTop + (TrackHeight / 2) - 12, 6, 24);
            dc.DrawRoundedRectangle(hot ? accent : Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), 1), handle, 3, 3);
        }

        // Hour labels.
        for (int hour = 0; hour <= 24; hour += 3)
        {
            double x = X(hour * 60);
            FormattedText label = Text(HourLabel(hour), 11, muted, dpi);
            double lx = Math.Clamp(x - (label.Width / 2), 0, width - label.Width);
            dc.DrawText(label, new Point(lx, TrackTop + TrackHeight + 4));
            dc.DrawLine(new Pen(stroke, 1), new Point(x, TrackTop + TrackHeight), new Point(x, TrackTop + TrackHeight + 3));
        }

        // Sunrise and sunset above the track.
        DrawMarker(dc, Sunrise, "", muted, dpi);
        DrawMarker(dc, Sunset, "", muted, dpi);

        // Now.
        if (Now is { } now)
        {
            double x = X(now);
            var pen = new Pen(text, 2);
            dc.DrawLine(pen, new Point(x, TrackTop - 4), new Point(x, TrackTop + TrackHeight + 2));
            dc.DrawEllipse(text, null, new Point(x, TrackTop - 5), 4, 4);
        }

        // While dragging, the time being set.
        if (_dragging is not null)
        {
            FormattedText time = Text(TimeLabel(_dragMinutes), 12, text, dpi, FontWeights.SemiBold);
            double x = Math.Clamp(X(_dragMinutes) - (time.Width / 2) - 6, 0, width - time.Width - 12);
            var bubble = new Rect(x, 2, time.Width + 12, time.Height + 4);
            dc.DrawRoundedRectangle(Resource("SolidBackgroundFillColorTertiaryBrush", Brushes.Black), new Pen(stroke, 1), bubble, 4, 4);
            dc.DrawText(time, new Point(bubble.X + 6, bubble.Y + 2));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);
        Point p = e.GetPosition(this);
        if (_dragging is not null)
        {
            _dragMinutes = Math.Clamp(Math.Round(Minutes(p.X) / SnapMinutes) * SnapMinutes, 0, MinutesPerDay - SnapMinutes);
            InvalidateVisual();
            return;
        }
        Guid? hover = HandleAt(p);
        if (hover != _hoverHandle)
        {
            _hoverHandle = hover;
            Cursor = hover is null ? Cursors.Arrow : Cursors.SizeWE;
            InvalidateVisual();
        }
        ToolTip = SegmentAt(p) is { } segment ? $"{segment.Label}  {TimeLabel(segment.Start)} – {TimeLabel(segment.End)}" : null;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverHandle is not null && _dragging is null)
        {
            _hoverHandle = null;
            Cursor = Cursors.Arrow;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonDown(e);
        Point p = e.GetPosition(this);
        if (HandleAt(p) is { } handle && Segments?.FirstOrDefault(s => s.SlotId == handle) is { } segment)
        {
            _dragging = handle;
            _dragMinutes = Shown(segment);
            SelectedSlot = handle;
            CaptureMouse();
            e.Handled = true;
        }
        else if (SegmentAt(p)?.SlotId is { } slot)
        {
            SelectedSlot = slot;
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging is not { } slot)
            return;
        _dragging = null;
        ReleaseMouseCapture();
        InvalidateVisual();
        SlotMoved?.Invoke(this, new TimelineDragEventArgs(slot, _dragMinutes));
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _dragging = null;
        InvalidateVisual();
    }

    private double Shown(TimelineSegment segment) => segment.SlotId is not null && segment.SlotId == _dragging ? _dragMinutes : segment.Start;

    private double X(double minutes) => minutes / MinutesPerDay * ActualWidth;

    private double Minutes(double x) => x / Math.Max(ActualWidth, 1) * MinutesPerDay;

    private Guid? HandleAt(Point p)
    {
        if (p.Y < TrackTop - 4 || p.Y > TrackTop + TrackHeight + 4 || Segments is null)
            return null;
        return Segments.Where(s => s.SlotId is not null && Math.Abs(X(s.Start) - p.X) <= 7)
            .OrderBy(s => Math.Abs(X(s.Start) - p.X))
            .Select(s => s.SlotId)
            .FirstOrDefault();
    }

    private TimelineSegment? SegmentAt(Point p)
    {
        if (p.Y < TrackTop || p.Y > TrackTop + TrackHeight || Segments is null)
            return null;
        double minutes = Minutes(p.X);
        return Segments.LastOrDefault(s => s.Start <= minutes && minutes < s.End);
    }

    private void DrawMarker(DrawingContext dc, double? minutes, string glyph, Brush brush, double dpi)
    {
        if (minutes is not { } m)
            return;
        double x = X(m);
        FormattedText icon = Text(glyph, 14, brush, dpi, family: "Segoe Fluent Icons, Segoe MDL2 Assets");
        FormattedText time = Text(TimeLabel(m), 11, brush, dpi);
        double total = icon.Width + 4 + time.Width;
        double left = Math.Clamp(x - (icon.Width / 2), 0, ActualWidth - total);
        dc.DrawText(icon, new Point(left, 8));
        dc.DrawText(time, new Point(left + icon.Width + 4, 9));
        dc.DrawLine(new Pen(brush, 1) { DashStyle = DashStyles.Dot }, new Point(x, 25), new Point(x, TrackTop + TrackHeight));
    }

    private static Brush Fill(TimelineSegment segment) => segment.Picture is { } picture
        ? new ImageBrush(picture) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center }
        : Brushes.SlateGray;

    private static Rect Inset(Rect r, double by) => new(r.X + by, r.Y + by, Math.Max(0, r.Width - (2 * by)), Math.Max(0, r.Height - (2 * by)));

    private Brush Resource(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;

    private static FormattedText Text(string value, double size, Brush brush, double dpi, FontWeight? weight = null, string family = "Segoe UI Variable Text, Segoe UI") =>
        new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily(family), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, brush, dpi);

    private static string HourLabel(int hour) =>
        hour == 24 ? new TimeOnly(0, 0).ToString("t", CultureInfo.CurrentCulture) : new TimeOnly(hour, 0).ToString("t", CultureInfo.CurrentCulture);

    /// <summary>A time of day in the user's format, e.g. 21:30 or 9:30 PM.</summary>
    public static string TimeLabel(double minutes)
    {
        int m = (int)Math.Round(minutes) % (24 * 60);
        return new TimeOnly(m / 60, m % 60).ToString("t", CultureInfo.CurrentCulture);
    }
}
