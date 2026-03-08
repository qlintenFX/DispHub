using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

public partial class DynamicControlsPage : Page, INavigationAware
{
    private bool _isLoaded = false;

    public DynamicControlsPage()
    {
        InitializeComponent();
        Loaded += DynamicControlsPage_Loaded;
    }

    private void DynamicControlsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        DynamicControlsToggle.IsChecked = MainWindow.DynamicControls.IsEnabled;
        DcActiveInfoBar.IsOpen = MainWindow.DynamicControls.IsEnabled;
        _isLoaded = true;

        UpdateValueDisplay();
        MainWindow.DynamicControls.ValuesChanged += OnValuesChanged;
    }

    public Task OnNavigatedToAsync()
    {
        UpdateValueDisplay();
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync()
    {
        MainWindow.DynamicControls.ValuesChanged -= OnValuesChanged;
        return Task.CompletedTask;
    }

    private void OnValuesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateValueDisplay);
    }

    private void UpdateValueDisplay()
    {
        GammaValueText.Text = MainWindow.DynamicControls.Gamma.ToString("F2");
        ContrastValueText.Text = $"{MainWindow.DynamicControls.Contrast * 100:F0}%";
        VibranceValueText.Text = MainWindow.DynamicControls.Vibrance.ToString();
    }

    private void DynamicControlsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        bool enabled = DynamicControlsToggle.IsChecked == true;
        MainWindow.DynamicControls.IsEnabled = enabled;
        MainWindow.SettingsManager.DynamicControlsEnabled = enabled;
        DcActiveInfoBar.IsOpen = enabled;

        if (enabled)
            MainWindow.DynamicControls.RegisterHotkeys(MainWindow.HotkeyManager);
        else
            MainWindow.DynamicControls.UnregisterHotkeys(MainWindow.HotkeyManager);
    }
}
