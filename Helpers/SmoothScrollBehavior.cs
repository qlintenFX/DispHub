using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DisplayHub.Helpers;

/// <summary>
/// Smooth scrolling using lerp interpolation - responsive Windows-like feel.
/// </summary>
public static class SmoothScrollBehavior
{
    private const double SmoothFactor = 0.25;
    private const double PixelsPerLine = 40.0;

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached("State", typeof(ScrollState), typeof(SmoothScrollBehavior));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv) return;

        if ((bool)e.NewValue)
        {
            var state = new ScrollState(sv);
            sv.SetValue(StateProperty, state);
            sv.PreviewMouseWheel += OnWheel;
            sv.Unloaded += OnUnloaded;
        }
        else
        {
            Detach(sv);
        }
    }

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        var state = sv.GetValue(StateProperty) as ScrollState;
        if (state == null || sv.ScrollableHeight <= 0) return;

        if (HasNestedScrollable(e.OriginalSource as DependencyObject, sv, e.Delta))
            return;

        double lines = SystemParameters.WheelScrollLines;
        if (lines <= 0) lines = 3;
        double amount = -(e.Delta / 120.0) * lines * PixelsPerLine;

        state.TargetOffset = Math.Clamp(state.TargetOffset + amount, 0, sv.ScrollableHeight);
        state.Start();
        e.Handled = true;
    }

    private static bool HasNestedScrollable(DependencyObject? source, ScrollViewer owner, int delta)
    {
        var current = source;
        while (current != null && current != owner)
        {
            if (current is ScrollViewer nested && nested != owner && nested.ScrollableHeight > 0)
            {
                bool canScroll = delta > 0 ? nested.VerticalOffset > 0 : nested.VerticalOffset < nested.ScrollableHeight;
                if (canScroll) return true;
            }
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer sv)
            (sv.GetValue(StateProperty) as ScrollState)?.Stop();
    }

    private static void Detach(ScrollViewer sv)
    {
        sv.PreviewMouseWheel -= OnWheel;
        sv.Unloaded -= OnUnloaded;
        (sv.GetValue(StateProperty) as ScrollState)?.Stop();
        sv.ClearValue(StateProperty);
    }

    private sealed class ScrollState
    {
        private readonly ScrollViewer _sv;
        private bool _running;

        public ScrollState(ScrollViewer sv)
        {
            _sv = sv;
            TargetOffset = sv.VerticalOffset;
        }

        public double TargetOffset { get; set; }

        public void Start()
        {
            if (_running) return;
            _running = true;
            CompositionTarget.Rendering += OnRender;
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            CompositionTarget.Rendering -= OnRender;
        }

        private void OnRender(object? sender, EventArgs e)
        {
            double current = _sv.VerticalOffset;
            double diff = TargetOffset - current;

            if (Math.Abs(diff) < 0.5)
            {
                _sv.ScrollToVerticalOffset(TargetOffset);
                Stop();
                return;
            }

            _sv.ScrollToVerticalOffset(current + diff * SmoothFactor);
        }
    }
}
