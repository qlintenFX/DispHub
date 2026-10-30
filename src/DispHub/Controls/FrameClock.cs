// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Controls;

/// <summary>
/// Turns WPF's per-frame <c>RenderingTime</c> into animation time steps.
/// </summary>
/// <remarks>
/// Measured on a 165 Hz laptop: when motion starts from an idle screen, the GPU and compositor take 12-40 ms to
/// deliver the first frames, and about one frame in ten arrives a refresh late while moving. Animating by wall-clock
/// time turns those late frames into visible jumps. One missed refresh is caught up (that keeps the motion in time
/// with the eye); longer gaps advance the animation by at most two refreshes, so it pauses briefly instead of jumping.
/// </remarks>
public sealed class FrameClock
{
    private const int Window = 9;

    private static readonly TimeSpan SameFrame = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MinPeriod = TimeSpan.FromMilliseconds(2);
    private static readonly TimeSpan MaxPeriod = TimeSpan.FromMilliseconds(50);

    private readonly TimeSpan[] _recent = new TimeSpan[Window];
    private readonly TimeSpan[] _sorted = new TimeSpan[Window];
    private int _count;
    private TimeSpan _last;
    private bool _running;

    /// <summary>The display's frame interval, learned from recent frames. Kept across animations.</summary>
    public TimeSpan Period { get; private set; } = TimeSpan.FromSeconds(1.0 / 60);

    /// <summary>Call when an animation starts; the next tick is its first frame.</summary>
    public void Restart() => _running = false;

    /// <summary>Returns how far to advance the animation for the frame WPF is about to render.</summary>
    public TimeSpan Tick(TimeSpan renderingTime)
    {
        if (!_running)
        {
            // The first frame already shows one frame of motion, so a scroll responds immediately.
            _running = true;
            _last = renderingTime;
            return Period;
        }

        TimeSpan delta = renderingTime - _last;
        if (delta < SameFrame)
            return TimeSpan.Zero; // Rendering can fire more than once per frame.
        _last = renderingTime;
        Learn(delta);
        return delta < Period * 2 ? delta : Period * 2;
    }

    private void Learn(TimeSpan delta)
    {
        if (delta < MinPeriod || delta > MaxPeriod)
            return;
        Array.Copy(_recent, 0, _recent, 1, Window - 1);
        _recent[0] = delta;
        _count = Math.Min(_count + 1, Window);
        if (_count < 3)
            return;
        // The median ignores late frames and stalls, and still follows a move to a screen with another refresh rate.
        Array.Copy(_recent, _sorted, _count);
        Array.Sort(_sorted, 0, _count);
        Period = _sorted[_count / 2];
    }
}
