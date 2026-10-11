using DispHub.Core.Color;

namespace DispHub.Tests.Core;

public sealed class BlackbodyTests
{
    [Fact]
    public void Multipliers_At6500K_AreExactlyNeutral() =>
        Assert.Equal((1.0, 1.0, 1.0), Blackbody.Multipliers(6500));

    [Fact]
    public void Multipliers_NearNeutral_AreAlmostNeutral()
    {
        (double r, double g, double b) = Blackbody.Multipliers(6499);

        Assert.InRange(r, 0.99, 1.0);
        Assert.InRange(g, 0.99, 1.0);
        Assert.InRange(b, 0.99, 1.0);
    }

    [Fact]
    public void Multipliers_BlueFallsAsTemperatureFalls()
    {
        double previous = double.MaxValue;
        for (int kelvin = 6500; kelvin >= ColorSettings.TemperatureMinK; kelvin -= 100)
        {
            double blue = Blackbody.Multipliers(kelvin).B;
            Assert.True(blue <= previous + 1e-9, $"blue rose at {kelvin} K");
            previous = blue;
        }
    }

    [Fact]
    public void Multipliers_At3400K_LookWarm()
    {
        (double r, double g, double b) = Blackbody.Multipliers(3400);

        Assert.Equal(1.0, r, 6);
        Assert.InRange(g, 0.7, 0.85);
        Assert.InRange(b, 0.45, 0.65);
    }

    [Theory]
    [InlineData(500, 2000)]
    [InlineData(40000, 10000)]
    public void Multipliers_OutOfRange_AreClamped(int input, int clampedTo) =>
        Assert.Equal(Blackbody.Multipliers(clampedTo), Blackbody.Multipliers(input));
}

public sealed class VibranceMappingTests
{
    private static readonly VibranceRange Legacy = new(Min: 0, Max: 63, Default: 0);
    private static readonly VibranceRange Extended = new(Min: -50, Max: 100, Default: 0);

    [Fact]
    public void ToDriverLevel_FiftyPercent_IsDriverDefault()
    {
        Assert.Equal(0, VibranceMapping.ToDriverLevel(50, Legacy));
        Assert.Equal(0, VibranceMapping.ToDriverLevel(50, Extended));
    }

    [Fact]
    public void ToDriverLevel_Extremes_AreMinAndMax()
    {
        Assert.Equal(-50, VibranceMapping.ToDriverLevel(0, Extended));
        Assert.Equal(100, VibranceMapping.ToDriverLevel(100, Extended));
        Assert.Equal(63, VibranceMapping.ToDriverLevel(100, Legacy));
    }

    [Fact]
    public void ToDriverLevel_LegacyBelowFifty_StaysAtDefault() =>
        Assert.Equal(0, VibranceMapping.ToDriverLevel(10, Legacy));

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(70)]
    [InlineData(100)]
    public void RoundTrip_Extended_StaysWithinOnePercent(int percent)
    {
        int level = VibranceMapping.ToDriverLevel(percent, Extended);

        Assert.InRange(VibranceMapping.ToPercent(level, Extended), percent - 1, percent + 1);
    }

    [Fact]
    public void ToPercent_LegacyDefault_IsFifty() => Assert.Equal(50, VibranceMapping.ToPercent(0, Legacy));
}

public sealed class ColorBlendTests
{
    private static readonly ColorSettings Day = new(Brightness: 0, TemperatureK: 6500);
    private static readonly ColorSettings Night = new(Brightness: -20, TemperatureK: 3000, Vibrance: 40);

    [Fact]
    public void Lerp_Endpoints_ReturnInputs()
    {
        Assert.Equal(Day, ColorBlend.Lerp(Day, Night, 0));
        Assert.Equal(Night, ColorBlend.Lerp(Day, Night, 1));
    }

    [Fact]
    public void Lerp_Midpoint_InterpolatesTemperatureInMireds()
    {
        ColorSettings mid = ColorBlend.Lerp(Day, Night, 0.5);

        // The mired midpoint of 6500 K and 3000 K is about 4105 K, not the linear 4750 K.
        Assert.InRange(mid.TemperatureK, 4090, 4120);
        Assert.Equal(-10, mid.Brightness);
        Assert.Equal(45, mid.Vibrance);
    }
}

public sealed class ColorSettingsTests
{
    [Fact]
    public void Clamp_OutOfRangeValues_AreLimited()
    {
        ColorSettings clamped = new ColorSettings(-200, 200, double.NaN, 100, 500).Clamp();

        Assert.Equal(new ColorSettings(-50, 50, 1.0, 2000, 100), clamped);
    }

    [Fact]
    public void HasNeutralRamp_IgnoresVibrance() => Assert.True(new ColorSettings(Vibrance: 80).HasNeutralRamp);
}
