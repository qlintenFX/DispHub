// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DispHub.Pages;

public partial class FlyoutPage : Page, INavigationAware
{
    private bool _isLoaded;

    public FlyoutPage()
    {
        InitializeComponent();
        Loaded += FlyoutPage_Loaded;
    }

    private void FlyoutPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        FlyoutEnabledToggle.IsChecked = MainWindow.SettingsManager.FlyoutEnabled;
        DurationTextBox.Text = MainWindow.SettingsManager.FlyoutDuration.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _isLoaded = true;
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void FlyoutEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.FlyoutEnabled = FlyoutEnabledToggle.IsChecked ?? false;
    }

    private void Duration_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isLoaded) return;
        if (int.TryParse(DurationTextBox.Text, System.Globalization.CultureInfo.InvariantCulture, out int ms) && ms >= 200 && ms <= 10000)
            MainWindow.SettingsManager.FlyoutDuration = ms;
    }
}
