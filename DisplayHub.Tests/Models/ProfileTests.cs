using System.Text.Json;
using FluentAssertions;
using DisplayHub.Constants;
using DisplayHub.Models;
using Xunit;

namespace DisplayHub.Tests.Models;

public class ProfileTests
{
    private const int VK_A = 0x41;
    private const int VK_F1 = 0x70;
    private const int VK_F5 = 0x74;
    // WinForms-style bitmask values stored in Profile.HotKeyModifier
    private const int MOD_CTRL = 0x20000;
    private const int MOD_ALT = 0x40000;

    [Fact]
    public void DefaultConstructor_SetsDefaultValues()
    {
        var profile = new Profile();
        profile.Name.Should().Be("New Profile");
        profile.Gamma.Should().Be(AppConstants.GammaDefault);
        profile.Contrast.Should().Be(AppConstants.ContrastDefault);
        profile.Vibrance.Should().Be(AppConstants.VibranceDefault);
        profile.HotKey.Should().Be(0);
        profile.HotKeyModifier.Should().Be(0);
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
        var original = new Profile("Original", 1.0, 0.5, 50) { HotKey = VK_A, HotKeyModifier = MOD_CTRL, HotkeyId = 5 };
        var updated = original.WithSettings(1.5, 0.8, 90);
        updated.Should().NotBeSameAs(original);
        updated.Name.Should().Be("Original");
        updated.Gamma.Should().Be(1.5);
        updated.Contrast.Should().Be(0.8);
        updated.Vibrance.Should().Be(90);
        updated.HotKey.Should().Be(VK_A);
        updated.HotKeyModifier.Should().Be(MOD_CTRL);
        updated.HotkeyId.Should().Be(5);
    }

    [Fact]
    public void WithHotkey_ReturnsNewInstanceWithUpdatedHotkey()
    {
        var original = new Profile("Original", 1.0, 0.5, 50);
        var updated = original.WithHotkey(VK_F5, MOD_ALT, 3);
        updated.Should().NotBeSameAs(original);
        updated.HotKey.Should().Be(VK_F5);
        updated.HotKeyModifier.Should().Be(MOD_ALT);
        updated.HotkeyId.Should().Be(3);
    }

    [Fact]
    public void ToString_WithHotkey_ContainsNameAndGamma()
    {
        var profile = new Profile("Gaming", 1.2, 0.6, 70) { HotKey = VK_F1, HotKeyModifier = MOD_CTRL };
        string result = profile.ToString();
        result.Should().Contain("Gaming");
        result.Should().Contain("1.20");
    }

    [Fact]
    public void ToString_WithoutHotkey_ShowsNoHotkey()
    {
        var profile = new Profile("Default", 1.0, 0.5, 50);
        profile.ToString().Should().Contain("No hotkey");
    }

    [Fact]
    public void JsonSerialization_RoundTrip_PreservesValues()
    {
        var original = new Profile("Test Profile", 1.5, 0.7, 80) { HotKey = VK_A, HotKeyModifier = MOD_CTRL };
        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Profile>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("Test Profile");
        deserialized.Gamma.Should().Be(1.5);
        deserialized.Contrast.Should().Be(0.7);
        deserialized.Vibrance.Should().Be(80);
        deserialized.HotKeyValue.Should().Be(VK_A);
        deserialized.HotKeyModifierValue.Should().Be(MOD_CTRL);
    }

    [Fact]
    public void HotKeyValue_Property_MapsToHotKey()
    {
        var profile = new Profile();
        profile.HotKeyValue = VK_F5;
        profile.HotKey.Should().Be(VK_F5);
        profile.HotKeyValue.Should().Be(VK_F5);
    }

    [Fact]
    public void HotKeyModifierValue_Property_MapsToHotKeyModifier()
    {
        var profile = new Profile();
        profile.HotKeyModifierValue = MOD_ALT;
        profile.HotKeyModifier.Should().Be(MOD_ALT);
        profile.HotKeyModifierValue.Should().Be(MOD_ALT);
    }

    [Fact]
    public void Constructor_ClampsOutOfRangeValues()
    {
        var profile = new Profile("Test", -5.0, 2.0, 200);
        profile.Gamma.Should().Be(AppConstants.GammaMin);
        profile.Contrast.Should().Be(AppConstants.ContrastMax);
        profile.Vibrance.Should().Be(AppConstants.VibranceMax);
    }

    [Fact]
    public void HotkeyDisplayText_WithNoHotkey_ReturnsEmpty()
    {
        new Profile().HotkeyDisplayText.Should().BeEmpty();
    }

    [Fact]
    public void HotkeyDisplayText_WithCtrlModifier_ContainsCtrl()
    {
        new Profile { HotKey = VK_A, HotKeyModifier = MOD_CTRL }.HotkeyDisplayText.Should().Contain("Ctrl");
    }

    [Fact]
    public void HotkeyDisplayText_WithAltModifier_ContainsAlt()
    {
        new Profile { HotKey = VK_F1, HotKeyModifier = MOD_ALT }.HotkeyDisplayText.Should().Contain("Alt");
    }
}
