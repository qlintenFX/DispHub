using DispHub.Constants;
using DispHub.Helpers;

namespace DispHub.Tests;

public class ColorTemperatureMapperTests
{
    [Fact]
    public void SliderMidpoint_MapsToNeutralKelvin()
    {
        double kelvin = ColorTemperatureMapper.GetKelvinFromSlider(AppConstants.ColorTempNeutralValue);
        Assert.Equal(AppConstants.ColorTempNeutralKelvin, kelvin);
    }

    [Fact]
    public void SliderExtremes_MapToConfiguredKelvinBounds()
    {
        double minKelvin = ColorTemperatureMapper.GetKelvinFromSlider(AppConstants.ColorTempMin);
        double maxKelvin = ColorTemperatureMapper.GetKelvinFromSlider(AppConstants.ColorTempMax);

        Assert.Equal(AppConstants.ColorTempMinKelvin, minKelvin);
        Assert.Equal(AppConstants.ColorTempMaxKelvin, maxKelvin);
    }

    [Fact]
    public void NeutralMultipliers_AreUnityAcrossChannels()
    {
        var (r, g, b) = ColorTemperatureMapper.GetMultipliers(AppConstants.ColorTempNeutralValue);

        Assert.Equal(1.0, r, 6);
        Assert.Equal(1.0, g, 6);
        Assert.Equal(1.0, b, 6);
    }

    [Fact]
    public void WarmAndCoolMultipliers_ShiftExpectedChannels()
    {
        var (warmR, _, warmB) = ColorTemperatureMapper.GetMultipliers(AppConstants.ColorTempMin);
        var (coolR, _, coolB) = ColorTemperatureMapper.GetMultipliers(AppConstants.ColorTempMax);

        Assert.True(warmR >= warmB, "Warmest setting should bias red over blue.");
        Assert.True(coolB >= coolR, "Coolest setting should bias blue over red.");
    }
}
