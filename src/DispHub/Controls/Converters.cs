using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DispHub.Controls;

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public static InverseBoolToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ActiveTextConverter : IValueConverter
{
    public static ActiveTextConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "On" : "Paused";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Shows enum values as words: "ShowProfileMenu" becomes "Show profile menu".</summary>
public sealed class EnumWordsConverter : IValueConverter
{
    public static EnumWordsConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string text = value?.ToString() ?? "";
        var words = new System.Text.StringBuilder(text.Length + 4);
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]))
                words.Append(' ').Append(char.ToLowerInvariant(text[i]));
            else
                words.Append(text[i]);
        }
        return words.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToCollapsedConverter : IValueConverter
{
    public static NullToCollapsedConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
