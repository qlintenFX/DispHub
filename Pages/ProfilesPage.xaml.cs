using DisplayHub.Models;
using DisplayHub.Services.Logging;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

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

        MainWindow.ActiveProfileChanged += OnActiveProfileChanged;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        MainWindow.ActiveProfileChanged -= OnActiveProfileChanged;
    }

    public Task OnNavigatedToAsync()
    {
        if (_isLoaded)
        {
            RefreshProfileCards();
            SyncDynamicControlsState();
        }
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    // ── Hotkey-driven profile switch (fired from MainWindow) ──

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
        SlidersPanel.IsEnabled = !dcActive;
        ActionButtonsPanel.IsEnabled = !dcActive;
        ProfileCardPanel.IsEnabled = !dcActive;
    }

    // ── Profile card rendering ──

    private void RefreshProfileCards()
    {
        ProfileCardPanel.Children.Clear();
        var profiles = MainWindow.ProfileManager.Profiles;

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
            var card = new Wpf.Ui.Controls.Button
            {
                Content = content,
                Appearance = isSelected
                    ? Wpf.Ui.Controls.ControlAppearance.Primary
                    : Wpf.Ui.Controls.ControlAppearance.Secondary,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(16, 10, 16, 10),
                Tag = index,
            };
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
        ApplyCurrentSliderValues();
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
        _isLoaded = true;

        UpdateSliderLabels();
    }

    private void UpdateSliderLabels()
    {
        GammaValueText.Text = GammaSlider.Value.ToString("F2");
        ContrastValueText.Text = $"{ContrastSlider.Value * 100:F0}%";
        VibranceValueText.Text = ((int)VibranceSlider.Value).ToString();
    }

    private void ApplyCurrentSliderValues()
    {
        MainWindow.DisplayManager.ApplySettings(
            GammaSlider.Value, ContrastSlider.Value, (int)VibranceSlider.Value);

        var profiles = MainWindow.ProfileManager.Profiles;
        if (_selectedIndex >= 0 && _selectedIndex < profiles.Count)
            Logger.Log($"Applied profile: {profiles[_selectedIndex].Name}");
    }

    // ── Slider ValueChanged handlers ──

    private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        UpdateSliderLabels();
        ApplyCurrentSliderValues();
    }

    private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        UpdateSliderLabels();
        ApplyCurrentSliderValues();
    }

    private void VibranceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
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
        var updated = new Profile(existing.Name, GammaSlider.Value, ContrastSlider.Value, (int)VibranceSlider.Value)
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
        if (dialog.ShowDialog() != true) return;

        var profile = profiles[_selectedIndex];

        if (profile.HotkeyId > 0)
            MainWindow.HotkeyManager.UnregisterHotkey(profile.HotkeyId);

        profile.HotKeyValue = dialog.VirtualKeyCode;
        profile.HotKeyModifierValue = dialog.Modifiers;
        MainWindow.ProfileManager.SaveProfiles();

        if (profile.HotKeyValue != 0)
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
        _isLoaded = true;

        UpdateSliderLabels();
        MainWindow.DisplayManager.ApplySettings(
            Constants.AppConstants.GammaDefault,
            Constants.AppConstants.ContrastDefault,
            Constants.AppConstants.VibranceDefault);
        Logger.Log("Reset to default values");
    }
}
