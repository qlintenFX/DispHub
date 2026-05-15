using DispHub.Constants;
using DispHub.Helpers;

namespace DispHub.Tests;

public class ColorTemperatureMapperExtendedTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(90)]
    [InlineData(100)]
    public void GetMultipliers_AllValuesInRange_ProduceValidRgb(int temperature)
    {
        var (r, g, b) = ColorTemperatureMapper.GetMultipliers(temperature);

        Assert.InRange(r, 0.0, 1.0);
        Assert.InRange(g, 0.0, 1.0);
        Assert.InRange(b, 0.0, 1.0);
    }

    [Theory]
    [InlineData(-50)]
    [InlineData(-1)]
    public void GetMultipliers_BelowMin_ClampedToMin(int temperature)
    {
        var (r, g, b) = ColorTemperatureMapper.GetMultipliers(temperature);
        var (rMin, gMin, bMin) = ColorTemperatureMapper.GetMultipliers(AppConstants.ColorTempMin);

        Assert.Equal(rMin, r, 10);
        Assert.Equal(gMin, g, 10);
        Assert.Equal(bMin, b, 10);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(500)]
    public void GetMultipliers_AboveMax_ClampedToMax(int temperature)
    {
        var (r, g, b) = ColorTemperatureMapper.GetMultipliers(temperature);
        var (rMax, gMax, bMax) = ColorTemperatureMapper.GetMultipliers(AppConstants.ColorTempMax);

        Assert.Equal(rMax, r, 10);
        Assert.Equal(gMax, g, 10);
        Assert.Equal(bMax, b, 10);
    }

    [Fact]
    public void GetMultipliers_AtNeutral_AllChannelsAreOne()
    {
        var (r, g, b) = ColorTemperatureMapper.GetMultipliers(AppConstants.ColorTempNeutralValue);

        Assert.Equal(1.0, r, 6);
        Assert.Equal(1.0, g, 6);
        Assert.Equal(1.0, b, 6);
    }

    [Fact]
    public void GetMultipliers_AtLeastOneChannelIsMax()
    {
        // After normalization, at least one channel should be 1.0
        for (int t = 0; t <= 100; t += 10)
        {
            var (r, g, b) = ColorTemperatureMapper.GetMultipliers(t);
            double max = Math.Max(r, Math.Max(g, b));
            Assert.Equal(1.0, max, 4);
        }
    }

    [Theory]
    [InlineData(0, AppConstants.ColorTempMinKelvin)]
    [InlineData(50, AppConstants.ColorTempNeutralKelvin)]
    [InlineData(100, AppConstants.ColorTempMaxKelvin)]
    public void GetKelvinFromSlider_KeyPoints(int slider, double expectedKelvin)
    {
        double kelvin = ColorTemperatureMapper.GetKelvinFromSlider(slider);
        Assert.Equal(expectedKelvin, kelvin, 1);
    }

    [Fact]
    public void GetKelvinFromSlider_IsMonotonicallyIncreasing()
    {
        double previousKelvin = 0;
        for (int t = 0; t <= 100; t++)
        {
            double kelvin = ColorTemperatureMapper.GetKelvinFromSlider(t);
            Assert.True(kelvin >= previousKelvin,
                $"Kelvin at slider={t} ({kelvin}) should be >= previous ({previousKelvin})");
            previousKelvin = kelvin;
        }
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(-1)]
    public void GetKelvinFromSlider_BelowMin_ClampedToMinKelvin(int slider)
    {
        double kelvin = ColorTemperatureMapper.GetKelvinFromSlider(slider);
        Assert.Equal(AppConstants.ColorTempMinKelvin, kelvin, 1);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(500)]
    public void GetKelvinFromSlider_AboveMax_ClampedToMaxKelvin(int slider)
    {
        double kelvin = ColorTemperatureMapper.GetKelvinFromSlider(slider);
        Assert.Equal(AppConstants.ColorTempMaxKelvin, kelvin, 1);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(75)]
    public void GetKelvinFromSlider_Midpoints_AreInterpolated(int slider)
    {
        double kelvin = ColorTemperatureMapper.GetKelvinFromSlider(slider);
        Assert.True(kelvin > AppConstants.ColorTempMinKelvin);
        Assert.True(kelvin < AppConstants.ColorTempMaxKelvin);
    }

    [Fact]
    public void WarmTemperatures_BiasRedChannel()
    {
        // At warm temperatures (low slider), red should be stronger than blue
        for (int t = 0; t <= 20; t += 5)
        {
            var (r, _, b) = ColorTemperatureMapper.GetMultipliers(t);
            Assert.True(r >= b, $"At warm temp {t}, red ({r}) should be >= blue ({b})");
        }
    }

    [Fact]
    public void CoolTemperatures_BiasBlueChannel()
    {
        // At cool temperatures (high slider), blue should be stronger than red
        for (int t = 80; t <= 100; t += 5)
        {
            var (r, _, b) = ColorTemperatureMapper.GetMultipliers(t);
            Assert.True(b >= r, $"At cool temp {t}, blue ({b}) should be >= red ({r})");
        }
    }

    [Fact]
    public void GetMultipliers_GreenChannel_IsAlwaysPositive()
    {
        for (int t = 0; t <= 100; t++)
        {
            var (_, g, _) = ColorTemperatureMapper.GetMultipliers(t);
            Assert.True(g > 0, $"Green channel at temp {t} should be > 0");
        }
    }
}
