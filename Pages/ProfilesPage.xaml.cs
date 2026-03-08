using DisplayHub.Models;
using DisplayHub.Services.Logging;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace DisplayHub.Pages;

public partial class ProfilesPage : Page, INavigationAware
{
    private bool _isLoaded = false;
    private int _selectedIndex = 0;

    public ProfilesPage()
    {
        InitializeComponent();
        Loaded += ProfilesPage_Loaded;
    }

    private void ProfilesPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        RefreshProfileList();
        if (MainWindow.ProfileManager.Profiles.Count > 0)
        {
            _selectedIndex = 0;
            LoadSelectedProfile();
        }
        UpdateDcState();
    }

    public Task OnNavigatedToAsync()
    {
        if (_isLoaded)
        {
            RefreshProfileList();
            UpdateDcState();
        }
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void UpdateDcState()
    {
        bool dcActive = MainWindow.DynamicControls.IsEnabled;
        DcWarningBar.IsOpen = dcActive;
        SlidersPanel.IsEnabled = !dcActive;
        ActionButtonsPanel.IsEnabled = !dcActive;
        ProfileCardPanel.IsEnabled = !dcActive;
    }

    private void RefreshProfileList()
    {
        ProfileCardPanel.Children.Clear();
        for (int i = 0; i < MainWindow.ProfileManager.Profiles.Count; i++)
        {
            var profile = MainWindow.ProfileManager.Profiles[i];
            bool isSelected = (i == _selectedIndex);

            var stack = new StackPanel { MinWidth = 100 };
            stack.Children.Add(new TextBlock
            {
                Text = profile.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            });
            if (profile.HotKeyValue != 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = profile.HotkeyDisplayText,
                    FontSize = 11,
                    Opacity = 0.6,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            int capturedIndex = i;
            var btn = new Wpf.Ui.Controls.Button
            {
                Content = stack,
                Appearance = isSelected
                    ? Wpf.Ui.Controls.ControlAppearance.Primary
                    : Wpf.Ui.Controls.ControlAppearance.Secondary,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(16, 10, 16, 10),
                Tag = capturedIndex,
            };
            btn.Click += ProfileCard_Click;
            ProfileCardPanel.Children.Add(btn);
        }
    }

    private void ProfileCard_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is int idx)
        {
            _selectedIndex = idx;
            LoadSelectedProfile();
            ApplyCurrentSliders();
            RefreshProfileList();
        }
    }

    private void LoadSelectedProfile()
    {
        if (_selectedIndex < 0 || _selectedIndex >= MainWindow.ProfileManager.Profiles.Count) return;
        var profile = MainWindow.ProfileManager.Profiles[_selectedIndex];

        _isLoaded = false;
        GammaSlider.Value = profile.Gamma;
        ContrastSlider.Value = profile.Contrast;
        VibranceSlider.Value = profile.Vibrance;
        _isLoaded = true;

        GammaValueText.Text = profile.Gamma.ToString("F2");
        ContrastValueText.Text = $"{profile.Contrast * 100:F0}%";
        VibranceValueText.Text = profile.Vibrance.ToString();
    }

    private void ApplyCurrentSliders()
    {
        MainWindow.DisplayManager.ApplySettings(GammaSlider.Value, ContrastSlider.Value, (int)VibranceSlider.Value);
        if (_selectedIndex >= 0 && _selectedIndex < MainWindow.ProfileManager.Profiles.Count)
            Logger.Log($"Applied profile: {MainWindow.ProfileManager.Profiles[_selectedIndex].Name}");
    }

    private void GammaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        GammaValueText.Text = GammaSlider.Value.ToString("F2");
        ApplyCurrentSliders();
    }

    private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        ContrastValueText.Text = $"{ContrastSlider.Value * 100:F0}%";
        ApplyCurrentSliders();
    }

    private void VibranceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isLoaded) return;
        VibranceValueText.Text = ((int)VibranceSlider.Value).ToString();
        ApplyCurrentSliders();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        if (_selectedIndex < 0 || _selectedIndex >= MainWindow.ProfileManager.Profiles.Count) return;

        var profile = MainWindow.ProfileManager.Profiles[_selectedIndex];
        var updated = new Profile(profile.Name, GammaSlider.Value, ContrastSlider.Value, (int)VibranceSlider.Value)
        {
            HotKeyValue = profile.HotKeyValue,
            HotKeyModifierValue = profile.HotKeyModifierValue,
            HotkeyId = profile.HotkeyId
        };

        MainWindow.ProfileManager.UpdateProfile(_selectedIndex, updated);
        Logger.Log($"Saved profile: {updated.Name}");
        RefreshProfileList();
    }

    private void SetHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        if (_selectedIndex < 0 || _selectedIndex >= MainWindow.ProfileManager.Profiles.Count) return;

        var dialog = new HotkeyDialog();
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true)
        {
            var profile = MainWindow.ProfileManager.Profiles[_selectedIndex];

            // Unregister old hotkey
            if (profile.HotkeyId > 0)
                MainWindow.HotkeyManager.UnregisterHotkey(profile.HotkeyId);

            profile.HotKeyValue = dialog.VirtualKeyCode;
            profile.HotKeyModifierValue = dialog.Modifiers;
            MainWindow.ProfileManager.SaveProfiles();

            // Register new hotkey if set
            if (profile.HotKeyValue != 0)
                MainWindow.HotkeyManager.RegisterHotkey(profile);

            RefreshProfileList();
        }
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        var profile = new Profile($"Profile {MainWindow.ProfileManager.Profiles.Count + 1}",
            1.0, 0.5, 50);
        MainWindow.ProfileManager.AddProfile(profile);
        _selectedIndex = MainWindow.ProfileManager.Profiles.Count - 1;
        LoadSelectedProfile();
        ApplyCurrentSliders();
        RefreshProfileList();
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        if (_selectedIndex < 0 || _selectedIndex >= MainWindow.ProfileManager.Profiles.Count) return;

        // Unregister hotkey before deleting
        var profile = MainWindow.ProfileManager.Profiles[_selectedIndex];
        if (profile.HotkeyId > 0)
            MainWindow.HotkeyManager.UnregisterHotkey(profile.HotkeyId);

        MainWindow.ProfileManager.RemoveProfile(_selectedIndex);
        if (MainWindow.ProfileManager.Profiles.Count > 0)
        {
            _selectedIndex = Math.Min(_selectedIndex, MainWindow.ProfileManager.Profiles.Count - 1);
            LoadSelectedProfile();
            ApplyCurrentSliders();
        }
        RefreshProfileList();
    }

    private void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        _isLoaded = false;
        GammaSlider.Value = 1.0;
        ContrastSlider.Value = 0.5;
        VibranceSlider.Value = 50;
        _isLoaded = true;

        GammaValueText.Text = "1.00";
        ContrastValueText.Text = "50%";
        VibranceValueText.Text = "50";

        MainWindow.DisplayManager.ApplySettings(1.0, 0.5, 50);
        Logger.Log("Reset to default values");
    }
}
