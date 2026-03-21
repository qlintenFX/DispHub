using DisplayHub.Constants;
using DisplayHub.Models;

namespace DisplayHub.Tests;

public class ProfileModelTests
{
    [Fact]
    public void Defaults_AreInitializedFromConstants()
    {
        var profile = new Profile();

        Assert.Equal("New Profile", profile.Name);
        Assert.Equal(AppConstants.GammaDefault, profile.Gamma);
        Assert.Equal(AppConstants.ContrastDefault, profile.Contrast);
        Assert.Equal(AppConstants.VibranceDefault, profile.Vibrance);
        Assert.Equal(AppConstants.ColorTempDefault, profile.ColorTemperature);
    }

    [Theory]
    [InlineData(-1.0, AppConstants.GammaMin)]
    [InlineData(99.0, AppConstants.GammaMax)]
    public void Gamma_IsClamped(double input, double expected)
    {
        var profile = new Profile { Gamma = input };
        Assert.Equal(expected, profile.Gamma);
    }

    [Theory]
    [InlineData(-0.1, AppConstants.ContrastMin)]
    [InlineData(1.5, AppConstants.ContrastMax)]
    public void Contrast_IsClamped(double input, double expected)
    {
        var profile = new Profile { Contrast = input };
        Assert.Equal(expected, profile.Contrast);
    }

    [Theory]
    [InlineData(-10, AppConstants.VibranceMin)]
    [InlineData(1000, AppConstants.VibranceMax)]
    public void Vibrance_IsClamped(int input, int expected)
    {
        var profile = new Profile { Vibrance = input };
        Assert.Equal(expected, profile.Vibrance);
    }

    [Theory]
    [InlineData(-10, AppConstants.ColorTempMin)]
    [InlineData(1000, AppConstants.ColorTempMax)]
    public void ColorTemperature_IsClamped(int input, int expected)
    {
        var profile = new Profile { ColorTemperature = input };
        Assert.Equal(expected, profile.ColorTemperature);
    }
}
