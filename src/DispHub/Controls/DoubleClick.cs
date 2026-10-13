using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DispHub.Controls;

/// <summary>
/// Runs a command when a control is double-clicked, e.g. a slider back to its default, like in photo editors.
/// Works even when the control handles the clicks itself, as a slider's knob does.
/// </summary>
public static class DoubleClick
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(DoubleClick), new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Control control)
            return;
        control.PreviewMouseDoubleClick -= OnDoubleClick;
        if (e.NewValue is not null)
            control.PreviewMouseDoubleClick += OnDoubleClick;
    }

    private static void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && GetCommand((DependencyObject)sender) is { } command && command.CanExecute(null))
            command.Execute(null);
    }
}
