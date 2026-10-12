using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace DispHub.Controls;

/// <summary>
/// A settings row, like Windows Settings: icon, title and description on the left, the control (the content) on
/// the right. When the row gets narrow the control moves under the text instead of squeezing it.
/// <see cref="Footer"/> holds anything that belongs under the row, such as a colour grid.
/// </summary>
public class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(null));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(SymbolRegular), typeof(SettingsCard), new PropertyMetadata(SymbolRegular.Empty));

    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(object), typeof(SettingsCard), new PropertyMetadata(null));

    public static readonly DependencyProperty WrapWidthProperty = DependencyProperty.Register(
        nameof(WrapWidth), typeof(double), typeof(SettingsCard), new PropertyMetadata(500.0));

    private static readonly DependencyPropertyKey IsWrappedKey = DependencyProperty.RegisterReadOnly(
        nameof(IsWrapped), typeof(bool), typeof(SettingsCard), new PropertyMetadata(false));

    public static readonly DependencyProperty IsWrappedProperty = IsWrappedKey.DependencyProperty;

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public SymbolRegular Icon
    {
        get => (SymbolRegular)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>Below this width the control moves under the text.</summary>
    public double WrapWidth
    {
        get => (double)GetValue(WrapWidthProperty);
        set => SetValue(WrapWidthProperty, value);
    }

    public bool IsWrapped => (bool)GetValue(IsWrappedProperty);

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        ArgumentNullException.ThrowIfNull(sizeInfo);
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            SetValue(IsWrappedKey, sizeInfo.NewSize.Width < WrapWidth);
    }
}

/// <summary>A section heading with optional actions (the content) on the right, aligned to the heading.</summary>
public class SectionHeader : ContentControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SectionHeader), new PropertyMetadata(""));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
