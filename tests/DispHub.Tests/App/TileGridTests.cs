// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DispHub.Controls;

namespace DispHub.Tests.App;

public sealed class TileGridTests
{
    [Theory]
    [InlineData(4, 3, 2)] // two and two, not three and one
    [InlineData(5, 4, 3)]
    [InlineData(4, 4, 4)]
    [InlineData(7, 4, 4)]
    [InlineData(2, 4, 2)]
    [InlineData(0, 4, 1)]
    public void Columns_EvenOutTheRows(int count, int fit, int columns) =>
        Assert.Equal(columns, TileGrid.Columns(count, fit));

    [Fact]
    public void AddTile_FillsTheRestOfItsRow()
    {
        // Four profiles and the add tile at a width that fits three columns: three, then one and a wide add tile.
        List<Rect> slots = Arrange(width: 560, tiles: 5);
        double tile = (560 - 16) / 3.0;
        Assert.Equal(tile, slots[0].Width, 3);
        Assert.Equal(0, slots[3].X);
        Assert.Equal(slots[1].X, slots[4].X, 3);
        Assert.Equal(560 - slots[4].X, slots[4].Width, 3);
        Assert.Equal(slots[3].Y, slots[4].Y);
    }

    [Fact]
    public void AddTile_AloneOnItsRow_IsASlimBar()
    {
        List<Rect> slots = Arrange(width: 560, tiles: 4);
        Assert.Equal(0, slots[3].X);
        Assert.Equal(560, slots[3].Width);
        Assert.Equal(AddHeight, slots[3].Height);
        Assert.Equal(TileHeight + 8, slots[3].Y);
    }

    [Fact]
    public void ThreeProfilesAndTheAddTile_ShareOneRowWhenTheyFit()
    {
        List<Rect> slots = Arrange(width: 760, tiles: 4);
        Assert.All(slots, s => Assert.Equal(0, s.Y));
        Assert.Equal(760, slots[3].Right, 3);
        Assert.Equal(slots[0].Width, slots[3].Width, 3);
    }

    private const double TileHeight = 100;
    private const double AddHeight = 50;

    /// <summary>Lays out <paramref name="tiles"/> tiles (the last one is the short add tile) and returns each one's slot.</summary>
    private static List<Rect> Arrange(double width, int tiles)
    {
        List<Rect> result = [];
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var grid = new TileGrid { FillLastRow = true, MinTileWidth = 180, MaxColumns = 4, Spacing = 8 };
                for (int i = 0; i < tiles; i++)
                    grid.Children.Add(new Border { Height = i == tiles - 1 ? AddHeight : TileHeight });
                grid.Measure(new Size(width, double.PositiveInfinity));
                grid.Arrange(new Rect(0, 0, width, grid.DesiredSize.Height));
                foreach (UIElement child in grid.Children)
                    result.Add(LayoutInformation.GetLayoutSlot((FrameworkElement)child));
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw failure;
        return result;
    }
}
