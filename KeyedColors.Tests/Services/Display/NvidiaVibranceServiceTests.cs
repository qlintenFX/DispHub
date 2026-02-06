using FluentAssertions;
using KeyedColors.Services.Display;
using Xunit;

namespace KeyedColors.Tests.Services.Display;

public class NvidiaVibranceServiceTests
{
    // Symmetric range around 0 (typical NVIDIA GPU)
    private const int DvcMin = -63;
    private const int DvcMax = 63;

    [Theory]
    [InlineData(0, -63)]    // App 0% → DVC min (B&W)
    [InlineData(50, 0)]     // App 50% → DVC 0 (neutral, NVIDIA CP 50%)
    [InlineData(100, 63)]   // App 100% → DVC max (saturated)
    [InlineData(25, -32)]   // Quarter-way (integer rounding: -63 + (25*126)/100 = -63+31 = -32)
    [InlineData(75, 31)]    // Three-quarters
    public void MapToNvidiaRange_MapsCorrectly(int appValue, int expected)
    {
        int result = NvidiaVibranceService.MapToNvidiaRange(appValue, DvcMin, DvcMax);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-10, -63)]  // Below min clamps to DVC min
    [InlineData(-1, -63)]
    public void MapToNvidiaRange_NegativeAppValues_ClampsToMin(int appValue, int expected)
    {
        int result = NvidiaVibranceService.MapToNvidiaRange(appValue, DvcMin, DvcMax);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(101, 63)]   // Above max clamps to DVC max
    [InlineData(200, 63)]
    public void MapToNvidiaRange_OverMaxValues_ClampsToMax(int appValue, int expected)
    {
        int result = NvidiaVibranceService.MapToNvidiaRange(appValue, DvcMin, DvcMax);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-63, 0)]    // DVC min → App 0%
    [InlineData(0, 50)]     // DVC 0 → App 50%
    [InlineData(63, 100)]   // DVC max → App 100%
    [InlineData(-31, 25)]   // Quarter
    [InlineData(31, 74)]    // Three-quarters (integer rounding)
    public void MapFromNvidiaRange_MapsCorrectly(int nvidiaValue, int expected)
    {
        int result = NvidiaVibranceService.MapFromNvidiaRange(nvidiaValue, DvcMin, DvcMax);

        result.Should().Be(expected);
    }

    [Fact]
    public void MapToNvidiaRange_BoundaryValues_AreCorrect()
    {
        // Full round-trip at boundaries
        NvidiaVibranceService.MapToNvidiaRange(0, DvcMin, DvcMax).Should().Be(-63);
        NvidiaVibranceService.MapFromNvidiaRange(-63, DvcMin, DvcMax).Should().Be(0);

        NvidiaVibranceService.MapToNvidiaRange(50, DvcMin, DvcMax).Should().Be(0);
        NvidiaVibranceService.MapFromNvidiaRange(0, DvcMin, DvcMax).Should().Be(50);

        NvidiaVibranceService.MapToNvidiaRange(100, DvcMin, DvcMax).Should().Be(63);
        NvidiaVibranceService.MapFromNvidiaRange(63, DvcMin, DvcMax).Should().Be(100);
    }

    [Fact]
    public void MapToNvidiaRange_AsymmetricRange_MapsCorrectly()
    {
        // Some GPUs might report asymmetric ranges
        int result = NvidiaVibranceService.MapToNvidiaRange(0, -100, 100);
        result.Should().Be(-100);

        result = NvidiaVibranceService.MapToNvidiaRange(50, -100, 100);
        result.Should().Be(0);

        result = NvidiaVibranceService.MapToNvidiaRange(100, -100, 100);
        result.Should().Be(100);
    }

    [Fact]
    public void MapFromNvidiaRange_EqualMinMax_ReturnsDefault()
    {
        int result = NvidiaVibranceService.MapFromNvidiaRange(0, 0, 0);
        result.Should().Be(50); // Default vibrance
    }

    [Fact]
    public void MapToNvidiaRange_ZeroToSixtyThreeRange_MapsCorrectly()
    {
        // Old-style range (0-63, no desaturation)
        int result = NvidiaVibranceService.MapToNvidiaRange(0, 0, 63);
        result.Should().Be(0);

        result = NvidiaVibranceService.MapToNvidiaRange(100, 0, 63);
        result.Should().Be(63);
    }
}
