// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;

namespace DispHub.Tests.Core;

public sealed class LiquidGlassTests
{
    private const int Width = 200, Height = 120;
    private static readonly GlassLook Look = new(CornerRadius: 12, BevelDepth: 8);

    private static byte[] Grey(byte level)
    {
        var pixels = new byte[Width * Height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
            (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = (level, level, level, 255);
        return pixels;
    }

    // A 100 by 40 panel in the middle of the picture.
    private static byte[] Draw(byte[] pixels) => LiquidGlass.Draw(pixels, Width, Height, 100, 60, 100, 40, Look);

    private static int At(byte[] pixels, int x, int y, int channel = 1) => pixels[(((y * Width) + x) * 4) + channel];

    [Fact]
    public void RoundedRect_IsNegativeInsideZeroOnTheEdgeAndPositiveOutside()
    {
        Assert.Equal(-20, LiquidGlass.RoundedRect(0, 0, 50, 20, 12), 6);
        Assert.Equal(0, LiquidGlass.RoundedRect(50, 0, 50, 20, 12), 6);
        Assert.Equal(5, LiquidGlass.RoundedRect(0, 25, 50, 20, 12), 6);
        // the corner is rounded: its tip is outside
        Assert.True(LiquidGlass.RoundedRect(49.5, 19.5, 50, 20, 12) > 0);
    }

    [Fact]
    public void Draw_LeavesThePictureAwayFromThePanelAsItWas()
    {
        byte[] source = Grey(120);
        byte[] result = Draw(source);

        Assert.Equal(120, At(result, 2, 2));
        Assert.Equal(120, At(result, Width - 3, Height - 3));
        Assert.Equal(255, At(result, 100, 60, 3));
        Assert.Equal(120, At(source, 100, 60));    // works on a copy
    }

    [Fact]
    public void Draw_KeepsAFlatColourInTheMiddleAndLightsTheTopRim()
    {
        byte[] result = Draw(Grey(120));

        // a touch lighter from the soft lights, nothing more
        Assert.InRange(At(result, 100, 60), 120, 135);
        Assert.True(At(result, 100, 41) > 135, $"top rim {At(result, 100, 41)}");
    }

    [Fact]
    public void Draw_CastsAShadowBelowThePanel()
    {
        byte[] result = Draw(Grey(200));

        Assert.True(At(result, 100, 82) < 190, $"below {At(result, 100, 82)}");
        Assert.True(At(result, 100, 82) < At(result, 100, 100));
    }

    [Fact]
    public void Draw_BendsThePictureNearTheEdge()
    {
        // A white line just inside the panel's left edge, on black: the glass moves it.
        byte[] source = Grey(0);
        for (int y = 0; y < Height; y++)
        {
            for (int c = 0; c < 3; c++)
                source[(((y * Width) + 53) * 4) + c] = 255;
        }

        byte[] result = Draw(source);

        Assert.True(At(result, 53, 60) < 128, $"at the line {At(result, 53, 60)}");
        Assert.Equal(255, At(result, 53, 5));      // above the panel, as it was
    }

    [Fact]
    public void Draw_RejectsABufferOfTheWrongSize() =>
        Assert.Throws<ArgumentException>(() => LiquidGlass.Draw(new byte[10], Width, Height, 100, 60, 100, 40, Look));
}
