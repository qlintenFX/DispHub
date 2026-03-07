using FluentAssertions;
using DisplayHub.Services.Display;
using Xunit;

namespace DisplayHub.Tests.Services.Display;

public class NvidiaVibranceServiceTests
{
    private const int DvcMin = -63;
    private const int DvcMax = 63;

    [Theory]
    [InlineData(0, -63)]
    [InlineData(50, 0)]
    [InlineData(100, 63)]
    [InlineData(25, -32)]
    [InlineData(75, 31)]
    public void MapToNvidiaRange_MapsCorrectly(int appValue, int expected) =>
        NvidiaVibranceService.MapToNvidiaRange(appValue, DvcMin, DvcMax).Should().Be(expected);

    [Theory]
    [InlineData(-10, -63)]
    [InlineData(-1, -63)]
    public void MapToNvidiaRange_NegativeAppValues_ClampsToMin(int appValue, int expected) =>
        NvidiaVibranceService.MapToNvidiaRange(appValue, DvcMin, DvcMax).Should().Be(expected);

    [Theory]
    [InlineData(101, 63)]
    [InlineData(200, 63)]
    public void MapToNvidiaRange_OverMaxValues_ClampsToMax(int appValue, int expected) =>
        NvidiaVibranceService.MapToNvidiaRange(appValue, DvcMin, DvcMax).Should().Be(expected);

    [Theory]
    [InlineData(-63, 0)]
    [InlineData(0, 50)]
    [InlineData(63, 100)]
    [InlineData(-31, 25)]
    [InlineData(31, 74)]
    public void MapFromNvidiaRange_MapsCorrectly(int nvidiaValue, int expected) =>
        NvidiaVibranceService.MapFromNvidiaRange(nvidiaValue, DvcMin, DvcMax).Should().Be(expected);

    [Fact]
    public void MapToNvidiaRange_BoundaryValues_AreCorrect()
    {
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
        NvidiaVibranceService.MapToNvidiaRange(0, -100, 100).Should().Be(-100);
        NvidiaVibranceService.MapToNvidiaRange(50, -100, 100).Should().Be(0);
        NvidiaVibranceService.MapToNvidiaRange(100, -100, 100).Should().Be(100);
    }

    [Fact]
    public void MapFromNvidiaRange_EqualMinMax_ReturnsDefault() =>
        NvidiaVibranceService.MapFromNvidiaRange(0, 0, 0).Should().Be(50);

    [Fact]
    public void MapToNvidiaRange_ZeroToSixtyThreeRange_MapsCorrectly()
    {
        NvidiaVibranceService.MapToNvidiaRange(0, 0, 63).Should().Be(0);
        NvidiaVibranceService.MapToNvidiaRange(100, 0, 63).Should().Be(63);
    }
}
