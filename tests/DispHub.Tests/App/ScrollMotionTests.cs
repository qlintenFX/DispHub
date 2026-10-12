using DispHub.Controls;

namespace DispHub.Tests.App;

public sealed class ScrollMotionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromSeconds(1.0 / 165);
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(220);

    [Fact]
    public void FromRest_EndsExactlyOnTargetAfterTheDuration()
    {
        var motion = new ScrollMotion();
        motion.JumpTo(100);

        motion.AnimateTo(148, Duration);
        List<double> positions = Run(motion, Duration - Frame);
        Assert.True(motion.IsMoving);
        motion.Advance(Frame * 2);

        Assert.False(motion.IsMoving);
        Assert.Equal(148, motion.Position);
        Assert.Equal(0, motion.Velocity);
        Assert.All(positions, p => Assert.InRange(p, 100, 148));
    }

    [Fact]
    public void FromRest_StartsFastAndSlowsDown()
    {
        var motion = new ScrollMotion();
        motion.AnimateTo(48, Duration);

        List<double> positions = Run(motion, Duration);
        var steps = positions.Zip(positions.Skip(1), (a, b) => b - a).ToList();

        Assert.True(steps[0] > 0);
        Assert.All(steps.Zip(steps.Skip(1)), pair => Assert.True(pair.Second <= pair.First + 1e-9));
    }

    [Fact]
    public void AnotherStepInTheSameDirection_KeepsTheSpeed()
    {
        var motion = new ScrollMotion();
        motion.AnimateTo(48, Duration);
        Run(motion, TimeSpan.FromMilliseconds(50));
        double before = motion.Velocity;

        motion.AnimateTo(96, Duration);

        Assert.Equal(before, motion.Velocity, 6);
        Run(motion, Duration + Frame);
        Assert.Equal(96, motion.Position);
    }

    [Fact]
    public void FastMotionTowardsANearTarget_ArrivesSoonerWithoutOvershooting()
    {
        var motion = new ScrollMotion();
        motion.AnimateTo(400, Duration);
        Run(motion, TimeSpan.FromMilliseconds(20));
        double speed = motion.Velocity;

        motion.AnimateTo(motion.Position + 5, Duration);
        Assert.Equal(speed, motion.Velocity, 6);
        double target = motion.Target;
        List<double> positions = Run(motion, Duration);

        Assert.All(positions, p => Assert.True(p <= target + 1e-9));
        Assert.Equal(target, motion.Position);
    }

    [Fact]
    public void ChangingDirection_MovesTheNewWayImmediately()
    {
        var motion = new ScrollMotion();
        motion.AnimateTo(200, Duration);
        Run(motion, TimeSpan.FromMilliseconds(60));
        double turnAt = motion.Position;

        motion.AnimateTo(turnAt - 48, Duration);
        motion.Advance(Frame);

        Assert.True(motion.Position < turnAt);
        Assert.True(motion.Velocity < 0);
    }

    [Fact]
    public void ZeroDuration_Jumps()
    {
        var motion = new ScrollMotion();

        motion.AnimateTo(30, TimeSpan.Zero);

        Assert.False(motion.IsMoving);
        Assert.Equal(30, motion.Position);
    }

    private static List<double> Run(ScrollMotion motion, TimeSpan time)
    {
        var positions = new List<double> { motion.Position };
        for (TimeSpan t = TimeSpan.Zero; t < time; t += Frame)
        {
            motion.Advance(Frame);
            positions.Add(motion.Position);
        }
        return positions;
    }
}

public sealed class FrameClockTests
{
    private static readonly TimeSpan Refresh = TimeSpan.FromTicks(60_606); // 165 Hz

    [Fact]
    public void FirstFrame_ShowsOneFrameOfMotion()
    {
        var clock = new FrameClock();

        Assert.Equal(clock.Period, clock.Tick(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void LearnsTheRefreshRate()
    {
        FrameClock clock = Warm(out _);

        Assert.Equal(Refresh, clock.Period);
    }

    [Fact]
    public void RepeatedCallbackForTheSameFrame_DoesNotAdvance()
    {
        FrameClock clock = Warm(out TimeSpan now);

        Assert.Equal(TimeSpan.Zero, clock.Tick(now + TimeSpan.FromTicks(5_000)));
    }

    [Fact]
    public void OneMissedRefresh_IsCaughtUp()
    {
        FrameClock clock = Warm(out TimeSpan now);

        Assert.Equal(Refresh * 2, clock.Tick(now + (Refresh * 2)));
    }

    [Fact]
    public void LongStall_AdvancesAtMostTwoRefreshes()
    {
        FrameClock clock = Warm(out TimeSpan now);

        Assert.Equal(Refresh * 2, clock.Tick(now + TimeSpan.FromMilliseconds(60)));
        Assert.Equal(Refresh, clock.Period);
    }

    [Fact]
    public void Restart_TreatsTheNextTickAsAFirstFrame()
    {
        FrameClock clock = Warm(out TimeSpan now);
        clock.Restart();

        Assert.Equal(Refresh, clock.Tick(now + TimeSpan.FromSeconds(3)));
    }

    private static FrameClock Warm(out TimeSpan now)
    {
        var clock = new FrameClock();
        now = TimeSpan.FromSeconds(1);
        clock.Tick(now);
        for (int i = 0; i < 12; i++)
        {
            now += Refresh;
            clock.Tick(now);
        }
        return clock;
    }
}
