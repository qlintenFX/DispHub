// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DispHub.Controls;

/// <summary>
/// Two pictures on top of each other with a divider to drag: <see cref="Before"/> on the left of it,
/// <see cref="After"/> on the right. Keeps the pictures' shape; arrow keys move the divider too.
/// </summary>
public sealed class CompareView : FrameworkElement
{
    public static readonly DependencyProperty BeforeProperty = DependencyProperty.Register(
        nameof(Before), typeof(ImageSource), typeof(CompareView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty AfterProperty = DependencyProperty.Register(
        nameof(After), typeof(ImageSource), typeof(CompareView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position), typeof(double), typeof(CompareView),
        new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, (_, v) => Math.Clamp((double)v, 0, 1)));

    public static readonly DependencyProperty BeforeLabelProperty = DependencyProperty.Register(
        nameof(BeforeLabel), typeof(string), typeof(CompareView), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AfterLabelProperty = DependencyProperty.Register(
        nameof(AfterLabel), typeof(string), typeof(CompareView), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    private const double CornerRadius = 8;
    private const double HandleRadius = 15;
    private static readonly Brush LabelBackground = Freeze(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)));
    private static readonly Brush HandleBrush = Freeze(new SolidColorBrush(Color.FromRgb(250, 250, 250)));
    private static readonly Pen DividerPen = Freeze(new Pen(HandleBrush, 2));
    private static readonly Pen ShadowPen = Freeze(new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), 4));
    private static readonly Pen ChevronPen = Freeze(new Pen(new SolidColorBrush(Color.FromRgb(60, 60, 60)), 1.6)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round,
    });

    public CompareView()
    {
        Focusable = true;
        Cursor = Cursors.SizeWE;
        FocusVisualStyle = null;
    }

    public ImageSource? Before
    {
        get => (ImageSource?)GetValue(BeforeProperty);
        set => SetValue(BeforeProperty, value);
    }

    public ImageSource? After
    {
        get => (ImageSource?)GetValue(AfterProperty);
        set => SetValue(AfterProperty, value);
    }

    /// <summary>Where the divider is, from 0 (all after) to 1 (all before).</summary>
    public double Position
    {
        get => (double)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public string BeforeLabel
    {
        get => (string)GetValue(BeforeLabelProperty);
        set => SetValue(BeforeLabelProperty, value);
    }

    public string AfterLabel
    {
        get => (string)GetValue(AfterLabelProperty);
        set => SetValue(AfterLabelProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        ImageSource? picture = After ?? Before;
        double aspect = picture is { Width: > 0 } ? picture.Height / picture.Width : 10.0 / 16;
        double width = double.IsInfinity(availableSize.Width) ? picture?.Width ?? 480 : availableSize.Width;
        return new Size(width, width * aspect);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var bounds = new Rect(RenderSize);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        drawingContext.PushClip(new RectangleGeometry(bounds, CornerRadius, CornerRadius));
        if (After is { } after)
            drawingContext.DrawImage(after, bounds);
        double x = Math.Round(bounds.Width * Position);
        if (Before is { } before && x > 0)
        {
            drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, x, bounds.Height)));
            drawingContext.DrawImage(before, bounds);
            drawingContext.Pop();
        }

        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        DrawLabel(drawingContext, BeforeLabel, new Point(10, 10), alignRight: false, dpi, visible: x > 60);
        DrawLabel(drawingContext, AfterLabel, new Point(bounds.Width - 10, 10), alignRight: true, dpi, visible: bounds.Width - x > 60);

        // The divider and its handle.
        drawingContext.DrawLine(ShadowPen, new Point(x, 0), new Point(x, bounds.Height));
        drawingContext.DrawLine(DividerPen, new Point(x, 0), new Point(x, bounds.Height));
        var centre = new Point(x, bounds.Height / 2);
        drawingContext.DrawEllipse(HandleBrush, ShadowPen, centre, HandleRadius, HandleRadius);
        drawingContext.DrawEllipse(HandleBrush, null, centre, HandleRadius, HandleRadius);
        DrawChevron(drawingContext, centre with { X = centre.X - 5 }, -1);
        DrawChevron(drawingContext, centre with { X = centre.X + 5 }, 1);
        drawingContext.Pop();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseLeftButtonDown(e);
        Focus();
        CaptureMouse();
        MoveTo(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnMouseMove(e);
        if (IsMouseCaptured)
            MoveTo(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        ReleaseMouseCapture();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        if (e.Key is Key.Left or Key.Right)
        {
            Position += e.Key == Key.Left ? -0.05 : 0.05;
            e.Handled = true;
        }
    }

    private void MoveTo(Point point)
    {
        if (ActualWidth > 0)
            Position = point.X / ActualWidth;
    }

    private static void DrawLabel(DrawingContext dc, string text, Point anchor, bool alignRight, double pixelsPerDip, bool visible)
    {
        if (!visible || string.IsNullOrEmpty(text))
            return;
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), 11, Brushes.White, pixelsPerDip);
        var box = new Rect(alignRight ? anchor.X - formatted.Width - 16 : anchor.X, anchor.Y, formatted.Width + 16, formatted.Height + 6);
        dc.DrawRoundedRectangle(LabelBackground, null, box, box.Height / 2, box.Height / 2);
        dc.DrawText(formatted, new Point(box.X + 8, box.Y + 3));
    }

    private static void DrawChevron(DrawingContext dc, Point tip, int direction)
    {
        dc.DrawLine(ChevronPen, new Point(tip.X - (3 * direction), tip.Y - 4), tip);
        dc.DrawLine(ChevronPen, new Point(tip.X - (3 * direction), tip.Y + 4), tip);
    }

    private static T Freeze<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
