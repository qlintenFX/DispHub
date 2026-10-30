// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DispHub.Controls;

/// <summary>
/// A ScrollViewer with animated wheel scrolling. WPF's own ScrollViewer jumps a fixed 48 px per wheel event and
/// ignores the wheel delta, which stutters on high refresh rate screens and on precision touchpads.
/// This one moves by the real delta and glides there, one step per rendered frame.
/// </summary>
/// <remarks>
/// <para>
/// The motion (<see cref="ScrollMotion"/>) keeps its speed when wheel steps follow each other and ends exactly on
/// target; the frame timing (<see cref="FrameClock"/>) keeps late frames from turning into jumps. Offsets are
/// rounded to whole device pixels so text doesn't shimmer while it moves.
/// </para>
/// <para>
/// The wheel is caught on the way down (PreviewMouseWheel). Text boxes, combo boxes and number boxes inside the page
/// would otherwise swallow wheel events that pass over them and make the page stall mid-scroll. A child only gets
/// the wheel if it has its own scrollable content that can still move in that direction.
/// </para>
/// <para>
/// While something holds the mouse (an open drop-down, a popup, a slider being dragged) the page stays put, or
/// the open list would be left floating where the page used to be.
/// </para>
/// </remarks>
public class SmoothScrollViewer : ScrollViewer
{
    private const double PixelsPerLine = 16;

    /// <summary>How long a notched wheel step glides.</summary>
    private static readonly TimeSpan WheelDuration = TimeSpan.FromMilliseconds(220);

    /// <summary>Precision touchpads send many small steps and add their own momentum; light smoothing only.</summary>
    private static readonly TimeSpan TouchpadDuration = TimeSpan.FromMilliseconds(90);

    /// <summary>The user turned off animations in DispHub's settings.</summary>
    public static bool ReduceMotion { get; set; }

    private readonly ScrollMotion _motion = new();
    private readonly FrameClock _clock = new();
    private double _shown;
    private double _pixelsPerDip = 1;
    private bool _animating;

    public SmoothScrollViewer()
    {
        CanContentScroll = false;
        // The wheel still lands here while the page under the pointer ignores the mouse during a glide.
        Background = Brushes.Transparent;
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
        if (e.Handled || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || SomethingHoldsMouse() || ChildWantsWheel(e))
            return;

        if (ScrollBy(e.Delta))
            e.Handled = true;
    }

    // Wheel events that a child let through (it reached its own edge) still bubble here.
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!e.Handled && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !SomethingHoldsMouse() && ScrollBy(e.Delta))
            e.Handled = true;
    }

    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);
        // Something else moved the view (keyboard, scrollbar, BringIntoView, content resize): follow it.
        if (_animating && Math.Abs(VerticalOffset - _shown) > 1)
            Stop();
    }

    /// <summary>Starts or extends the animation. Returns false at the edge so an outer scroller can take over.</summary>
    private bool ScrollBy(int delta)
    {
        if (ScrollableHeight <= 0)
            return false;

        if (!_animating)
        {
            _motion.JumpTo(VerticalOffset);
            _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        }

        int lines = SystemParameters.WheelScrollLines;
        double step = lines < 0 ? ViewportHeight : lines * PixelsPerLine;
        double target = Math.Clamp(_motion.Target - (delta / (double)Mouse.MouseWheelDeltaForOneLine * step), 0, ScrollableHeight);
        if (Math.Abs(target - _motion.Target) < 0.01)
            return _animating;

        TimeSpan duration = !SystemParameters.ClientAreaAnimation || ReduceMotion ? TimeSpan.Zero
            : Math.Abs(delta) < Mouse.MouseWheelDeltaForOneLine ? TouchpadDuration
            : WheelDuration;
        _motion.AnimateTo(target, duration);
        if (_motion.IsMoving)
            Start();
        else
            Show(_motion.Position);
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

    /// <summary>An open drop-down, or a popup or menu outside the page, has the mouse.</summary>
    private bool SomethingHoldsMouse() => Mouse.Captured switch
    {
        null => false,
        ComboBox box => box.IsDropDownOpen,
        Visual visual => visual != this && !IsAncestorOf(visual),
        _ => true,
    };

    private void Start()
    {
        if (_animating)
            return;
        _animating = true;
        _clock.Restart();
        // As browsers do: what slides under a still pointer doesn't light up, so frames aren't spent on hover effects.
        if (Content is UIElement content)
            content.IsHitTestVisible = false;
        CompositionTarget.Rendering += OnRendering;
    }

    private void Stop()
    {
        if (!_animating)
            return;
        StopRendering();
        _motion.JumpTo(VerticalOffset);
    }

    private void StopRendering()
    {
        _animating = false;
        CompositionTarget.Rendering -= OnRendering;
        if (Content is UIElement content)
            content.IsHitTestVisible = true;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        // A drop-down opened while the page was still gliding: stop here so it opens under its box.
        if (SomethingHoldsMouse())
        {
            Stop();
            return;
        }

        TimeSpan step = _clock.Tick(((RenderingEventArgs)e).RenderingTime);
        if (step == TimeSpan.Zero)
            return;

        if (_motion.Target > ScrollableHeight)
            _motion.AnimateTo(ScrollableHeight, WheelDuration); // The content got shorter.
        _motion.Advance(step);
        Show(_motion.Position);
        if (!_motion.IsMoving)
            StopRendering();
    }

    private void Show(double offset)
    {
        _shown = Math.Round(offset * _pixelsPerDip) / _pixelsPerDip;
        ScrollToVerticalOffset(_shown);
    }
}
