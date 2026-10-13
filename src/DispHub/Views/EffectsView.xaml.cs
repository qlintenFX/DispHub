using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DispHub.ViewModels;

namespace DispHub.Views;

public partial class EffectsView : UserControl
{
    /// <summary>Below this width the look's name and actions go under the preview, and the effects box under the look.</summary>
    private const double NarrowWidth = 640;

    /// <summary>The clipboard-style format an effect is dragged in: its key.</summary>
    private const string EffectFormat = "DispHub.Effect";

    private Point _pressedAt;

    public EffectsView() => InitializeComponent();

    private void OnPreviewCardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < NarrowWidth;
        Grid.SetColumn(InfoPanel, narrow ? 0 : 1);
        Grid.SetRow(InfoPanel, narrow ? 1 : 0);
        InfoPanel.Margin = narrow ? new Thickness(0, 14, 0, 0) : new Thickness(20, 0, 0, 0);
        PictureColumn.Width = narrow ? new GridLength(1, GridUnitType.Star) : new GridLength(3, GridUnitType.Star);
        InfoColumn.Width = narrow ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
        InfoColumn.MinWidth = narrow ? 0 : 220;
    }

    private void OnEditorSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = e.NewSize.Width < NarrowWidth;
        Grid.SetColumn(LibraryBox, narrow ? 0 : 1);
        Grid.SetRow(LibraryBox, narrow ? 1 : 0);
        LibraryBox.Margin = narrow ? new Thickness(0, 8, 0, 6) : new Thickness(12, 0, 0, 6);
        LibraryColumn.Width = narrow ? new GridLength(0) : new GridLength(270);
    }

    private void OnLibraryItemPressed(object sender, MouseButtonEventArgs e) => _pressedAt = e.GetPosition(this);

    /// <summary>Starts dragging an effect from the box once the mouse has moved a little with the button down.</summary>
    private void OnLibraryItemMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { Tag: string key } item)
            return;
        Vector moved = e.GetPosition(this) - _pressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        DragDrop.DoDragDrop(item, new DataObject(EffectFormat, key), DragDropEffects.Copy);
        ShowDropTarget(false);
    }

    private void OnDragOverLook(object sender, DragEventArgs e)
    {
        bool effect = e.Data.GetDataPresent(EffectFormat);
        e.Effects = effect ? DragDropEffects.Copy : DragDropEffects.None;
        ShowDropTarget(effect);
        e.Handled = true;
    }

    private void OnDragLeaveLook(object sender, DragEventArgs e) => ShowDropTarget(false);

    private void OnDropOnLook(object sender, DragEventArgs e)
    {
        ShowDropTarget(false);
        if (e.Data.GetData(EffectFormat) is string key && DataContext is EffectsViewModel viewModel)
            viewModel.AddEffect(key);
    }

    private void ShowDropTarget(bool show) =>
        DropZone.BorderBrush = show ? (System.Windows.Media.Brush)FindResource("AccentFillColorDefaultBrush") : System.Windows.Media.Brushes.Transparent;
}
