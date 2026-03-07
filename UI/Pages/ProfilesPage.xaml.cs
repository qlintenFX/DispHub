using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DisplayHub.Models;
using DisplayHub.Services.Logging;
using DisplayHub.UI.Dialogs;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.UI.Pages;

public partial class ProfilesPage : Page, INavigationAware
{
    private MainWindow MainWindow => (System.Windows.Application.Current.MainWindow as MainWindow)!;
    private bool suppressSliderEvents;

    public ProfilesPage()
    {
        InitializeComponent();
    }

    public Task OnNavigatedToAsync()
    {
        LoadProfilesToUI();
        UpdateSliderLabels();
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void LoadProfilesToUI()
    {
        var mw = MainWindow;
        if (mw.ProfileManager == null) return;

        var selectedProfile = ProfileListBox.SelectedItem as Profile;
        ProfileListBox.Items.Clear();

        foreach (Profile profile in mw.ProfileManager.Profiles)
            ProfileListBox.Items.Add(profile);

        if (selectedProfile != null && ProfileListBox.Items.Contains(selectedProfile))
            ProfileListBox.SelectedItem = selectedProfile;
        else if (ProfileListBox.Items.Count > 0)
            ProfileListBox.SelectedIndex = 0;

        mw.UpdateTrayProfilesMenu();
    }

    // ─── Profile List Events ───────────────────────────────────────────

    private void ProfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileListBox.SelectedItem is not Profile selected) return;

        suppressSliderEvents = true;
        GammaSlider.Value = (int)(selected.Gamma * 100);
        ContrastSlider.Value = (int)(selected.Contrast * 100);
        VibranceSlider.Value = Math.Clamp(selected.Vibrance, 0, 100);
        suppressSliderEvents = false;
        UpdateSliderLabels();

        HotkeyLabel.Text = string.IsNullOrEmpty(selected.HotkeyDisplayText)
            ? "Hotkey: None"
            : $"Hotkey: {selected.HotkeyDisplayText}";
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
    }

    private void UpdateSliderLabels()
    {
        if (GammaValueText == null) return;
        GammaValueText.Text = $"{GammaSlider.Value / 100.0:F2}";
        ContrastValueText.Text = $"{(int)ContrastSlider.Value}%";
        VibranceValueText.Text = $"{(int)VibranceSlider.Value}%";
    }

    // ─── Button Events ─────────────────────────────────────────────────

    private void ApplyProfile_Click(object sender, RoutedEventArgs e) => ApplySelectedProfile();

    private void ApplySelectedProfile()
    {
        if (ProfileListBox.SelectedItem is not Profile selected) return;
        MainWindow.ApplyProfile(selected);
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var mw = MainWindow;
        if (mw.ProfileManager == null) return;

        string? name = ProfileNameDialog.Show(Window.GetWindow(this));
        if (name == null) return;

        double gamma = GammaSlider.Value / 100.0;
        double contrast = ContrastSlider.Value / 100.0;
        var newProfile = new Profile(name, gamma, contrast, (int)VibranceSlider.Value);
        mw.ProfileManager.AddProfile(newProfile);

        LoadProfilesToUI();
        ProfileListBox.SelectedItem = newProfile;
    }

    private void UpdateProfile_Click(object sender, RoutedEventArgs e)
    {
        var mw = MainWindow;
        if (mw.ProfileManager == null || ProfileListBox.SelectedItem is not Profile selected) return;

        selected.Gamma = GammaSlider.Value / 100.0;
        selected.Contrast = ContrastSlider.Value / 100.0;
        selected.Vibrance = (int)VibranceSlider.Value;
        mw.ProfileManager.SaveProfiles();

        int idx = ProfileListBox.SelectedIndex;
        LoadProfilesToUI();
        if (idx < ProfileListBox.Items.Count) ProfileListBox.SelectedIndex = idx;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var mw = MainWindow;
        if (mw.ProfileManager == null || ProfileListBox.SelectedItem is not Profile selected) return;

        var result = System.Windows.MessageBox.Show(
            $"Delete profile '{selected.Name}'?", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        if (selected.HotkeyId > 0)
            mw.HotkeyManager?.UnregisterHotkey(selected.HotkeyId);

        int idx = ProfileListBox.SelectedIndex;
        mw.ProfileManager.RemoveProfile(idx);
        LoadProfilesToUI();

        if (idx >= ProfileListBox.Items.Count) idx = ProfileListBox.Items.Count - 1;
        if (idx >= 0) ProfileListBox.SelectedIndex = idx;
    }

    private void SetHotkey_Click(object sender, RoutedEventArgs e)
    {
        var mw = MainWindow;
        if (ProfileListBox.SelectedItem is not Profile selected) return;
        if (mw.HotkeyManager == null || mw.ProfileManager == null) return;

        var dialog = new HotkeyDialog(selected.HotKey, selected.HotKeyModifier) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        if (dialog.ResultVkCode == 0)
        {
            // Cleared
            if (selected.HotkeyId > 0) mw.HotkeyManager.UnregisterHotkey(selected.HotkeyId);
            selected.HotKey = 0; selected.HotKeyModifier = 0; selected.HotkeyId = -1;
            mw.ProfileManager.SaveProfiles();
            HotkeyLabel.Text = "Hotkey: None";
            return;
        }

        if (selected.HotkeyId > 0) mw.HotkeyManager.UnregisterHotkey(selected.HotkeyId);
        selected.HotKey = dialog.ResultVkCode;
        selected.HotKeyModifier = dialog.ResultModifiers;

        int id = mw.HotkeyManager.RegisterHotkey(selected);
        if (id > 0)
        {
            selected.HotkeyId = id;
            mw.ProfileManager.SaveProfiles();
            HotkeyLabel.Text = $"Hotkey: {selected.HotkeyDisplayText}";
            int savedIdx = ProfileListBox.SelectedIndex;
            LoadProfilesToUI();
            ProfileListBox.SelectedIndex = savedIdx;
        }
        else
        {
            System.Windows.MessageBox.Show(
                "Failed to register the hotkey. It may be in use by another application.",
                "Hotkey Registration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.DisplayManager?.ResetToDefault();
        suppressSliderEvents = true;
        GammaSlider.Value = 100;
        ContrastSlider.Value = 50;
        VibranceSlider.Value = 50;
        suppressSliderEvents = false;
        UpdateSliderLabels();
    }
}
