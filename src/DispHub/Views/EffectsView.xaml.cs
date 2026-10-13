using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DispHub.Controls;
using DispHub.ViewModels;

namespace DispHub.Views;

public partial class EffectsView : UserControl
{
    /// <summary>Below this width the look's name and actions go under the preview, and the effects box under the look.</summary>
    private const double NarrowWidth = 640;

    /// <summary>The clipboard-style format an effect is dragged in: its key.</summary>
    private const string EffectFormat = "DispHub.Effect";

    private static readonly TimeSpan GlideDuration = TimeSpan.FromMilliseconds(260);

    private readonly ScrollMotion _stripMotion = new();
    private readonly FrameClock _stripClock = new();
    private bool _stripGliding;
    private Point _pressedAt;

    public EffectsView()
    {
        InitializeComponent();
        LookList.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(ShowSelectedLook, System.Windows.Threading.DispatcherPriority.Loaded);
        Strip.PreviewMouseWheel += OnStripWheel;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
                Dispatcher.BeginInvoke(ShowSelectedLook, System.Windows.Threading.DispatcherPriority.Loaded);
        };
        Unloaded += (_, _) => StopStripGlide();
    }

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

    // The row of looks.

    private void OnStripBack(object sender, RoutedEventArgs e) => GlideStripBy(-Strip.ViewportWidth * 0.8);

    private void OnStripForward(object sender, RoutedEventArgs e) => GlideStripBy(Strip.ViewportWidth * 0.8);

    /// <summary>The page keeps the wheel; Shift and the wheel, or a touchpad sideways, move the row.</summary>
    private void OnStripWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            return;
        GlideStripBy(-e.Delta);
        e.Handled = true;
    }

    private void OnStripScrolled(object sender, ScrollChangedEventArgs e)
    {
        // Disabled rather than hidden at the ends, so nothing moves.
        StripBack.IsEnabled = Strip.HorizontalOffset > 0.5;
        StripForward.IsEnabled = Strip.HorizontalOffset < Strip.ScrollableWidth - 0.5;
    }

    /// <summary>Brings the selected look into the row's view, without moving the page.</summary>
    private void ShowSelectedLook()
    {
        if (LookList.SelectedItem is null || LookList.ItemContainerGenerator.ContainerFromItem(LookList.SelectedItem) is not FrameworkElement item || !item.IsLoaded)
            return;
        double left = item.TransformToAncestor(LookList).Transform(default).X;
        double right = left + item.ActualWidth;
        if (left < Strip.HorizontalOffset)
            GlideStripTo(left);
        else if (right > Strip.HorizontalOffset + Strip.ViewportWidth)
            GlideStripTo(right - Strip.ViewportWidth);
    }

    private void GlideStripBy(double distance)
    {
        if (!_stripGliding)
            _stripMotion.JumpTo(Strip.HorizontalOffset);
        GlideStripTo(_stripMotion.Target + distance);
    }

    private void GlideStripTo(double target)
    {
        target = Math.Clamp(target, 0, Strip.ScrollableWidth);
        if (!_stripGliding)
            _stripMotion.JumpTo(Strip.HorizontalOffset);
        bool animate = SystemParameters.ClientAreaAnimation && !SmoothScrollViewer.ReduceMotion;
        _stripMotion.AnimateTo(target, animate ? GlideDuration : TimeSpan.Zero);
        if (!_stripMotion.IsMoving)
        {
            StopStripGlide();
            Strip.ScrollToHorizontalOffset(target);
            return;
        }
        if (_stripGliding)
            return;
        _stripGliding = true;
        _stripClock.Restart();
        CompositionTarget.Rendering += OnStripFrame;
    }

    private void OnStripFrame(object? sender, EventArgs e)
    {
        TimeSpan step = _stripClock.Tick(((RenderingEventArgs)e).RenderingTime);
        if (step == TimeSpan.Zero)
            return;
        _stripMotion.Advance(step);
        Strip.ScrollToHorizontalOffset(_stripMotion.Position);
        if (!_stripMotion.IsMoving)
            StopStripGlide();
    }

    private void StopStripGlide()
    {
        if (!_stripGliding)
            return;
        _stripGliding = false;
        CompositionTarget.Rendering -= OnStripFrame;
    }

    // Adding effects: drag one from the box onto the look, or click it.

    private void OnLibraryItemPressed(object sender, MouseButtonEventArgs e) => _pressedAt = e.GetPosition(this);

    /// <summary>Starts dragging an effect from the box once the mouse has moved a little with the button down.</summary>
    private void OnLibraryItemMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { Tag: string key } item || !item.IsEnabled)
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
        DropZone.BorderBrush = show ? (Brush)FindResource("AccentFillColorDefaultBrush") : Brushes.Transparent;
}
