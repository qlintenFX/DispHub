// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Models;
using DispHub.Services.Logging;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DispHub.Pages;

public partial class ProfilesPage : Page, INavigationAware
{
    private bool _isLoaded;
    private int _selectedIndex;

    public ProfilesPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        _selectedIndex = 0;
        RefreshProfileCards();
        LoadSelectedProfile();
        SyncDynamicControlsState();
        SyncPowerState();

        MainWindow.ActiveProfileChanged += OnActiveProfileChanged;
        MainWindow.DisplayPowerChanged += OnPowerChanged;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        MainWindow.ActiveProfileChanged -= OnActiveProfileChanged;
        MainWindow.DisplayPowerChanged -= OnPowerChanged;
    }

    public Task OnNavigatedToAsync()
    {
        if (_isLoaded)
        {
            RefreshProfileCards();
            SyncDynamicControlsState();
            SyncPowerState();
        }
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    /// <summary>
    /// &lt;summary&gt;Handles hotkey-driven profile switches, fired from MainWindow.&lt;/summary&gt;
    /// </summary>

    private void OnActiveProfileChanged(int profileIndex)
    {
        Dispatcher.Invoke(() =>
        {
            if (profileIndex < 0 || profileIndex >= MainWindow.ProfileManager.Profiles.Count)
                return;

            _selectedIndex = profileIndex;
            LoadSelectedProfile();
            RefreshProfileCards();
        });
    }

    // ── Dynamic-controls mutual exclusion ──

    private void SyncDynamicControlsState()
    {
        bool dcActive = MainWindow.DynamicControls.IsEnabled;
        DcWarningBar.IsOpen = dcActive;
        bool canEdit = !dcActive && MainWindow.IsDisplayActive;
        SlidersPanel.IsEnabled = canEdit;
        ActionButtonsPanel.IsEnabled = canEdit;
        ProfileCardPanel.IsEnabled = canEdit;
    }

    // ── Power state ──

    private void SyncPowerState()
    {
        bool powerOff = !MainWindow.IsDisplayActive;
        PowerOffBar.IsOpen = powerOff;
        bool canEdit = !powerOff && !MainWindow.DynamicControls.IsEnabled;
        SlidersPanel.IsEnabled = canEdit;
        ActionButtonsPanel.IsEnabled = canEdit;
        ProfileCardPanel.IsEnabled = canEdit;
    }

    private void OnPowerChanged(bool active)
    {
        Dispatcher.Invoke(() =>
        {
            SyncPowerState();
            SyncDynamicControlsState();
            RefreshProfileCards();
        });
    }

    // ── Profile card rendering ──

    private void RefreshProfileCards()
    {
        ProfileCardPanel.Children.Clear();
        var profiles = MainWindow.ProfileManager.Profiles;
        bool isDisabled = !MainWindow.IsDisplayActive;

        for (int i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            bool isSelected = i == _selectedIndex;

            var content = new StackPanel { MinWidth = 100 };
            content.Children.Add(new TextBlock
            {
                Text = profile.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            });

            if (profile.HotKeyValue != 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = profile.HotkeyDisplayText,
                    FontSize = 11,
                    Opacity = 0.6,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            int index = i;

            // When DispHub is disabled, all cards use Secondary appearance
            // but the active card gets a left accent border to indicate it will resume
            var appearance = isSelected && !isDisabled
                ? Wpf.Ui.Controls.ControlAppearance.Primary
                : Wpf.Ui.Controls.ControlAppearance.Secondary;

            var card = new Wpf.Ui.Controls.Button
            {
                Content = content,
                Appearance = appearance,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(16, 10, 16, 10),
                Tag = index,
            };

            // Active card when disabled: accent left border to show "will resume"
            if (isSelected && isDisabled)
            {
                card.BorderThickness = new Thickness(3, 1, 1, 1);
                card.BorderBrush = (System.Windows.Media.Brush)FindResource("SystemAccentColorPrimaryBrush");
                card.Opacity = 0.7;
            }

            card.Click += OnProfileCardClicked;
            ProfileCardPanel.Children.Add(card);
        }
    }

    // ── Card click → select + apply ──

    private void OnProfileCardClicked(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        if (sender is not Wpf.Ui.Controls.Button { Tag: int index }) return;

        _selectedIndex = index;
        LoadSelectedProfile();
        MainWindow.ApplyProfile(index);
        RefreshProfileCards();
    }

    // ── Slider ↔ profile synchronisation ──

    private void LoadSelectedProfile()
    {
        var profiles = MainWindow.ProfileManager.Profiles;
        if (_selectedIndex < 0 || _selectedIndex >= profiles.Count) return;

        var profile = profiles[_selectedIndex];

        // Suppress slider-change handlers while we set programmatic values
        _isLoaded = false;
        GammaSlider.Value = profile.Gamma;
        ContrastSlider.Value = profile.Contrast;
        VibranceSlider.Value = profile.Vibrance;
        ColorTempSlider.Value = profile.ColorTemperature;
        _isLoaded = true;

        UpdateSliderLabels();
    }

    private void UpdateSliderLabels()
    {
        GammaValueText.Text = GammaSlider.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        ContrastValueText.Text = $"{ContrastSlider.Value * 100:F0}%";
        VibranceValueText.Text = ((int)VibranceSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        int temp = (int)ColorTempSlider.Value;
        string warmthLabel = temp < 40 ? "Warm" : "Neutral";
        ColorTempValueText.Text = temp > 60 ? "Cool" : warmthLabel;

        int kelvin = temp <= Constants.AppConstants.ColorTempNeutralValue
            ? Constants.AppConstants.ColorTempMinKelvin +
              (int)Math.Round((temp / (double)Constants.AppConstants.ColorTempNeutralValue) *
                              (Constants.AppConstants.ColorTempNeutralKelvin - Constants.AppConstants.ColorTempMinKelvin))
            : Constants.AppConstants.ColorTempNeutralKelvin +
              (int)Math.Round(((temp - Constants.AppConstants.ColorTempNeutralValue) /
                               (double)(Constants.AppConstants.ColorTempMax - Constants.AppConstants.ColorTempNeutralValue)) *
                              (Constants.AppConstants.ColorTempMaxKelvin - Constants.AppConstants.ColorTempNeutralKelvin));

        ColorTempKelvinText.Text = $"{kelvin.ToString(System.Globalization.CultureInfo.InvariantCulture)} K";
    }

    private void ApplyCurrentSliderValues()
    {
        MainWindow.DisplayManager.ApplySettings(
            GammaSlider.Value, ContrastSlider.Value, (int)VibranceSlider.Value, (int)ColorTempSlider.Value);

        var profiles = MainWindow.ProfileManager.Profiles;
        if (_selectedIndex >= 0 && _selectedIndex < profiles.Count)
            Logger.Log($"Applied profile: {profiles[_selectedIndex].Name}");
    }

    // Slider ValueChanged handlers all perform the same logic:
    // re-render labels and push values to the display manager.

    private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        OnAnySliderValueChanged();

    private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        OnAnySliderValueChanged();

    private void VibranceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        OnAnySliderValueChanged();

    private void ColorTempSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        OnAnySliderValueChanged();

    private void OnAnySliderValueChanged()
    {
        if (!_isLoaded) return;
        UpdateSliderLabels();
        ApplyCurrentSliderValues();
    }

    // ── Action buttons ──

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        var profiles = MainWindow.ProfileManager.Profiles;
        if (_selectedIndex < 0 || _selectedIndex >= profiles.Count) return;

        var existing = profiles[_selectedIndex];
        var updated = new Profile(existing.Name, GammaSlider.Value, ContrastSlider.Value, (int)VibranceSlider.Value, (int)ColorTempSlider.Value)
        {
            HotKeyValue = existing.HotKeyValue,
            HotKeyModifierValue = existing.HotKeyModifierValue,
            HotkeyId = existing.HotkeyId
        };

        MainWindow.ProfileManager.UpdateProfile(_selectedIndex, updated);
        Logger.Log($"Saved profile: {updated.Name}");
        RefreshProfileCards();
    }

    private void SetHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        var profiles = MainWindow.ProfileManager.Profiles;
        if (_selectedIndex < 0 || _selectedIndex >= profiles.Count) return;

        var dialog = new HotkeyDialog { Owner = Window.GetWindow(this) };
        if (!(dialog.ShowDialog() ?? false)) return;

        var profile = profiles[_selectedIndex];

        // Unregister old hotkey (only if profile mode is active and hotkey was registered)
        if (profile.HotkeyId > 0)
            MainWindow.HotkeyManager.UnregisterHotkey(profile.HotkeyId);

        profile.HotKeyValue = dialog.VirtualKeyCode;
        profile.HotKeyModifierValue = dialog.Modifiers;
        MainWindow.ProfileManager.SaveProfiles();

        // Only register if profile mode is active (DC mode doesn't register profile hotkeys)
        if (profile.HotKeyValue != 0 && !MainWindow.DynamicControls.IsEnabled)
            MainWindow.HotkeyManager.RegisterHotkey(profile);

        RefreshProfileCards();
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;

        var profile = new Profile($"Profile {MainWindow.ProfileManager.Profiles.Count + 1}");
        MainWindow.ProfileManager.AddProfile(profile);
        _selectedIndex = MainWindow.ProfileManager.Profiles.Count - 1;

        LoadSelectedProfile();
        ApplyCurrentSliderValues();
        RefreshProfileCards();
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        var profiles = MainWindow.ProfileManager.Profiles;
        if (_selectedIndex < 0 || _selectedIndex >= profiles.Count) return;

        var profile = profiles[_selectedIndex];
        if (profile.HotkeyId > 0)
            MainWindow.HotkeyManager.UnregisterHotkey(profile.HotkeyId);

        MainWindow.ProfileManager.RemoveProfile(_selectedIndex);

        if (profiles.Count > 0)
        {
            _selectedIndex = Math.Min(_selectedIndex, profiles.Count - 1);
            LoadSelectedProfile();
            ApplyCurrentSliderValues();
        }
        RefreshProfileCards();
    }

    private void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;

        _isLoaded = false;
        GammaSlider.Value = Constants.AppConstants.GammaDefault;
        ContrastSlider.Value = Constants.AppConstants.ContrastDefault;
        VibranceSlider.Value = Constants.AppConstants.VibranceDefault;
        ColorTempSlider.Value = Constants.AppConstants.ColorTempDefault;
        _isLoaded = true;

        UpdateSliderLabels();
        MainWindow.DisplayManager.ApplySettings(
            Constants.AppConstants.GammaDefault,
            Constants.AppConstants.ContrastDefault,
            Constants.AppConstants.VibranceDefault,
            Constants.AppConstants.ColorTempDefault);
        Logger.Log("Reset to default values");
    }

    private void GammaReset_Click(object sender, RoutedEventArgs e) =>
        GammaSlider.Value = Constants.AppConstants.GammaDefault;

    private void ContrastReset_Click(object sender, RoutedEventArgs e) =>
        ContrastSlider.Value = Constants.AppConstants.ContrastDefault;

    private void VibranceReset_Click(object sender, RoutedEventArgs e) =>
        VibranceSlider.Value = Constants.AppConstants.VibranceDefault;

    private void ColorTempReset_Click(object sender, RoutedEventArgs e) =>
        ColorTempSlider.Value = Constants.AppConstants.ColorTempDefault;

    // Right-click on slider or label resets to default.
    // Each pair (slider + label) shares a common reset target.

    private void GammaSlider_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ResetSliderOnRightClick(GammaSlider, Constants.AppConstants.GammaDefault, e);

    private void GammaLabel_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ResetSliderOnRightClick(GammaSlider, Constants.AppConstants.GammaDefault, e);

    private void ContrastSlider_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ResetSliderOnRightClick(ContrastSlider, Constants.AppConstants.ContrastDefault, e);

    private void ContrastLabel_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ResetSliderOnRightClick(ContrastSlider, Constants.AppConstants.ContrastDefault, e);

    private void VibranceSlider_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ResetSliderOnRightClick(VibranceSlider, Constants.AppConstants.VibranceDefault, e);

    private void VibranceLabel_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ResetSliderOnRightClick(VibranceSlider, Constants.AppConstants.VibranceDefault, e);

    private void ColorTempSlider_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ResetSliderOnRightClick(ColorTempSlider, Constants.AppConstants.ColorTempDefault, e);

    private void ColorTempLabel_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        ResetSliderOnRightClick(ColorTempSlider, Constants.AppConstants.ColorTempDefault, e);

    private static void ResetSliderOnRightClick(Slider slider, double defaultValue, System.Windows.Input.MouseButtonEventArgs e)
    {
        slider.Value = defaultValue;
        e.Handled = true;
    }
}
