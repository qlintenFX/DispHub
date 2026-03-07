using FluentAssertions;
using DisplayHub.Constants;
using DisplayHub.Services.Display;
using DisplayHub.Services.Hotkeys;
using Moq;
using Xunit;

namespace DisplayHub.Tests.Services.Hotkeys;

public class DynamicControlsTests
{
    private readonly Mock<IVibranceService> _mockVibranceService;
    private readonly DisplayManager _displayManager;
    private readonly DynamicControls _sut;

    public DynamicControlsTests()
    {
        _mockVibranceService = new Mock<IVibranceService>();
        _mockVibranceService.Setup(v => v.MinValue).Returns(0);
        _mockVibranceService.Setup(v => v.MaxValue).Returns(100);
        _mockVibranceService.Setup(v => v.DefaultValue).Returns(50);
        _mockVibranceService.Setup(v => v.IsSupported).Returns(false);
        _mockVibranceService.Setup(v => v.ApplyVibrance(It.IsAny<int>())).Returns(true);
        _displayManager = new DisplayManager(_mockVibranceService.Object);
        _sut = new DynamicControls(_displayManager);
    }

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        _sut.Gamma.Should().Be(AppConstants.GammaDefault);
        _sut.Contrast.Should().Be(AppConstants.ContrastDefault);
        _sut.Vibrance.Should().Be(AppConstants.VibranceDefault);
        _sut.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void AdjustGamma_WhenDisabled_DoesNothing()
    {
        _sut.IsEnabled = false;
        double original = _sut.Gamma;
        _sut.AdjustGamma(AppConstants.GammaStep);
        _sut.Gamma.Should().Be(original);
    }

    [Fact]
    public void AdjustGamma_WhenEnabled_AdjustsValue()
    {
        _sut.IsEnabled = true;
        double original = _sut.Gamma;
        _sut.AdjustGamma(AppConstants.GammaStep);
        _sut.Gamma.Should().Be(original + AppConstants.GammaStep);
    }

    [Fact]
    public void AdjustGamma_ClampsAtMax()
    {
        _sut.IsEnabled = true;
        _sut.SetValues(AppConstants.GammaMax, AppConstants.ContrastDefault, AppConstants.VibranceDefault);
        _sut.AdjustGamma(AppConstants.GammaStep);
        _sut.Gamma.Should().Be(AppConstants.GammaMax);
    }

    [Fact]
    public void AdjustGamma_ClampsAtMin()
    {
        _sut.IsEnabled = true;
        _sut.SetValues(AppConstants.GammaMin, AppConstants.ContrastDefault, AppConstants.VibranceDefault);
        _sut.AdjustGamma(-AppConstants.GammaStep);
        _sut.Gamma.Should().Be(AppConstants.GammaMin);
    }

    [Fact]
    public void AdjustContrast_WhenDisabled_DoesNothing()
    {
        _sut.IsEnabled = false;
        double original = _sut.Contrast;
        _sut.AdjustContrast(AppConstants.ContrastStep);
        _sut.Contrast.Should().Be(original);
    }

    [Fact]
    public void AdjustContrast_WhenEnabled_AdjustsValue()
    {
        _sut.IsEnabled = true;
        double original = _sut.Contrast;
        _sut.AdjustContrast(AppConstants.ContrastStep);
        _sut.Contrast.Should().Be(original + AppConstants.ContrastStep);
    }

    [Fact]
    public void AdjustContrast_ClampsAtMax()
    {
        _sut.IsEnabled = true;
        _sut.SetValues(AppConstants.GammaDefault, AppConstants.ContrastMax, AppConstants.VibranceDefault);
        _sut.AdjustContrast(AppConstants.ContrastStep);
        _sut.Contrast.Should().Be(AppConstants.ContrastMax);
    }

    [Fact]
    public void AdjustVibrance_WhenDisabled_DoesNothing()
    {
        _sut.IsEnabled = false;
        int original = _sut.Vibrance;
        _sut.AdjustVibrance(AppConstants.VibranceStep);
        _sut.Vibrance.Should().Be(original);
    }

    [Fact]
    public void AdjustVibrance_WhenEnabled_AdjustsValue()
    {
        _sut.IsEnabled = true;
        int original = _sut.Vibrance;
        _sut.AdjustVibrance(AppConstants.VibranceStep);
        _sut.Vibrance.Should().Be(original + AppConstants.VibranceStep);
    }

    [Fact]
    public void AdjustVibrance_ClampsAtMax()
    {
        _sut.IsEnabled = true;
        _sut.SetValues(AppConstants.GammaDefault, AppConstants.ContrastDefault, AppConstants.VibranceMax);
        _sut.AdjustVibrance(AppConstants.VibranceStep);
        _sut.Vibrance.Should().Be(AppConstants.VibranceMax);
    }

    [Fact]
    public void AdjustVibrance_ClampsAtMin()
    {
        _sut.IsEnabled = true;
        _sut.SetValues(AppConstants.GammaDefault, AppConstants.ContrastDefault, AppConstants.VibranceMin);
        _sut.AdjustVibrance(-AppConstants.VibranceStep);
        _sut.Vibrance.Should().Be(AppConstants.VibranceMin);
    }

    [Fact]
    public void SetValues_ClampsAllValues()
    {
        _sut.IsEnabled = true;
        _sut.SetValues(-1.0, 2.0, 200);
        _sut.Gamma.Should().Be(AppConstants.GammaMin);
        _sut.Contrast.Should().Be(AppConstants.ContrastMax);
        _sut.Vibrance.Should().Be(AppConstants.VibranceMax);
    }

    [Fact]
    public void SetValues_WhenEnabled_FiresValuesChanged()
    {
        _sut.IsEnabled = true;
        bool fired = false;
        _sut.ValuesChanged += (_, _) => fired = true;
        _sut.SetValues(1.5, 0.7, 80);
        fired.Should().BeTrue();
    }

    [Fact]
    public void SetValues_WhenDisabled_DoesNotFireValuesChanged()
    {
        _sut.IsEnabled = false;
        bool fired = false;
        _sut.ValuesChanged += (_, _) => fired = true;
        _sut.SetValues(1.5, 0.7, 80);
        fired.Should().BeFalse();
    }

    [Fact]
    public void AdjustGamma_WhenEnabled_FiresValuesChanged()
    {
        _sut.IsEnabled = true;
        bool fired = false;
        _sut.ValuesChanged += (_, _) => fired = true;
        _sut.AdjustGamma(AppConstants.GammaStep);
        fired.Should().BeTrue();
    }

    [Fact]
    public void ProcessHotkey_WhenDisabled_ReturnsFalse()
    {
        _sut.IsEnabled = false;
        _sut.ProcessHotkey(IntPtr.Zero).Should().BeFalse();
    }
}
