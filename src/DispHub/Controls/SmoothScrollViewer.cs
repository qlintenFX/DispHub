using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DispHub.Controls;

/// <summary>
/// A ScrollViewer with animated wheel scrolling. WPF's own ScrollViewer jumps a fixed 48 px per wheel event and
/// ignores the wheel delta, which stutters on high refresh rate screens and on precision touchpads.
/// This one moves by the real delta and eases there on every rendered frame.
/// </summary>
public class SmoothScrollViewer : ScrollViewer
{
    private const double PixelsPerLine = 16;

    private double _current;
    private double _target;
    private double _expected;
    private TimeSpan _tau = ScrollEasing.WheelTau;
    private TimeSpan _lastFrame;
    private bool _animating;

    public SmoothScrollViewer()
    {
        CanContentScroll = false;
        PanningMode = PanningMode.VerticalFirst;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => Stop()), handledEventsToo: true);
        Unloaded += (_, _) => Stop();
    }

    /// <summary>Jumps to the top without animation, e.g. after navigating to another page.</summary>
    public void ResetToTop()
    {
        Stop();
        ScrollToVerticalOffset(0);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Handled || ScrollableHeight <= 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return;

        if (!_animating)
            _current = _target = VerticalOffset;

        int lines = SystemParameters.WheelScrollLines;
        double step = lines < 0 ? ViewportHeight : lines * PixelsPerLine;
        double next = Math.Clamp(_target - (e.Delta / (double)Mouse.MouseWheelDeltaForOneLine * step), 0, ScrollableHeight);

        // At the edge: let the event bubble so an outer scroll viewer can take over.
        if (Math.Abs(next - _target) < 0.01 && !_animating)
            return;

        e.Handled = true;
        _target = next;
        _tau = !SystemParameters.ClientAreaAnimation ? TimeSpan.Zero
            : Math.Abs(e.Delta) < Mouse.MouseWheelDeltaForOneLine ? ScrollEasing.TouchpadTau
            : ScrollEasing.WheelTau;
        Start();
    }

    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);
        // Something else moved the view (keyboard, scrollbar, BringIntoView, content resize): follow it.
        if (_animating && Math.Abs(VerticalOffset - _expected) > 1)
            Stop();
    }

    private void Start()
    {
        if (_animating)
            return;
        _animating = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    private void Stop()
    {
        if (!_animating)
            return;
        _animating = false;
        CompositionTarget.Rendering -= OnRendering;
        _current = _target = VerticalOffset;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        TimeSpan now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _lastFrame)
            return; // Rendering can fire more than once per frame.

        TimeSpan elapsed = _lastFrame == TimeSpan.Zero
            ? TimeSpan.FromSeconds(1.0 / 60)
            : TimeSpan.FromTicks(Math.Min((now - _lastFrame).Ticks, TimeSpan.FromMilliseconds(50).Ticks));
        _lastFrame = now;

        _target = Math.Min(_target, ScrollableHeight);
        _current = ScrollEasing.Step(_current, _target, elapsed, _tau);
        _expected = _current;
        ScrollToVerticalOffset(_current);

        if (_current == _target)
        {
            _animating = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }
}
