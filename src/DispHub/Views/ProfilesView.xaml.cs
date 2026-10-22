// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace DispHub.Views;

public partial class ProfilesView : UserControl
{
    public ProfilesView() => InitializeComponent();

    /// <summary>
    /// A click on a profile turns it on, even the one already selected: a list only reports new selections, so
    /// with DispHub off the highlighted profile wouldn't react.
    /// </summary>
    private void OnProfileClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is ItemsControl list && list.ContainerFromElement((DependencyObject)e.OriginalSource) is FrameworkElement { DataContext: ViewModels.ProfileItemViewModel item })
            ((ViewModels.ProfilesViewModel)DataContext).Pick(item);
    }

    private void OnProfileKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is System.Windows.Input.Key.Space or System.Windows.Input.Key.Enter && DataContext is ViewModels.ProfilesViewModel { SelectedProfile: { } item } profiles)
            profiles.Pick(item);
    }

    private void OnTemplatesClick(object sender, RoutedEventArgs e)
    {
        if (TemplatesButton.ContextMenu is { } menu)
        {
            menu.DataContext = DataContext;
            menu.PlacementTarget = TemplatesButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
