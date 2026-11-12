// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows.Automation;
using System.Windows.Controls;
using DispHub.Controls;

namespace DispHub.Tests.App;

public sealed class SettingsCardTests
{
    [Fact]
    public void Control_IsNamedAfterTheRow() => OnSta(() =>
    {
        var toggle = new CheckBox();
        _ = new SettingsCard { Header = "Start with Windows", Description = "Starts quietly.", Content = toggle };
        Assert.Equal("Start with Windows", AutomationProperties.GetName(toggle));
        Assert.Equal("Starts quietly.", AutomationProperties.GetHelpText(toggle));
    });

    [Fact]
    public void Name_FollowsTheTitle() => OnSta(() =>
    {
        var combo = new ComboBox();
        var card = new SettingsCard { Header = "Theme", Content = combo };
        card.Header = "Thema";
        Assert.Equal("Thema", AutomationProperties.GetName(combo));
    });

    [Fact]
    public void NameFromXaml_Stays() => OnSta(() =>
    {
        var slider = new Slider();
        AutomationProperties.SetName(slider, "Message duration");
        var card = new SettingsCard { Header = "On-screen message", Content = slider };
        card.Header = "Something else";
        Assert.Equal("Message duration", AutomationProperties.GetName(slider));
    });

    [Fact]
    public void ControlWithWords_KeepsThemAndGetsTheTitleAsHelp() => OnSta(() =>
    {
        var button = new Button { Content = "Ctrl+Alt+F10" };
        _ = new SettingsCard { Header = "DispHub on or off", Description = "Like the power button.", Content = button };
        Assert.Equal("", AutomationProperties.GetName(button));
        Assert.Equal("DispHub on or off", AutomationProperties.GetHelpText(button));
    });

    [Fact]
    public void ButtonWithTranslatedWords_KeepsThem() => OnSta(() =>
    {
        // Translated text arrives through a binding that has no value yet when the card first sees the button.
        var button = new Button();
        button.SetBinding(ContentControl.ContentProperty, new System.Windows.Data.Binding("Missing"));
        _ = new SettingsCard { Header = "Schedule", Content = button };
        Assert.Equal("", AutomationProperties.GetName(button));
        Assert.Equal("Schedule", AutomationProperties.GetHelpText(button));
    });

    [Fact]
    public void ButtonThatGetsItsWordsLater_LosesTheGivenName() => OnSta(() =>
    {
        // XAML puts the button in the card first and sets its text afterwards; the card looks again when loaded.
        var button = new Button();
        var card = new SettingsCard { Header = "Schedule", Content = button };
        button.Content = "Set up";
        card.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent));
        Assert.Equal("", AutomationProperties.GetName(button));
        Assert.Equal("Schedule", AutomationProperties.GetHelpText(button));
    });

    private static void OnSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw failure;
    }
}
