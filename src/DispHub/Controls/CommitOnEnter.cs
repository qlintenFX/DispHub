using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DispHub.Controls;

/// <summary>
/// For text boxes bound with UpdateSourceTrigger=LostFocus: pressing Enter pushes the text to the view model too,
/// so the value is saved whether the user presses Enter or clicks away.
/// </summary>
public static class CommitOnEnter
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(CommitOnEnter), new PropertyMetadata(false, OnChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
            return;
        box.KeyDown -= OnKeyDown;
        if (e.NewValue is true)
            box.KeyDown += OnKeyDown;
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox box)
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
