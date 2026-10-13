using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DispHub.Controls;

/// <summary>
/// Makes every slider work like the ones in Windows Settings: pressing anywhere on the track moves the knob there
/// and keeps it under the pointer while dragging. WPF either jumps a page at a time (the default) or, with
/// IsMoveToPointEnabled, moves the knob but drops the drag, so a press and drag on the track did nothing more.
/// </summary>
public static class SliderDragFix
{
    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(Slider), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
        // After WPF's own handler has moved the knob (it marks the event handled), hand the press on to the knob.
        EventManager.RegisterClassHandler(typeof(Slider), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPressed), handledEventsToo: true);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) =>
        ((Slider)sender).SetCurrentValue(Slider.IsMoveToPointEnabledProperty, true);

    private static void OnPressed(object sender, MouseButtonEventArgs e)
    {
        var slider = (Slider)sender;
        if (slider.Template?.FindName("PART_Track", slider) is not Track { Thumb: { } thumb } || thumb.IsMouseOver || !slider.IsEnabled)
            return;
        thumb.UpdateLayout();
        thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = thumb,
        });
    }
}
