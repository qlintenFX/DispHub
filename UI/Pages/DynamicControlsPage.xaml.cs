using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using DisplayHub.Constants;
using DisplayHub.Models;
using DisplayHub.Services.Logging;
using DisplayHub.UI.Dialogs;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.UI.Pages;

public partial class DynamicControlsPage : Page, INavigationAware
{
    private MainWindow MainWindow => (System.Windows.Application.Current.MainWindow as MainWindow)!;
    private bool suppressSliderEvents;
    private bool subscribed;

    public DynamicControlsPage()
    {
        InitializeComponent();
    }

    public Task OnNavigatedToAsync()
    {
        var mw = MainWindow;
        if (mw.DynamicControls != null && !subscribed)
        {
            mw.DynamicControls.ValuesChanged += DynamicControls_ValuesChanged;
            subscribed = true;
        }

        if (mw.DynamicControls != null)
        {
            DynamicToggle.IsChecked = mw.DynamicControls.IsEnabled;
            UpdateSlidersFromDynamic();
        }
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void DynamicControls_ValuesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateSlidersFromDynamic);
    }

    private void UpdateSlidersFromDynamic()
    {
        var dc = MainWindow.DynamicControls;
        if (dc == null) return;

        suppressSliderEvents = true;
        DynamicGammaSlider.Value = (int)(dc.Gamma * 100);
        DynamicContrastSlider.Value = (int)(dc.Contrast * 100);
        DynamicVibranceSlider.Value = dc.Vibrance;
        suppressSliderEvents = false;
        UpdateSliderLabels();
    }

    private void UpdateSliderLabels()
    {
        if (DynGammaText == null || DynContrastText == null || DynVibranceText == null) return;
        DynGammaText.Text = $"{DynamicGammaSlider.Value / 100.0:F2}";
        DynContrastText.Text = $"{(int)DynamicContrastSlider.Value}%";
        DynVibranceText.Text = $"{(int)DynamicVibranceSlider.Value}%";
    }

    private void DynamicToggle_Changed(object sender, RoutedEventArgs e)
    {
        var mw = MainWindow;
        if (mw.DynamicControls == null || mw.HotkeyManager == null || mw.DisplayManager == null) return;

        bool isEnabled = DynamicToggle.IsChecked == true;

        try
        {
            Logger.Log($"Dynamic controls toggled: {isEnabled}");
            mw.DynamicControls.IsEnabled = isEnabled;

            if (isEnabled)
            {
                mw.HotkeyManager.UnregisterAllHotkeys();
                var helper = new WindowInteropHelper(Window.GetWindow(this)!);
                mw.DynamicControls.RegisterHotkeys(mw.HotkeyManager, helper.Handle);
                mw.DynamicControls.SetValues(mw.DynamicControls.Gamma, mw.DynamicControls.Contrast, mw.DynamicControls.Vibrance);
            }
            else
            {
                mw.DynamicControls.UnregisterHotkeys(mw.HotkeyManager);
                mw.RegisterAllHotkeys();

                if (mw.CurrentProfile != null)
                    mw.ApplyProfile(mw.CurrentProfile);
                else
                    mw.DisplayManager.ResetToDefault();
            }

            mw.SettingsManager.SetDynamicControlsEnabled(isEnabled);
            mw.UpdateTrayProfilesMenu();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to toggle dynamic controls", ex);
            System.Windows.MessageBox.Show($"Failed to toggle dynamic controls: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            DynamicToggle.IsChecked = mw.DynamicControls.IsEnabled;
        }
    }

    private void DynamicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var dc = MainWindow.DynamicControls;
        if (suppressSliderEvents || dc == null) return;

        dc.SetValues(
            DynamicGammaSlider.Value / 100.0,
            DynamicContrastSlider.Value / 100.0,
            (int)DynamicVibranceSlider.Value);
        UpdateSliderLabels();
    }

    private void DynamicReset_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.DynamicControls?.SetValues(AppConstants.GammaDefault, AppConstants.ContrastDefault, AppConstants.VibranceDefault);
        UpdateSlidersFromDynamic();
    }

    private void SaveToProfile_Click(object sender, RoutedEventArgs e)
    {
        var mw = MainWindow;
        if (mw.DynamicControls == null || mw.ProfileManager == null || !mw.DynamicControls.IsEnabled) return;

        string? name = ProfileNameDialog.Show(Window.GetWindow(this), "Dynamic Profile", "Save as Profile");
        if (name == null) return;

        var newProfile = new Profile(name, mw.DynamicControls.Gamma, mw.DynamicControls.Contrast, mw.DynamicControls.Vibrance);
        mw.ProfileManager.AddProfile(newProfile);

        System.Windows.MessageBox.Show($"Dynamic settings saved as profile '{name}'.",
            "Profile Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
