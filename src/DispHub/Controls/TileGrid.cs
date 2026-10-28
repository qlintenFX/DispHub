// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace DispHub.Controls;

/// <summary>
/// Lays tiles out in equal columns that fill the width. Without <see cref="FillLastRow"/> it uses as many columns as
/// fit, then as few as still give the same number of rows, so the rows come out even: four tiles that don't fit in
/// one row become two and two rather than three and one. With it, the last tile (an "add" tile) takes whatever is
/// left of its row instead, so the grid never ends in a gap; alone on its row it shrinks to a slim bar.
/// </summary>
public sealed class TileGrid : Panel
{
    public static readonly DependencyProperty MinTileWidthProperty = DependencyProperty.Register(
        nameof(MinTileWidth), typeof(double), typeof(TileGrid),
        new FrameworkPropertyMetadata(180.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(TileGrid),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty MaxTileWidthProperty = DependencyProperty.Register(
        nameof(MaxTileWidth), typeof(double), typeof(TileGrid),
        new FrameworkPropertyMetadata(300.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(TileGrid),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty FillLastRowProperty = DependencyProperty.Register(
        nameof(FillLastRow), typeof(bool), typeof(TileGrid),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsArrange));

    public double MinTileWidth
    {
        get => (double)GetValue(MinTileWidthProperty);
        set => SetValue(MinTileWidthProperty, value);
    }

    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    /// <summary>Tiles stop growing at this width when only a few are shown.</summary>
    public double MaxTileWidth
    {
        get => (double)GetValue(MaxTileWidthProperty);
        set => SetValue(MaxTileWidthProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>The last tile stretches over the rest of its row.</summary>
    public bool FillLastRow
    {
        get => (bool)GetValue(FillLastRowProperty);
        set => SetValue(FillLastRowProperty, value);
    }

    /// <summary>The number of columns for <paramref name="count"/> tiles when at most <paramref name="fit"/> fit in a row.</summary>
    public static int Columns(int count, int fit)
    {
        if (count <= 0)
            return 1;
        fit = Math.Max(1, fit);
        int rows = (count + fit - 1) / fit;
        return (count + rows - 1) / rows;
    }

    private (int Columns, double TileWidth) Layout(double width)
    {
        int count = InternalChildren.Count;
        double spacing = Spacing;
        if (double.IsInfinity(width))
            width = (count * (MinTileWidth + spacing)) - spacing;
        int fit = Math.Clamp((int)Math.Floor((width + spacing) / (MinTileWidth + spacing)), 1, Math.Max(1, MaxColumns));
        int columns = FillLastRow ? Math.Clamp(count, 1, fit) : Columns(count, fit);
        double tileWidth = Math.Clamp((width - ((columns - 1) * spacing)) / columns, 0, Math.Max(MinTileWidth, MaxTileWidth));
        return (columns, tileWidth);
    }

    /// <summary>True when the last tile has a row to itself and is drawn as a bar.</summary>
    private bool LastIsBar(int columns) =>
        FillLastRow && InternalChildren.Count > 1 && (InternalChildren.Count - 1) % columns == 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        (int columns, double tileWidth) = Layout(availableSize.Width);
        int count = InternalChildren.Count;
        bool bar = LastIsBar(columns);
        double width = double.IsInfinity(availableSize.Width) ? (columns * (tileWidth + Spacing)) - Spacing : availableSize.Width;
        double tileHeight = 0;
        for (int i = 0; i < count; i++)
        {
            UIElement child = InternalChildren[i];
            child.Measure(new Size(bar && i == count - 1 ? width : tileWidth, double.PositiveInfinity));
            if (!(bar && i == count - 1))
                tileHeight = Math.Max(tileHeight, child.DesiredSize.Height);
        }
        if (count == 0)
            return new Size(0, 0);
        int fullRows = bar ? (count - 1 + columns - 1) / columns : (count + columns - 1) / columns;
        double height = (fullRows * tileHeight) + ((fullRows - 1) * Spacing);
        if (bar)
            height += Spacing + InternalChildren[count - 1].DesiredSize.Height;
        return new Size(Math.Max(0, width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        (int columns, double tileWidth) = Layout(finalSize.Width);
        int count = InternalChildren.Count;
        bool bar = LastIsBar(columns);
        // Every tile gets the height of the tallest, so the rows line up.
        double tileHeight = InternalChildren.Cast<UIElement>()
            .Take(bar ? count - 1 : count)
            .Select(c => c.DesiredSize.Height)
            .DefaultIfEmpty(0)
            .Max();
        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int column = i % columns;
            double x = column * (tileWidth + Spacing);
            double y = row * (tileHeight + Spacing);
            if (bar && i == count - 1)
            {
                InternalChildren[i].Arrange(new Rect(0, y, finalSize.Width, InternalChildren[i].DesiredSize.Height));
                continue;
            }
            bool last = i == count - 1;
            double width = last && FillLastRow ? Math.Max(tileWidth, finalSize.Width - x) : tileWidth;
            InternalChildren[i].Arrange(new Rect(x, y, width, tileHeight));
        }
        return finalSize;
    }
}
