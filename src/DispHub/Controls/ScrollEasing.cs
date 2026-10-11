namespace DispHub.Controls;

/// <summary>
/// Frame-rate independent exponential easing: the remaining distance shrinks by e^(-dt/tau) per step, so the
/// motion looks the same at 60 Hz and 165 Hz and never overshoots.
/// </summary>
public static class ScrollEasing
{
    /// <summary>Time constant for notched mouse wheels: about 90 % of a scroll lands within 0.2 s.</summary>
    public static readonly TimeSpan WheelTau = TimeSpan.FromMilliseconds(85);

    /// <summary>Precision touchpads send many small, unevenly timed deltas; light smoothing hides the unevenness while still feeling attached to the fingers.</summary>
    public static readonly TimeSpan TouchpadTau = TimeSpan.FromMilliseconds(55);

    /// <summary>Remaining distance below which the animation snaps to the target and stops.</summary>
    public const double SnapDistance = 0.5;

    public static double Step(double current, double target, TimeSpan elapsed, TimeSpan tau)
    {
        if (tau <= TimeSpan.Zero || elapsed <= TimeSpan.Zero)
            return elapsed <= TimeSpan.Zero ? current : target;
        double remaining = (current - target) * Math.Exp(-elapsed.TotalSeconds / tau.TotalSeconds);
        return Math.Abs(remaining) < SnapDistance ? target : target + remaining;
    }
}
