using DisplayHub.Models;
using DisplayHub.Services.Logging;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

public partial class ProfilesPage : Page, INavigationAware
{
    private bool _isLoaded = false;

    public ProfilesPage()
    {
        InitializeComponent();
        Loaded += ProfilesPage_Loaded;
    }

    private void ProfilesPage_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshProfileList();
        if (ProfileListBox.Items.Count > 0)
            ProfileListBox.SelectedIndex = 0;
        _isLoaded = true;
    }

    public Task OnNavigatedToAsync()
    {
        RefreshProfileList();
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void RefreshProfileList()
    {
        ProfileListBox.Items.Clear();
        foreach (var profile in MainWindow.ProfileManager.Profiles)
        {
            string display = profile.HotKeyValue != 0
                ? $"{profile.Name}  [{profile.HotkeyDisplayText}]"
                : profile.Name;
            ProfileListBox.Items.Add(display);
        }
    }

    private void ProfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        LoadSelectedProfile();
    }

    private void LoadSelectedProfile()
    {
        int idx = ProfileListBox.SelectedIndex;
        if (idx < 0 || idx >= MainWindow.ProfileManager.Profiles.Count) return;

        var profile = MainWindow.ProfileManager.Profiles[idx];

        _isLoaded = false;
        GammaSlider.Value = profile.Gamma;
        ContrastSlider.Value = profile.Contrast;
        VibranceSlider.Value = profile.Vibrance;
        _isLoaded = true;

        GammaValueText.Text = profile.Gamma.ToString("F2");
        ContrastValueText.Text = $"{profile.Contrast * 100:F0}%";
        VibranceValueText.Text = profile.Vibrance.ToString();
        HotkeyDisplayText.Text = $"Hotkey: {profile.HotkeyDisplayText}";
    }

    private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        GammaValueText.Text = GammaSlider.Value.ToString("F2");
    }

    private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        ContrastValueText.Text = $"{ContrastSlider.Value * 100:F0}%";
    }

    private void VibranceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        VibranceValueText.Text = ((int)VibranceSlider.Value).ToString();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        int idx = ProfileListBox.SelectedIndex;
        if (idx < 0 || idx >= MainWindow.ProfileManager.Profiles.Count) return;

        var profile = MainWindow.ProfileManager.Profiles[idx];
        var updated = new Profile(profile.Name, GammaSlider.Value, ContrastSlider.Value, (int)VibranceSlider.Value)
        {
            HotKeyValue = profile.HotKeyValue,
            HotKeyModifierValue = profile.HotKeyModifierValue,
            HotkeyId = profile.HotkeyId
        };

        MainWindow.ProfileManager.UpdateProfile(idx, updated);
        MainWindow.DisplayManager.ApplySettings(updated.Gamma, updated.Contrast, updated.Vibrance);
        Logger.Log($"Applied profile: {updated.Name}");

        RefreshProfileList();
        ProfileListBox.SelectedIndex = idx;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.DisplayManager.ResetToDefault();
        Logger.Log("Display reset to default");
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        var profile = new Profile($"Profile {MainWindow.ProfileManager.Profiles.Count + 1}",
            1.0, 0.5, 50);
        MainWindow.ProfileManager.AddProfile(profile);
        RefreshProfileList();
        ProfileListBox.SelectedIndex = ProfileListBox.Items.Count - 1;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        int idx = ProfileListBox.SelectedIndex;
        if (idx < 0) return;
        MainWindow.ProfileManager.RemoveProfile(idx);
        RefreshProfileList();
        if (ProfileListBox.Items.Count > 0)
            ProfileListBox.SelectedIndex = Math.Min(idx, ProfileListBox.Items.Count - 1);
    }

    private void SetHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        int idx = ProfileListBox.SelectedIndex;
        if (idx < 0) return;
        MessageBox.Show("Press a hotkey to assign it to the selected profile.\n\nNote: Hotkey assignment UI coming soon.\nFor now, edit profiles.json directly in %APPDATA%\\DisplayHub\\",
            "Set Hotkey", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
