// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DispHub.Constants;
using Wpf.Ui.Abstractions.Controls;

namespace DispHub.Pages;

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
        SyncPowerState();
        DynamicControlsToggle.IsChecked = MainWindow.DynamicControls.IsEnabled;
        DcActiveInfoBar.IsOpen = MainWindow.DynamicControls.IsEnabled;
        _isLoaded = true;

        UpdateValueDisplay();
        UpdateKeybindLabels();

        MainWindow.DynamicControls.ValuesChanged += OnValuesChanged;
        MainWindow.DynamicControlsModeChanged += OnModeChanged;
        MainWindow.DisplayPowerChanged += OnPowerChanged;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        MainWindow.DynamicControls.ValuesChanged -= OnValuesChanged;
        MainWindow.DynamicControlsModeChanged -= OnModeChanged;
        MainWindow.DisplayPowerChanged -= OnPowerChanged;
    }

    public Task OnNavigatedToAsync()
    {
        if (_isLoaded)
        {
            UpdateValueDisplay();
            UpdateKeybindLabels();
            SyncPowerState();
        }
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void OnValuesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(UpdateValueDisplay);
    }

    private void OnModeChanged(bool dcEnabled)
    {
        Dispatcher.Invoke(() =>
        {
            _isLoaded = false;
            DynamicControlsToggle.IsChecked = dcEnabled;
            DcActiveInfoBar.IsOpen = dcEnabled;
            _isLoaded = true;
        });
    }

    private void OnPowerChanged(bool active)
    {
        Dispatcher.Invoke(SyncPowerState);
    }

    private void SyncPowerState()
    {
        bool powerOff = !MainWindow.IsDisplayActive;
        PowerOffBar.IsOpen = powerOff;
        DynamicControlsToggle.IsEnabled = !powerOff;
    }

    private void UpdateValueDisplay()
    {
        var dc = MainWindow.DynamicControls;
        GammaValueText.Text = dc.Gamma.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        ContrastValueText.Text = $"{dc.Contrast * 100:F0}%";
        VibranceValueText.Text = dc.Vibrance.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private void UpdateKeybindLabels()
    {
        var kb = MainWindow.SettingsManager.DcKeybinds;
        GammaUpKeybind.Content = FormatKeybind(kb.GammaUpKey, kb.GammaUpMod);
        GammaDownKeybind.Content = FormatKeybind(kb.GammaDownKey, kb.GammaDownMod);
        ContrastUpKeybind.Content = FormatKeybind(kb.ContrastUpKey, kb.ContrastUpMod);
        ContrastDownKeybind.Content = FormatKeybind(kb.ContrastDownKey, kb.ContrastDownMod);
        VibranceUpKeybind.Content = FormatKeybind(kb.VibranceUpKey, kb.VibranceUpMod);
        VibranceDownKeybind.Content = FormatKeybind(kb.VibranceDownKey, kb.VibranceDownMod);

        var toggleKey = MainWindow.SettingsManager.DcToggleKey;
        var toggleMod = MainWindow.SettingsManager.DcToggleMod;
        DcToggleKeybind.Content = toggleKey == 0 ? "None" : FormatKeybind(toggleKey, toggleMod);
    }

    private static string FormatKeybind(uint vk, uint mod)
    {
        if (vk == 0) return "None";
        var parts = new List<string>();
        if ((mod & AppConstants.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mod & AppConstants.MOD_ALT) != 0) parts.Add("Alt");
        if ((mod & AppConstants.MOD_SHIFT) != 0) parts.Add("Shift");
        Key key = KeyInterop.KeyFromVirtualKey((int)vk);
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }

    private void DynamicControlsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        bool enabled = DynamicControlsToggle.IsChecked ?? false;

        if (enabled)
            MainWindow.SwitchToDcMode();
        else
            MainWindow.SwitchToProfileMode();

        DcActiveInfoBar.IsOpen = enabled;

        // Rebuild tray menu to reflect mode change
        if (Application.Current.MainWindow is MainWindow mw)
            mw.BuildTrayContextMenu();
    }

    private void ChangeKeybind_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Wpf.Ui.Controls.Button { Tag: string action }) return;

        var dialog = new HotkeyDialog { Owner = Window.GetWindow(this) };
        if (!(dialog.ShowDialog() ?? false)) return;

        var kb = MainWindow.SettingsManager.DcKeybinds;
        uint vk = (uint)dialog.VirtualKeyCode;
        uint mod = dialog.Modifiers;

        switch (action)
        {
            case "GammaUp": kb.GammaUpKey = vk; kb.GammaUpMod = mod; break;
            case "GammaDown": kb.GammaDownKey = vk; kb.GammaDownMod = mod; break;
            case "ContrastUp": kb.ContrastUpKey = vk; kb.ContrastUpMod = mod; break;
            case "ContrastDown": kb.ContrastDownKey = vk; kb.ContrastDownMod = mod; break;
            case "VibranceUp": kb.VibranceUpKey = vk; kb.VibranceUpMod = mod; break;
            case "VibranceDown": kb.VibranceDownKey = vk; kb.VibranceDownMod = mod; break;
        }

        MainWindow.SettingsManager.SaveDcKeybinds();
        UpdateKeybindLabels();

        // Re-register if DC is currently active
        if (MainWindow.DynamicControls.IsEnabled)
        {
            MainWindow.DynamicControls.UnregisterHotkeys(MainWindow.HotkeyManager);
            MainWindow.DynamicControls.RegisterHotkeys(MainWindow.HotkeyManager, kb);
        }
    }

    private void ChangeDcToggle_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyDialog { Owner = Window.GetWindow(this) };
        if (!(dialog.ShowDialog() ?? false)) return;

        MainWindow.SettingsManager.DcToggleKey = (uint)dialog.VirtualKeyCode;
        MainWindow.SettingsManager.DcToggleMod = dialog.Modifiers;
        UpdateKeybindLabels();

        if (Application.Current.MainWindow is MainWindow mw)
            mw.UpdateDcToggleHotkey();
    }
}
