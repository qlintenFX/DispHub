using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

public partial class DynamicControlsPage : Page, INavigationAware
{
    private bool _isLoaded;

    public DynamicControlsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        DynamicControlsToggle.IsChecked = MainWindow.DynamicControls.IsEnabled;
        DcActiveInfoBar.IsOpen = MainWindow.DynamicControls.IsEnabled;
        _isLoaded = true;

        UpdateValueDisplay();
        MainWindow.DynamicControls.ValuesChanged += OnValuesChanged;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        MainWindow.DynamicControls.ValuesChanged -= OnValuesChanged;
    }

    public Task OnNavigatedToAsync()
    {
        if (_isLoaded)
            UpdateValueDisplay();
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void OnValuesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateValueDisplay);
    }

    private void UpdateValueDisplay()
    {
        var dc = MainWindow.DynamicControls;
        GammaValueText.Text = dc.Gamma.ToString("F2");
        ContrastValueText.Text = $"{dc.Contrast * 100:F0}%";
        VibranceValueText.Text = dc.Vibrance.ToString();
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
