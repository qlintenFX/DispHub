using FluentAssertions;
using DisplayHub.Constants;
using Xunit;

namespace DisplayHub.Tests.Constants;

public class AppConstantsTests
{
    [Fact]
    public void GammaRange_MinLessThanMax() =>
        AppConstants.GammaMin.Should().BeLessThan(AppConstants.GammaMax);

    [Fact]
    public void GammaDefault_WithinRange()
    {
        AppConstants.GammaDefault.Should().BeGreaterThanOrEqualTo(AppConstants.GammaMin);
        AppConstants.GammaDefault.Should().BeLessThanOrEqualTo(AppConstants.GammaMax);
    }

    [Fact]
    public void ContrastRange_MinLessThanMax() =>
        AppConstants.ContrastMin.Should().BeLessThan(AppConstants.ContrastMax);

    [Fact]
    public void ContrastDefault_WithinRange()
    {
        AppConstants.ContrastDefault.Should().BeGreaterThanOrEqualTo(AppConstants.ContrastMin);
        AppConstants.ContrastDefault.Should().BeLessThanOrEqualTo(AppConstants.ContrastMax);
    }

    [Fact]
    public void VibranceRange_MinLessThanMax() =>
        AppConstants.VibranceMin.Should().BeLessThan(AppConstants.VibranceMax);

    [Fact]
    public void VibranceDefault_WithinRange()
    {
        AppConstants.VibranceDefault.Should().BeGreaterThanOrEqualTo(AppConstants.VibranceMin);
        AppConstants.VibranceDefault.Should().BeLessThanOrEqualTo(AppConstants.VibranceMax);
    }

    [Fact] public void GammaStep_IsPositive() => AppConstants.GammaStep.Should().BeGreaterThan(0);
    [Fact] public void ContrastStep_IsPositive() => AppConstants.ContrastStep.Should().BeGreaterThan(0);
    [Fact] public void VibranceStep_IsPositive() => AppConstants.VibranceStep.Should().BeGreaterThan(0);

    [Fact]
    public void NvidiaVibranceRange_MinLessThanMax() =>
        AppConstants.NvidiaVibranceMin.Should().BeLessThan(AppConstants.NvidiaVibranceMax);

    [Fact] public void GammaRampSize_Is256() => AppConstants.GammaRampSize.Should().Be(256);
    [Fact] public void WM_HOTKEY_IsCorrectValue() => AppConstants.WM_HOTKEY.Should().Be(0x0312);
    [Fact] public void Version_Is3_0_0() => AppConstants.Version.Should().Be("3.0.0");
    [Fact] public void ApplicationName_IsDisplayHub() => AppConstants.ApplicationName.Should().Be("DisplayHub");

    [Fact]
    public void HotkeyModifiers_AreDistinct()
    {
        var modifiers = new[] { AppConstants.MOD_ALT, AppConstants.MOD_CONTROL, AppConstants.MOD_SHIFT, AppConstants.MOD_WIN };
        modifiers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void RegistryKeys_AreNotEmpty()
    {
        AppConstants.StartupRegistryKey.Should().NotBeNullOrEmpty();
        AppConstants.SettingsRegistryKey.Should().NotBeNullOrEmpty();
    }
}
