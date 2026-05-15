// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DispHub.Pages;

public partial class TaskbarWidgetPage : Page, INavigationAware
{
    private bool _isLoaded;

    public TaskbarWidgetPage()
    {
        InitializeComponent();
        Loaded += TaskbarWidgetPage_Loaded;
    }

    private void TaskbarWidgetPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        var sm = MainWindow.SettingsManager;
        WidgetEnabledToggle.IsChecked = sm.TaskbarWidgetEnabled;
        PositionComboBox.SelectedIndex = sm.TaskbarWidgetPosition;
        AutoPaddingToggle.IsChecked = sm.TaskbarWidgetAutoPadding;
        ManualPaddingBox.Value = sm.TaskbarWidgetManualPadding;
        ClickableToggle.IsChecked = sm.TaskbarWidgetClickable;
        BackgroundBlurToggle.IsChecked = sm.TaskbarWidgetBackgroundBlur;
        HideWhenInactiveToggle.IsChecked = sm.TaskbarWidgetHideWhenInactive;
        _isLoaded = true;
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void WidgetEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        bool enabled = WidgetEnabledToggle.IsChecked == true;
        MainWindow.SettingsManager.TaskbarWidgetEnabled = enabled;
        MainWindow.SetTaskbarWidgetEnabled(enabled);
    }

    private void Position_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.TaskbarWidgetPosition = PositionComboBox.SelectedIndex;
        MainWindow.RefreshTaskbarWidget();
    }

    private void AutoPadding_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.TaskbarWidgetAutoPadding = AutoPaddingToggle.IsChecked == true;
        MainWindow.RefreshTaskbarWidget();
    }

    private void ManualPadding_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        int val = (int)(ManualPaddingBox.Value ?? 0);
        MainWindow.SettingsManager.TaskbarWidgetManualPadding = val;
        MainWindow.RefreshTaskbarWidget();
    }

    private void Clickable_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.TaskbarWidgetClickable = ClickableToggle.IsChecked == true;
    }

    private void BackgroundBlur_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.TaskbarWidgetBackgroundBlur = BackgroundBlurToggle.IsChecked == true;
        MainWindow.UpdateTaskbarWidgetSettings();
    }

    private void HideWhenInactive_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.TaskbarWidgetHideWhenInactive = HideWhenInactiveToggle.IsChecked == true;
        MainWindow.UpdateTaskbarWidgetSettings();
    }
}
