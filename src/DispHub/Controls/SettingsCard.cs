// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace DispHub.Controls;

/// <summary>
/// A settings row, like Windows Settings: icon, title and description on the left, the control (the content) on
/// the right. When the row gets narrow the control moves under the text instead of squeezing it.
/// <see cref="Footer"/> holds anything that belongs under the row, such as a colour grid.
/// </summary>
/// <remarks>
/// Screen readers can't reach the title and description (text inside a control template isn't in the automation
/// tree), so the control on the right carries them: the title becomes its name and the description its help text.
/// A control that shows words of its own, like a button or a hotkey box, keeps them as its name and gets the title
/// as help text. A name set in XAML always wins.
/// </remarks>
public class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata("", OnTextChanged));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(null, OnTextChanged));

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

    // What this card last gave its control, so a later change to the title replaces it but a name from XAML stays.
    private string? _givenName;
    private string? _givenHelp;

    public SettingsCard()
    {
        // XAML hands the control to the card before setting the control's own text, so look again once loaded.
        Loaded += (_, _) => LabelContent();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        ArgumentNullException.ThrowIfNull(sizeInfo);
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            SetValue(IsWrappedKey, sizeInfo.NewSize.Width < WrapWidth);
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        _givenName = _givenHelp = null;
        LabelContent();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SettingsCard)d).LabelContent();

    private void LabelContent()
    {
        if (Content is not Control control)
            return;
        // Translated words arrive through a binding, which may not have given the button its text yet.
        bool showsWords = control is ContentControl content &&
            (content.Content is string { Length: > 0 } || BindingOperations.IsDataBound(content, ContentControl.ContentProperty));
        if (showsWords)
        {
            _givenName = Give(control, AutomationProperties.NameProperty, null, _givenName);
            _givenHelp = Give(control, AutomationProperties.HelpTextProperty, Header, _givenHelp);
        }
        else
        {
            _givenName = Give(control, AutomationProperties.NameProperty, Header, _givenName);
            _givenHelp = Give(control, AutomationProperties.HelpTextProperty, Description, _givenHelp);
        }
    }

    /// <summary>Sets the text unless the control already has one that this card didn't give it.</summary>
    private static string? Give(Control control, DependencyProperty property, string? text, string? given)
    {
        object current = control.ReadLocalValue(property);
        if (current != DependencyProperty.UnsetValue && !Equals(current, given))
            return given;
        if (string.IsNullOrEmpty(text))
        {
            control.ClearValue(property);
            return null;
        }
        control.SetValue(property, text);
        return text;
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
