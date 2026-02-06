using System.Text.Json;
using System.Windows.Forms;
using FluentAssertions;
using KeyedColors.Constants;
using KeyedColors.Models;
using Xunit;

namespace KeyedColors.Tests.Models;

public class ProfileTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var profile = new Profile();

        profile.Name.Should().Be("New Profile");
        profile.Gamma.Should().Be(AppConstants.GammaDefault);
        profile.Contrast.Should().Be(AppConstants.ContrastDefault);
        profile.Vibrance.Should().Be(AppConstants.VibranceDefault);
        profile.HotKey.Should().Be(Keys.None);
        profile.HotKeyModifier.Should().Be(Keys.None);
        profile.HotkeyId.Should().Be(-1);
    }

    [Fact]
    public void ParameterizedConstructor_SetsValues()
    {
        var profile = new Profile("Test", 1.5, 0.7, 80);

        profile.Name.Should().Be("Test");
        profile.Gamma.Should().Be(1.5);
        profile.Contrast.Should().Be(0.7);
        profile.Vibrance.Should().Be(80);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_NullOrWhitespace_DefaultsToNewProfile(string? name)
    {
        var profile = new Profile();
        profile.Name = name!;

        profile.Name.Should().Be("New Profile");
    }

    [Theory]
    [InlineData(-1.0, AppConstants.GammaMin)]
    [InlineData(0.0, AppConstants.GammaMin)]
    [InlineData(5.0, AppConstants.GammaMax)]
    [InlineData(100.0, AppConstants.GammaMax)]
    public void Gamma_ClampsToValidRange(double input, double expected)
    {
        var profile = new Profile();
        profile.Gamma = input;

        profile.Gamma.Should().Be(expected);
    }

    [Theory]
    [InlineData(-0.5, AppConstants.ContrastMin)]
    [InlineData(1.5, AppConstants.ContrastMax)]
    public void Contrast_ClampsToValidRange(double input, double expected)
    {
        var profile = new Profile();
        profile.Contrast = input;

        profile.Contrast.Should().Be(expected);
    }

    [Theory]
    [InlineData(-10, AppConstants.VibranceMin)]
    [InlineData(150, AppConstants.VibranceMax)]
    public void Vibrance_ClampsToValidRange(int input, int expected)
    {
        var profile = new Profile();
        profile.Vibrance = input;

        profile.Vibrance.Should().Be(expected);
    }

    [Fact]
    public void Gamma_WithinRange_SetsExactValue()
    {
        var profile = new Profile();
        profile.Gamma = 1.5;

        profile.Gamma.Should().Be(1.5);
    }

    [Fact]
    public void Contrast_WithinRange_SetsExactValue()
    {
        var profile = new Profile();
        profile.Contrast = 0.75;

        profile.Contrast.Should().Be(0.75);
    }

    [Fact]
    public void Vibrance_WithinRange_SetsExactValue()
    {
        var profile = new Profile();
        profile.Vibrance = 75;

        profile.Vibrance.Should().Be(75);
    }

    [Fact]
    public void WithSettings_ReturnsNewInstanceWithUpdatedValues()
    {
        var original = new Profile("Original", 1.0, 0.5, 50)
        {
            HotKey = Keys.A,
            HotKeyModifier = Keys.Control,
            HotkeyId = 5
        };

        var updated = original.WithSettings(1.5, 0.8, 90);

        updated.Should().NotBeSameAs(original);
        updated.Name.Should().Be("Original");
        updated.Gamma.Should().Be(1.5);
        updated.Contrast.Should().Be(0.8);
        updated.Vibrance.Should().Be(90);
        updated.HotKey.Should().Be(Keys.A);
        updated.HotKeyModifier.Should().Be(Keys.Control);
        updated.HotkeyId.Should().Be(5);
    }

    [Fact]
    public void WithHotkey_ReturnsNewInstanceWithUpdatedHotkey()
    {
        var original = new Profile("Original", 1.0, 0.5, 50);

        var updated = original.WithHotkey(Keys.F5, Keys.Alt, 3);

        updated.Should().NotBeSameAs(original);
        updated.Name.Should().Be("Original");
        updated.Gamma.Should().Be(1.0);
        updated.Contrast.Should().Be(0.5);
        updated.Vibrance.Should().Be(50);
        updated.HotKey.Should().Be(Keys.F5);
        updated.HotKeyModifier.Should().Be(Keys.Alt);
        updated.HotkeyId.Should().Be(3);
    }

    [Fact]
    public void ToString_WithHotkey_IncludesHotkeyInfo()
    {
        var profile = new Profile("Gaming", 1.2, 0.6, 70)
        {
            HotKey = Keys.F1,
            HotKeyModifier = Keys.Control
        };

        string result = profile.ToString();

        result.Should().Contain("Gaming");
        result.Should().Contain("1.20");
        result.Should().Contain("Control");
        result.Should().Contain("F1");
    }

    [Fact]
    public void ToString_WithoutHotkey_ShowsNoHotkey()
    {
        var profile = new Profile("Default", 1.0, 0.5, 50);

        string result = profile.ToString();

        result.Should().Contain("No hotkey");
    }

    [Fact]
    public void JsonSerialization_RoundTrip_PreservesValues()
    {
        var original = new Profile("Test Profile", 1.5, 0.7, 80)
        {
            HotKey = Keys.A,
            HotKeyModifier = Keys.Control
        };

        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Profile>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("Test Profile");
        deserialized.Gamma.Should().Be(1.5);
        deserialized.Contrast.Should().Be(0.7);
        deserialized.Vibrance.Should().Be(80);
        deserialized.HotKeyValue.Should().Be((int)Keys.A);
        deserialized.HotKeyModifierValue.Should().Be((int)Keys.Control);
    }

    [Fact]
    public void HotKeyValue_Property_MapsToHotKey()
    {
        var profile = new Profile();
        profile.HotKeyValue = (int)Keys.F5;

        profile.HotKey.Should().Be(Keys.F5);
        profile.HotKeyValue.Should().Be((int)Keys.F5);
    }

    [Fact]
    public void HotKeyModifierValue_Property_MapsToHotKeyModifier()
    {
        var profile = new Profile();
        profile.HotKeyModifierValue = (int)Keys.Alt;

        profile.HotKeyModifier.Should().Be(Keys.Alt);
        profile.HotKeyModifierValue.Should().Be((int)Keys.Alt);
    }

    [Fact]
    public void Constructor_ClampsOutOfRangeValues()
    {
        var profile = new Profile("Test", -5.0, 2.0, 200);

        profile.Gamma.Should().Be(AppConstants.GammaMin);
        profile.Contrast.Should().Be(AppConstants.ContrastMax);
        profile.Vibrance.Should().Be(AppConstants.VibranceMax);
    }
}
