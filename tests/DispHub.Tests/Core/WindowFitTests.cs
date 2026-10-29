// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using DispHub.Core.Config;
using DispHub.Core.Model;

namespace DispHub.Tests.Core;

public sealed class WindowFitTests
{
    // The window's own size and minimum, 1100 by 760 and 760 by 520, at a given scale.
    private static Size Preferred(double scale) => new((int)(1100 * scale), (int)(760 * scale));

    private static Size Minimum(double scale) => new((int)(760 * scale), (int)(520 * scale));

    [Fact]
    public void FirstOpen_OnA2560By1600LaptopAt200Percent_FitsAboveTheTaskbar()
    {
        var work = new Rectangle(0, 0, 2560, 1504);

        Rectangle window = WindowFit.FirstOpen(work, Preferred(2), Minimum(2));

        Assert.Equal(2200, window.Width);
        Assert.Equal((int)(1504 * WindowFit.MaxShare), window.Height);
        Assert.True(work.Contains(window));
        // centred, give or take the odd pixel
        Assert.InRange(window.Left - (work.Width - window.Right), -1, 1);
        Assert.InRange(window.Top - (work.Height - window.Bottom), -1, 1);
    }

    [Theory]
    [InlineData(1920, 1032, 1.0)] // 1080p at 100 %
    [InlineData(2560, 1392, 1.25)] // 1440p at 125 %
    [InlineData(3840, 2088, 1.5)] // 4K at 150 %
    public void FirstOpen_WhereItFits_KeepsItsOwnSize(int width, int height, double scale)
    {
        Rectangle window = WindowFit.FirstOpen(new Rectangle(0, 0, width, height), Preferred(scale), Minimum(scale));

        Assert.Equal(Preferred(scale), window.Size);
    }

    [Fact]
    public void FirstOpen_OnAScreenSmallerThanTheMinimum_StillFits()
    {
        // 1280 by 720 at 150 %: 853 by 448 points, below the 520 point minimum height.
        var work = new Rectangle(0, 0, 1280, 672);

        Rectangle window = WindowFit.FirstOpen(work, Preferred(1.5), Minimum(1.5));

        Assert.True(work.Contains(window));
        Assert.Equal(672, window.Height);
    }

    [Fact]
    public void FirstOpen_OnASecondMonitor_CentresOnThatMonitor()
    {
        var work = new Rectangle(-1920, 200, 1920, 1040);

        Rectangle window = WindowFit.FirstOpen(work, Preferred(1), Minimum(1));

        Assert.True(work.Contains(window));
        Assert.Equal(-1920 + 410, window.Left);
    }

    [Fact]
    public void Restore_KeepsAWindowThatStillFits()
    {
        var bounds = new Rectangle(300, 120, 1600, 1100);

        Assert.Equal(bounds, WindowFit.Restore(bounds, new Rectangle(0, 0, 2560, 1504), Minimum(2)));
    }

    [Fact]
    public void Restore_AfterTheScreenGotSmaller_ShrinksAndMovesItBackOn()
    {
        // Saved on a 4K monitor that's gone; the laptop screen is all that's left.
        var bounds = new Rectangle(2800, 400, 2600, 1700);
        var work = new Rectangle(0, 0, 2560, 1504);

        Rectangle window = WindowFit.Restore(bounds, work, Minimum(2));

        Assert.Equal(work, window);
    }

    [Fact]
    public void Restore_PartlyOffScreen_MovesItFullyOn()
    {
        var work = new Rectangle(0, 0, 1920, 1032);

        Rectangle window = WindowFit.Restore(new Rectangle(-300, 900, 1100, 760), work, Minimum(1));

        Assert.Equal(new Rectangle(0, 272, 1100, 760), window);
    }

    [Fact]
    public void Normalizer_DropsADamagedWindowPosition()
    {
        var config = new AppConfig { Ui = { Window = new WindowBounds(0, 0, 0, -5, false) } };

        ConfigNormalizer.Normalize(config);

        Assert.Null(config.Ui.Window);
    }
}
