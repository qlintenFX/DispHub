using DispHub.Helpers;

namespace DispHub.Tests;

public class NativeStructTests
{
    [Fact]
    public void RECT_Width_CalculatesCorrectly()
    {
        var rect = new NativeMethods.RECT { Left = 10, Right = 110 };
        Assert.Equal(100, rect.Width);
    }

    [Fact]
    public void RECT_Height_CalculatesCorrectly()
    {
        var rect = new NativeMethods.RECT { Top = 20, Bottom = 120 };
        Assert.Equal(100, rect.Height);
    }

    [Fact]
    public void RECT_ZeroSize_ReturnsZeroDimensions()
    {
        var rect = new NativeMethods.RECT { Left = 50, Right = 50, Top = 50, Bottom = 50 };
        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
    }

    [Fact]
    public void RECT_DefaultValues_AreZero()
    {
        var rect = new NativeMethods.RECT();
        Assert.Equal(0, rect.Left);
        Assert.Equal(0, rect.Top);
        Assert.Equal(0, rect.Right);
        Assert.Equal(0, rect.Bottom);
        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
    }

    [Fact]
    public void POINT_DefaultValues_AreZero()
    {
        var point = new NativeMethods.POINT();
        Assert.Equal(0, point.X);
        Assert.Equal(0, point.Y);
    }

    [Fact]
    public void POINT_PropertiesAreSettable()
    {
        var point = new NativeMethods.POINT { X = 100, Y = 200 };
        Assert.Equal(100, point.X);
        Assert.Equal(200, point.Y);
    }

    [Fact]
    public void WindowStyleConstants_AreExpectedValues()
    {
        Assert.Equal(-16, NativeMethods.GWL_STYLE);
        Assert.Equal(-20, NativeMethods.GWL_EXSTYLE);
        Assert.Equal(0x80000000u, NativeMethods.WS_POPUP);
        Assert.Equal(0x40000000u, NativeMethods.WS_CHILD);
        Assert.Equal(0x08000000, NativeMethods.WS_EX_NOACTIVATE);
        Assert.Equal(0x0004u, NativeMethods.SWP_NOZORDER);
        Assert.Equal(0x0010u, NativeMethods.SWP_NOACTIVATE);
        Assert.Equal(0x0040u, NativeMethods.SWP_SHOWWINDOW);
        Assert.Equal(0x4000u, NativeMethods.SWP_ASYNCWINDOWPOS);
    }
}
