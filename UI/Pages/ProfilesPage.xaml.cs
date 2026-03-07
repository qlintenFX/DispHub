using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DisplayHub.Constants;
using DisplayHub.Models;
using DisplayHub.Services.Logging;
using DisplayHub.UI.Dialogs;

namespace DisplayHub.UI.Pages;

public partial class ProfilesPage : Page
{
    private readonly MainWindow mainWindow;
    private bool suppressSliderEvents;
    private bool isInitializing;

    public ProfilesPage(MainWindow mainWindow)
    {
        this.mainWindow = mainWindow;
        InitializeComponent();
        Loaded += ProfilesPage_Loaded;
    }

    private void ProfilesPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadProfilesToUI();
        UpdateSliderLabels();
    }

    private void LoadProfilesToUI()
    {
        if (mainWindow.ProfileManager == null) return;

        isInitializing = true;
        try
        {
            var selectedProfile = ProfileListBox.SelectedItem as Profile;
            ProfileListBox.Items.Clear();

            foreach (Profile profile in mainWindow.ProfileManager.Profiles)
                ProfileListBox.Items.Add(profile);

            if (selectedProfile != null && ProfileListBox.Items.Contains(selectedProfile))
                ProfileListBox.SelectedItem = selectedProfile;
            else if (ProfileListBox.Items.Count > 0)
                ProfileListBox.SelectedIndex = 0;
        }
        finally
        {
            isInitializing = false;
        }

        mainWindow.UpdateTrayProfilesMenu();
    }

    internal void OnProfileApplied(Profile profile)
    {
        if (ProfileListBox.Items.Contains(profile))
            ProfileListBox.SelectedItem = profile;

        suppressSliderEvents = true;
        GammaSlider.Value = (int)(profile.Gamma * 100);
        ContrastSlider.Value = (int)(profile.Contrast * 100);
        VibranceSlider.Value = profile.Vibrance;
        suppressSliderEvents = false;
        UpdateSliderLabels();
    }

    // ─── Profile List Events ───────────────────────────────────────────

    private void ProfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileListBox.SelectedItem is not Profile selected) return;

        // Load selected profile's values into sliders (no display change)
        suppressSliderEvents = true;
        GammaSlider.Value = (int)(selected.Gamma * 100);
        ContrastSlider.Value = (int)(selected.Contrast * 100);
        VibranceSlider.Value = Math.Clamp(selected.Vibrance, 0, 100);
        suppressSliderEvents = false;
        UpdateSliderLabels();

        HotkeyLabel.Text = string.IsNullOrEmpty(selected.HotkeyDisplayText)
            ? "Hotkey: None"
            : $"Hotkey: {selected.HotkeyDisplayText}";

        // Only auto-apply during normal interaction, not during initialization/reload
        if (!isInitializing)
            mainWindow.ApplyProfile(selected);
    }

    private void ProfileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ApplySelectedProfile();
    }

    // ─── Slider Events ─────────────────────────────────────────────────

    private void ProfileSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (suppressSliderEvents) return;

        UpdateSliderLabels();

        // Live-preview changes without committing to the profile
        if (mainWindow.DisplayManager != null)
        {
            double gamma = GammaSlider.Value / 100.0;
            double contrast = ContrastSlider.Value / 100.0;
            mainWindow.DisplayManager.ApplySettings(gamma, contrast, (int)VibranceSlider.Value);
        }
    }

    private void UpdateSliderLabels()
    {
        if (GammaValueText == null) return;
        GammaValueText.Text = $"{GammaSlider.Value / 100.0:F2}";
        ContrastValueText.Text = $"{(int)ContrastSlider.Value}%";
        VibranceValueText.Text = $"{(int)VibranceSlider.Value}%";
    }

    // ─── Button Events ─────────────────────────────────────────────────

    private void ApplyProfile_Click(object sender, RoutedEventArgs e)
    {
        ApplySelectedProfile();
    }

    private void ApplySelectedProfile()
    {
        if (ProfileListBox.SelectedItem is not Profile selected) return;
        mainWindow.ApplyProfile(selected);
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        if (mainWindow.ProfileManager == null) return;

        string? name = ProfileNameDialog.Show(Window.GetWindow(this));
        if (name == null) return;

        double gamma = GammaSlider.Value / 100.0;
        double contrast = ContrastSlider.Value / 100.0;
        var newProfile = new Profile(name, gamma, contrast, (int)VibranceSlider.Value);
        mainWindow.ProfileManager.AddProfile(newProfile);

        LoadProfilesToUI();
        ProfileListBox.SelectedItem = newProfile;
    }

    private void UpdateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (mainWindow.ProfileManager == null || ProfileListBox.SelectedItem is not Profile selected) return;

        selected.Gamma = GammaSlider.Value / 100.0;
        selected.Contrast = ContrastSlider.Value / 100.0;
        selected.Vibrance = (int)VibranceSlider.Value;
        mainWindow.ProfileManager.SaveProfiles();

        int selectedIndex = ProfileListBox.SelectedIndex;
        LoadProfilesToUI();
        if (selectedIndex < ProfileListBox.Items.Count)
            ProfileListBox.SelectedIndex = selectedIndex;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (mainWindow.ProfileManager == null || ProfileListBox.SelectedItem is not Profile selected) return;

        var result = System.Windows.MessageBox.Show(
            $"Delete profile '{selected.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (selected.HotkeyId > 0)
            mainWindow.HotkeyManager?.UnregisterHotkey(selected.HotkeyId);

        int index = ProfileListBox.SelectedIndex;
        mainWindow.ProfileManager.RemoveProfile(index);
        LoadProfilesToUI();

        if (index >= ProfileListBox.Items.Count)
            index = ProfileListBox.Items.Count - 1;
        if (index >= 0)
            ProfileListBox.SelectedIndex = index;
    }

    private void SetHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileListBox.SelectedItem is not Profile selected) return;
        if (mainWindow.HotkeyManager == null || mainWindow.ProfileManager == null) return;

        var dialog = new HotkeyDialog(selected.HotKey, selected.HotKeyModifier) { Owner = Window.GetWindow(this) };

        if (dialog.ShowDialog() != true) return;

        int vkCode = dialog.ResultVkCode;
        int modifiers = dialog.ResultModifiers;

        if (vkCode == 0) return;

        if (selected.HotkeyId > 0)
            mainWindow.HotkeyManager.UnregisterHotkey(selected.HotkeyId);

        selected.HotKey = vkCode;
        selected.HotKeyModifier = modifiers;

        int id = mainWindow.HotkeyManager.RegisterHotkey(selected);
        if (id > 0)
        {
            selected.HotkeyId = id;
            mainWindow.ProfileManager.SaveProfiles();
            HotkeyLabel.Text = $"Hotkey: {selected.HotkeyDisplayText}";

            int selectedIndex = ProfileListBox.SelectedIndex;
            LoadProfilesToUI();
            ProfileListBox.SelectedIndex = selectedIndex;
        }
        else
        {
            System.Windows.MessageBox.Show(
                "Failed to register the hotkey. It may be in use by another application.",
                "Hotkey Registration Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (mainWindow.DisplayManager == null) return;

        mainWindow.DisplayManager.ResetToDefault();
        suppressSliderEvents = true;
        GammaSlider.Value = 100;
        ContrastSlider.Value = 50;
        VibranceSlider.Value = 50;
        suppressSliderEvents = false;
        UpdateSliderLabels();
    }
}
