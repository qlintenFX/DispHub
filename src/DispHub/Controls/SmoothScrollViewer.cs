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
/// <remarks>
/// The wheel is caught on the way down (PreviewMouseWheel). Text boxes, combo boxes and number boxes inside the page
/// would otherwise swallow wheel events that pass over them and make the page stall mid-scroll. A child only gets
/// the wheel if it has its own scrollable content that can still move in that direction.
/// </remarks>
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

    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewMouseWheel(e);
        if (e.Handled || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || ChildWantsWheel(e))
            return;

        if (ScrollBy(e.Delta))
            e.Handled = true;
    }

    // Wheel events that a child let through (it reached its own edge) still bubble here.
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!e.Handled && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && ScrollBy(e.Delta))
            e.Handled = true;
    }

    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);
        // Something else moved the view (keyboard, scrollbar, BringIntoView, content resize): follow it.
        if (_animating && Math.Abs(VerticalOffset - _expected) > 1)
            Stop();
    }

    /// <summary>Starts or extends the animation. Returns false at the edge so an outer scroller can take over.</summary>
    private bool ScrollBy(int delta)
    {
        if (ScrollableHeight <= 0)
            return false;

        if (!_animating)
            _current = _target = VerticalOffset;

        int lines = SystemParameters.WheelScrollLines;
        double step = lines < 0 ? ViewportHeight : lines * PixelsPerLine;
        double next = Math.Clamp(_target - (delta / (double)Mouse.MouseWheelDeltaForOneLine * step), 0, ScrollableHeight);
        if (Math.Abs(next - _target) < 0.01 && !_animating)
            return false;

        _target = next;
        _tau = !SystemParameters.ClientAreaAnimation ? TimeSpan.Zero
            : Math.Abs(delta) < Mouse.MouseWheelDeltaForOneLine ? ScrollEasing.TouchpadTau
            : ScrollEasing.WheelTau;
        Start();
        return true;
    }

    /// <summary>True when an element under the pointer has its own scrollable area that can move this way.</summary>
    private bool ChildWantsWheel(MouseWheelEventArgs e)
    {
        for (DependencyObject? node = e.OriginalSource as DependencyObject; node is not null && node != this; node = Parent(node))
        {
            if (node is ScrollViewer inner && inner.ScrollableHeight > 0)
            {
                bool canMove = e.Delta > 0 ? inner.VerticalOffset > 0 : inner.VerticalOffset < inner.ScrollableHeight;
                if (canMove)
                    return true;
            }
        }
        return false;

        static DependencyObject? Parent(DependencyObject node) =>
            node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
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
