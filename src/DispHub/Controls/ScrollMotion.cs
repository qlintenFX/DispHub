// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Controls;

/// <summary>
/// The scroll position over time. Each wheel step starts a new segment from the current position and speed to the
/// new target, so the speed never jumps between steps, and every segment ends exactly on its target after a fixed
/// time instead of creeping the last pixels.
/// </summary>
/// <remarks>
/// A segment is a cubic from (position, speed) to (target, 0). Its shape is set by the start slope <c>a</c>, the start
/// speed relative to an even move over the segment: 3 is a plain ease-out (fast start, gentle stop), lower values
/// first speed up. Slopes above 3 would overshoot the target, so a faster current speed shortens the segment instead.
/// </remarks>
public sealed class ScrollMotion
{
    /// <summary>Plain ease-out; also the most a segment can start with without overshooting.</summary>
    public const double FreshSlope = 3;

    /// <summary>A step that arrives while the previous one has almost stopped still starts at least this briskly.</summary>
    public const double MinContinuedSlope = 1;

    private double _from;
    private double _distance;
    private double _slope;
    private double _duration;
    private double _time;

    public double Position { get; private set; }

    public double Target => _from + _distance;

    public bool IsMoving => _time < _duration;

    /// <summary>Speed in units per second.</summary>
    public double Velocity
    {
        get
        {
            if (!IsMoving)
                return 0;
            double s = _time / _duration;
            return _distance / _duration * (_slope + (2 * (3 - (2 * _slope)) * s) + (3 * (_slope - 2) * s * s));
        }
    }

    public void JumpTo(double position)
    {
        _from = Position = position;
        _distance = _slope = _duration = _time = 0;
    }

    public void AnimateTo(double target, TimeSpan duration)
    {
        double distance = target - Position;
        double seconds = duration.TotalSeconds;
        if (seconds <= 0 || Math.Abs(distance) < 1e-6)
        {
            JumpTo(target);
            return;
        }

        double velocity = Velocity;
        double slope = velocity * seconds / distance;
        if (slope > FreshSlope)
        {
            // Already moving faster than an ease-out would start: keep the speed and arrive sooner.
            seconds = FreshSlope * distance / velocity;
            slope = FreshSlope;
        }
        else if (slope <= 0)
        {
            slope = FreshSlope; // At rest, or the direction changed.
        }
        else
        {
            slope = Math.Max(slope, MinContinuedSlope);
        }

        _from = Position;
        _distance = distance;
        _slope = slope;
        _duration = seconds;
        _time = 0;
    }

    public void Advance(TimeSpan elapsed)
    {
        if (!IsMoving || elapsed <= TimeSpan.Zero)
            return;
        _time = Math.Min(_time + elapsed.TotalSeconds, _duration);
        if (_time >= _duration)
        {
            Position = Target;
            return;
        }
        double s = _time / _duration;
        Position = _from + (_distance * s * (_slope + ((3 - (2 * _slope)) * s) + ((_slope - 2) * s * s)));
    }
}
