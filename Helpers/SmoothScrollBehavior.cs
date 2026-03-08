using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace DisplayHub.Helpers;

/// <summary>
/// Attached behavior that adds smooth animated scrolling to any ScrollViewer.
/// Usage: <ScrollViewer helpers:SmoothScrollBehavior.IsEnabled="True"/>
/// </summary>
public static class SmoothScrollBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty AnimatableVerticalOffsetProperty =
        DependencyProperty.RegisterAttached("AnimatableVerticalOffset", typeof(double), typeof(SmoothScrollBehavior),
            new PropertyMetadata(0.0, OnAnimatableVerticalOffsetChanged));

    private static readonly DependencyProperty TargetVerticalOffsetProperty =
        DependencyProperty.RegisterAttached("TargetVerticalOffset", typeof(double), typeof(SmoothScrollBehavior),
            new PropertyMetadata(0.0));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
        {
            if ((bool)e.NewValue)
            {
                sv.PreviewMouseWheel += OnPreviewMouseWheel;
                sv.ScrollChanged += OnScrollChanged;
            }
            else
            {
                sv.PreviewMouseWheel -= OnPreviewMouseWheel;
                sv.ScrollChanged -= OnScrollChanged;
            }
        }
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Keep target in sync when user drags scrollbar
        if (sender is ScrollViewer sv && Math.Abs(e.VerticalChange) > 0)
        {
            var currentTarget = (double)sv.GetValue(TargetVerticalOffsetProperty);
            if (Math.Abs(currentTarget - sv.VerticalOffset) > 50)
                sv.SetValue(TargetVerticalOffsetProperty, sv.VerticalOffset);
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        e.Handled = true;

        double currentTarget = (double)sv.GetValue(TargetVerticalOffsetProperty);

        // If previous animation finished, sync target with actual offset
        if (Math.Abs(currentTarget - sv.VerticalOffset) < 1)
            currentTarget = sv.VerticalOffset;

        // Scroll amount: 3 lines worth (~48px per notch, smooth feel)
        double scrollAmount = e.Delta * 0.6;
        double newTarget = Math.Clamp(currentTarget - scrollAmount, 0, sv.ScrollableHeight);
        sv.SetValue(TargetVerticalOffsetProperty, newTarget);

        var animation = new DoubleAnimation
        {
            To = newTarget,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        sv.BeginAnimation(AnimatableVerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void OnAnimatableVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
            sv.ScrollToVerticalOffset((double)e.NewValue);
    }
}
