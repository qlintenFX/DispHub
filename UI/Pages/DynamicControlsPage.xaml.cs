using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using DisplayHub.Constants;
using DisplayHub.Models;
using DisplayHub.Services.Logging;
using DisplayHub.UI.Dialogs;

namespace DisplayHub.UI.Pages;

public partial class DynamicControlsPage : Page
{
    private readonly MainWindow mainWindow;
    private bool suppressSliderEvents;

    public DynamicControlsPage(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow;
        InitializeComponent();
        Loaded += Page_Loaded;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (mainWindow.DynamicControls != null)
        {
            mainWindow.DynamicControls.ValuesChanged += DynamicControls_ValuesChanged;
            DynamicToggle.IsChecked = mainWindow.DynamicControls.IsEnabled;
            UpdateSlidersFromDynamic();
        }
    }

    private void DynamicControls_ValuesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateSlidersFromDynamic);
    }

    private void UpdateSlidersFromDynamic()
    {
        if (mainWindow.DynamicControls == null) return;

        suppressSliderEvents = true;
        DynamicGammaSlider.Value = (int)(mainWindow.DynamicControls.Gamma * 100);
        DynamicContrastSlider.Value = (int)(mainWindow.DynamicControls.Contrast * 100);
        DynamicVibranceSlider.Value = mainWindow.DynamicControls.Vibrance;
        suppressSliderEvents = false;
        UpdateSliderLabels();
    }

    private void UpdateSliderLabels()
    {
        if (DynGammaText == null) return;
        DynGammaText.Text = $"{DynamicGammaSlider.Value / 100.0:F2}";
        DynContrastText.Text = $"{(int)DynamicContrastSlider.Value}%";
        DynVibranceText.Text = $"{(int)DynamicVibranceSlider.Value}%";
    }

    private void DynamicToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (mainWindow.DynamicControls == null || mainWindow.HotkeyManager == null || mainWindow.DisplayManager == null) return;

        bool isEnabled = DynamicToggle.IsChecked == true;

        try
        {
            Logger.Log($"Dynamic controls toggled: {isEnabled}");
            mainWindow.DynamicControls.IsEnabled = isEnabled;

            if (isEnabled)
            {
                mainWindow.HotkeyManager.UnregisterAllHotkeys();

                var helper = new WindowInteropHelper(Window.GetWindow(this)!);
                mainWindow.DynamicControls.RegisterHotkeys(mainWindow.HotkeyManager, helper.Handle);
                mainWindow.DynamicControls.SetValues(
                    mainWindow.DynamicControls.Gamma,
                    mainWindow.DynamicControls.Contrast,
                    mainWindow.DynamicControls.Vibrance);
            }
            else
            {
                mainWindow.DynamicControls.UnregisterHotkeys(mainWindow.HotkeyManager);
                mainWindow.RegisterAllHotkeys();

                if (mainWindow.CurrentProfile != null)
                    mainWindow.ApplyProfile(mainWindow.CurrentProfile);
                else
                    mainWindow.DisplayManager.ResetToDefault();
            }

            mainWindow.SettingsManager.SetDynamicControlsEnabled(isEnabled);
            mainWindow.UpdateTrayProfilesMenu();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to toggle dynamic controls", ex);
            System.Windows.MessageBox.Show($"Failed to toggle dynamic controls: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);

            DynamicToggle.IsChecked = mainWindow.DynamicControls.IsEnabled;
        }
    }

    private void DynamicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (suppressSliderEvents || mainWindow.DynamicControls == null) return;

        double gamma = DynamicGammaSlider.Value / 100.0;
        double contrast = DynamicContrastSlider.Value / 100.0;
        int vibrance = (int)DynamicVibranceSlider.Value;
        mainWindow.DynamicControls.SetValues(gamma, contrast, vibrance);
        UpdateSliderLabels();
    }

    private void DynamicReset_Click(object sender, RoutedEventArgs e)
    {
        mainWindow.DynamicControls?.SetValues(AppConstants.GammaDefault, AppConstants.ContrastDefault, AppConstants.VibranceDefault);
        UpdateSlidersFromDynamic();
    }

    private void SaveToProfile_Click(object sender, RoutedEventArgs e)
    {
        if (mainWindow.DynamicControls == null || mainWindow.ProfileManager == null || !mainWindow.DynamicControls.IsEnabled) return;

        string? name = ProfileNameDialog.Show(Window.GetWindow(this), "Dynamic Profile", "Save as Profile");
        if (name == null) return;

        var newProfile = new Profile(name, mainWindow.DynamicControls.Gamma,
            mainWindow.DynamicControls.Contrast, mainWindow.DynamicControls.Vibrance);
        mainWindow.ProfileManager.AddProfile(newProfile);

        System.Windows.MessageBox.Show($"Dynamic settings saved as profile '{name}'", "Profile Saved",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
