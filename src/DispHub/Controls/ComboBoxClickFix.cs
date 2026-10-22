// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DispHub.Controls;

/// <summary>
/// WPF picks the drop-down item under the mouse when the button is released. Near the bottom of the screen the list
/// opens over the box itself, so a plain click opened the list and picked an item at once. This ignores the release
/// of the click that opened the list, unless the mouse moved to an item first (press, drag, release still works).
/// </summary>
public static class ComboBoxClickFix
{
    private const double Slop = 4;
    private static readonly TimeSpan ClickTime = TimeSpan.FromMilliseconds(700);
    private static Point _pressedAt;
    private static long _pressedTicks;
    private static bool _opening;

    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnBoxPressed));
        EventManager.RegisterClassHandler(typeof(ComboBoxItem), UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnItemReleased));
    }

    private static void OnBoxPressed(object sender, MouseButtonEventArgs e)
    {
        var box = (ComboBox)sender;
        // A press on an item of an open list also arrives here; only a press that opens the list counts.
        _opening = !box.IsDropDownOpen;
        _pressedAt = box.PointToScreen(e.GetPosition(box));
        _pressedTicks = Environment.TickCount64;
    }

    private static void OnItemReleased(object sender, MouseButtonEventArgs e)
    {
        if (!_opening)
            return;
        _opening = false;
        var item = (ComboBoxItem)sender;
        Point releasedAt = item.PointToScreen(e.GetPosition(item));
        bool moved = Math.Abs(releasedAt.X - _pressedAt.X) > Slop || Math.Abs(releasedAt.Y - _pressedAt.Y) > Slop;
        if (!moved && Environment.TickCount64 - _pressedTicks < ClickTime.TotalMilliseconds)
            e.Handled = true;
    }
}
