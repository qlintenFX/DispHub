using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

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
        WidgetEnabledToggle.IsChecked = MainWindow.SettingsManager.TaskbarWidgetEnabled;
        PositionComboBox.SelectedIndex = MainWindow.SettingsManager.TaskbarWidgetPosition;
        PaddingSlider.Value = MainWindow.SettingsManager.TaskbarWidgetPadding;
        PaddingValueText.Text = MainWindow.SettingsManager.TaskbarWidgetPadding.ToString();
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

    private void Padding_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded || PaddingValueText == null) return;
        int val = (int)PaddingSlider.Value;
        PaddingValueText.Text = val.ToString();
        MainWindow.SettingsManager.TaskbarWidgetPadding = val;
        MainWindow.RefreshTaskbarWidget();
    }
}
