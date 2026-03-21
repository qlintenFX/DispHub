using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DisplayHub.Helpers;

/// <summary>
/// Attached behavior that adds smooth animated scrolling to any ScrollViewer.
/// Usage: <ScrollViewer helpers:SmoothScrollBehavior.IsEnabled="True"/>
/// </summary>
public static class SmoothScrollBehavior
{
    private const double PixelsPerLine = 16.0;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty AnimatableVerticalOffsetProperty =
        DependencyProperty.RegisterAttached("AnimatableVerticalOffset", typeof(double), typeof(SmoothScrollBehavior),
            new PropertyMetadata(0.0, OnAnimatableVerticalOffsetChanged));

    private static readonly DependencyProperty TargetVerticalOffsetProperty =
        DependencyProperty.RegisterAttached("TargetVerticalOffset", typeof(double), typeof(SmoothScrollBehavior),
            new PropertyMetadata(0.0));

    private static readonly DependencyProperty IsAnimatingProperty =
        DependencyProperty.RegisterAttached("IsAnimating", typeof(bool), typeof(SmoothScrollBehavior),
            new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
        {
            if ((bool)e.NewValue)
            {
                sv.SetValue(TargetVerticalOffsetProperty, sv.VerticalOffset);
                sv.PreviewMouseWheel += OnPreviewMouseWheel;
                sv.ScrollChanged += OnScrollChanged;
            }
            else
            {
                sv.PreviewMouseWheel -= OnPreviewMouseWheel;
                sv.ScrollChanged -= OnScrollChanged;
                sv.BeginAnimation(AnimatableVerticalOffsetProperty, null);
                sv.SetValue(IsAnimatingProperty, false);
            }
        }
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv || Math.Abs(e.VerticalChange) < 0.001)
            return;

        if ((bool)sv.GetValue(IsAnimatingProperty))
            return;

        sv.SetValue(TargetVerticalOffsetProperty, sv.VerticalOffset);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        if (sv.ScrollableHeight <= 0)
            return;

        if (ShouldLetNestedScrollViewerHandle(e.OriginalSource as DependencyObject, sv, e.Delta))
            return;

        double currentTarget = (double)sv.GetValue(TargetVerticalOffsetProperty);

        // If previous animation finished, sync target with actual offset
        if (Math.Abs(currentTarget - sv.VerticalOffset) < 0.5)
            currentTarget = sv.VerticalOffset;

        double scrollAmount = CalculateScrollAmount(sv, e.Delta);
        if (Math.Abs(scrollAmount) < 0.001)
            return;

        double newTarget = Math.Clamp(currentTarget - scrollAmount, 0, sv.ScrollableHeight);
        if (Math.Abs(newTarget - currentTarget) < 0.001)
            return;

        e.Handled = true;
        sv.SetValue(TargetVerticalOffsetProperty, newTarget);

        var animation = new DoubleAnimation
        {
            To = newTarget,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        sv.SetValue(IsAnimatingProperty, true);
        animation.Completed += (_, _) =>
        {
            sv.SetValue(IsAnimatingProperty, false);
            sv.SetValue(TargetVerticalOffsetProperty, sv.VerticalOffset);
        };

        sv.BeginAnimation(AnimatableVerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void OnAnimatableVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
            sv.ScrollToVerticalOffset((double)e.NewValue);
    }

    private static double CalculateScrollAmount(ScrollViewer sv, int delta)
    {
        if (delta == 0)
            return 0;

        int wheelLines = SystemParameters.WheelScrollLines;
        double wheelSteps = delta / (double)Mouse.MouseWheelDeltaForOneLine;

        if (wheelLines == -1)
        {
            double pageAmount = Math.Max(48.0, sv.ViewportHeight * 0.9);
            return wheelSteps * pageAmount;
        }

        if (wheelLines <= 0)
            return 0;

        return wheelSteps * wheelLines * PixelsPerLine;
    }

    private static bool ShouldLetNestedScrollViewerHandle(DependencyObject? source, ScrollViewer owner, int delta)
    {
        DependencyObject? current = source;
        while (current != null && !ReferenceEquals(current, owner))
        {
            if (current is ScrollViewer nested && !ReferenceEquals(nested, owner) && CanScrollInDirection(nested, delta))
                return true;

            current = GetParent(current);
        }

        return false;
    }

    private static bool CanScrollInDirection(ScrollViewer scrollViewer, int delta)
    {
        if (scrollViewer.ScrollableHeight <= 0)
            return false;

        return delta > 0
            ? scrollViewer.VerticalOffset > 0
            : scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
    }

    private static DependencyObject? GetParent(DependencyObject obj)
    {
        if (obj is Visual || obj is System.Windows.Media.Media3D.Visual3D)
            return VisualTreeHelper.GetParent(obj);

        return LogicalTreeHelper.GetParent(obj);
    }
}
