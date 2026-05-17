using DispHub.Constants;
using DispHub.Models;

namespace DispHub.Tests;

public class ProfileExtendedTests
{
    [Fact]
    public void NamedConstructor_SetsAllProperties()
    {
        var profile = new Profile("Gaming", 1.2, 0.6, 75, 30);

        Assert.Equal("Gaming", profile.Name);
        Assert.Equal(1.2, profile.Gamma);
        Assert.Equal(0.6, profile.Contrast);
        Assert.Equal(75, profile.Vibrance);
        Assert.Equal(30, profile.ColorTemperature);
    }

    [Fact]
    public void NamedConstructor_DefaultParameters()
    {
        var profile = new Profile("Simple");

        Assert.Equal("Simple", profile.Name);
        Assert.Equal(AppConstants.GammaDefault, profile.Gamma);
        Assert.Equal(AppConstants.ContrastDefault, profile.Contrast);
        Assert.Equal(AppConstants.VibranceDefault, profile.Vibrance);
        Assert.Equal(AppConstants.ColorTempDefault, profile.ColorTemperature);
    }

    [Fact]
    public void Name_RejectsNullOrWhitespace()
    {
        var profile = new Profile { Name = null! };
        Assert.Equal("New Profile", profile.Name);

        profile.Name = "";
        Assert.Equal("New Profile", profile.Name);

        profile.Name = "   ";
        Assert.Equal("New Profile", profile.Name);
    }

    [Fact]
    public void Name_AcceptsValidString()
    {
        var profile = new Profile { Name = "My Custom Profile" };
        Assert.Equal("My Custom Profile", profile.Name);
    }

    [Theory]
    [InlineData(0.30)]
    [InlineData(1.0)]
    [InlineData(2.80)]
    public void Gamma_AcceptsValidValues(double value)
    {
        var profile = new Profile { Gamma = value };
        Assert.Equal(value, profile.Gamma);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Contrast_AcceptsValidValues(double value)
    {
        var profile = new Profile { Contrast = value };
        Assert.Equal(value, profile.Contrast);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Vibrance_AcceptsValidValues(int value)
    {
        var profile = new Profile { Vibrance = value };
        Assert.Equal(value, profile.Vibrance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void ColorTemperature_AcceptsValidValues(int value)
    {
        var profile = new Profile { ColorTemperature = value };
        Assert.Equal(value, profile.ColorTemperature);
    }

    [Fact]
    public void HotkeyId_DefaultsToNegativeOne()
    {
        var profile = new Profile();
        Assert.Equal(-1, profile.HotkeyId);
    }

    [Fact]
    public void HotKeyValue_DefaultsToZero()
    {
        var profile = new Profile();
        Assert.Equal(0, profile.HotKeyValue);
    }

    [Fact]
    public void HotKeyModifierValue_DefaultsToZero()
    {
        var profile = new Profile();
        Assert.Equal(0u, profile.HotKeyModifierValue);
    }

    [Fact]
    public void HotkeyDisplayText_ReturnsNoneWhenNoHotkey()
    {
        var profile = new Profile { HotKeyValue = 0 };
        Assert.Equal("None", profile.HotkeyDisplayText);
    }

    [Fact]
    public void HotkeyDisplayText_IncludesModifiers()
    {
        var profile = new Profile
        {
            HotKeyValue = 0x41, // 'A' key
            HotKeyModifierValue = AppConstants.MOD_CONTROL | AppConstants.MOD_SHIFT
        };

        string display = profile.HotkeyDisplayText;
        Assert.Contains("Ctrl", display, StringComparison.Ordinal);
        Assert.Contains("Shift", display, StringComparison.Ordinal);
    }

    [Fact]
    public void HotkeyDisplayText_IncludesAltModifier()
    {
        var profile = new Profile
        {
            HotKeyValue = 0x42,
            HotKeyModifierValue = AppConstants.MOD_ALT
        };

        Assert.Contains("Alt", profile.HotkeyDisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_JsonSerialization_RoundTrips()
    {
        var original = new Profile("Test Profile", 1.5, 0.7, 80, 40)
        {
            HotKeyValue = 0x41,
            HotKeyModifierValue = AppConstants.MOD_CONTROL
        };

        string json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<Profile>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Test Profile", deserialized!.Name);
        Assert.Equal(1.5, deserialized.Gamma);
        Assert.Equal(0.7, deserialized.Contrast);
        Assert.Equal(80, deserialized.Vibrance);
        Assert.Equal(40, deserialized.ColorTemperature);
        Assert.Equal(0x41, deserialized.HotKeyValue);
        Assert.Equal(AppConstants.MOD_CONTROL, deserialized.HotKeyModifierValue);
    }

    [Fact]
    public void Profile_JsonSerialization_ExcludesHotkeyId()
    {
        var profile = new Profile { HotkeyId = 42 };
        string json = System.Text.Json.JsonSerializer.Serialize(profile);

        // HotkeyId has [JsonIgnore], so it should not appear in JSON
        Assert.DoesNotContain("HotkeyId", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Profile_JsonSerialization_ExcludesHotkeyDisplayText()
    {
        var profile = new Profile { HotKeyValue = 0x41 };
        string json = System.Text.Json.JsonSerializer.Serialize(profile);

        // HotkeyDisplayText has [JsonIgnore], so it should not appear
        Assert.DoesNotContain("HotkeyDisplayText", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-100.0, AppConstants.GammaMin)]
    [InlineData(0.0, AppConstants.GammaMin)]
    [InlineData(100.0, AppConstants.GammaMax)]
    public void Gamma_ClampsExtremeValues(double input, double expected)
    {
        var profile = new Profile { Gamma = input };
        Assert.Equal(expected, profile.Gamma);
    }

    [Theory]
    [InlineData(-100.0, AppConstants.ContrastMin)]
    [InlineData(100.0, AppConstants.ContrastMax)]
    public void Contrast_ClampsExtremeValues(double input, double expected)
    {
        var profile = new Profile { Contrast = input };
        Assert.Equal(expected, profile.Contrast);
    }

    [Theory]
    [InlineData(-1000, AppConstants.VibranceMin)]
    [InlineData(10000, AppConstants.VibranceMax)]
    public void Vibrance_ClampsExtremeValues(int input, int expected)
    {
        var profile = new Profile { Vibrance = input };
        Assert.Equal(expected, profile.Vibrance);
    }

    [Theory]
    [InlineData(-1000, AppConstants.ColorTempMin)]
    [InlineData(10000, AppConstants.ColorTempMax)]
    public void ColorTemperature_ClampsExtremeValues(int input, int expected)
    {
        var profile = new Profile { ColorTemperature = input };
        Assert.Equal(expected, profile.ColorTemperature);
    }
}
