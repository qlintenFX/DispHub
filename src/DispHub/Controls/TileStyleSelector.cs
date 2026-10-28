// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace DispHub.Controls;

/// <summary>Profile tiles get the selectable card; the grid's "add" tile gets a container that isn't selectable.</summary>
public sealed class TileStyleSelector : StyleSelector
{
    public Style? TileStyle { get; set; }

    public Style? AddStyle { get; set; }

    public override Style? SelectStyle(object item, DependencyObject container) =>
        item is ViewModels.AddTileViewModel ? AddStyle : TileStyle;
}
