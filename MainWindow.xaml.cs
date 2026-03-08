// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using DisplayHub.Services.Display;
using DisplayHub.Services.Hotkeys;
using DisplayHub.Services.Logging;
using DisplayHub.Services.Profiles;
using DisplayHub.Services.Settings;
using DisplayHub.Windows;

namespace DisplayHub;

public partial class MainWindow : Window
{
    public static ProfileManager ProfileManager { get; private set; } = null!;
    public static DisplayManager DisplayManager { get; private set; } = null!;
    public static HotkeyManager HotkeyManager { get; private set; } = null!;
    public static DynamicControls DynamicControls { get; private set; } = null!;
    public static SettingsManager SettingsManager { get; private set; } = null!;

    /// <summary>Fired when a profile is applied via hotkey or tray menu.</summary>
    public static event Action<int>? ActiveProfileChanged;

    /// <summary>Fired when DC mode is toggled (via hotkey or tray).</summary>
    public static event Action<bool>? DynamicControlsModeChanged;

    /// <summary>Fired when display power is toggled on/off.</summary>
    public static event Action<bool>? DisplayPowerChanged;

    /// <summary>Whether the display settings are currently applied (not reset).</summary>
    public static bool IsDisplayActive { get; private set; } = true;

    private const int MaxTrayProfiles = 10;

    private SettingsWindow? _settingsWindow;
    private ProfileFlyoutWindow? _profileFlyout;
    private HwndSource? _hwndSource;
    private int _dcToggleHotkeyId = -1;
    private int _activeProfileIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        _staticInstance = this;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Initialize(Constants.AppConstants.LogFileName);
        Logger.Log("MainWindow_Loaded");

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);

        SettingsManager = new SettingsManager();
        SettingsWindow.ApplyTheme(SettingsManager.AppTheme);
        ProfileManager = new ProfileManager();
        DisplayManager = new DisplayManager(VibranceServiceFactory.Create());
        HotkeyManager = new HotkeyManager(helper.Handle);
        DynamicControls = new DynamicControls(DisplayManager);

        HotkeyManager.HotkeyPressed += OnProfileHotkeyPressed;

        if (SettingsManager.DynamicControlsEnabled)
        {
            DynamicControls.IsEnabled = true;
            SwitchToDcMode();
        }
        else
        {
            RegisterProfileHotkeys();
        }

        RegisterDcToggleHotkey();

        _profileFlyout = new ProfileFlyoutWindow();

        BuildTrayContextMenu();
        OpenSettingsWindow();
    }

    // ── Display power toggle ──

    public static void ToggleDisplayPower()
    {
        if (IsDisplayActive)
        {
            DisplayManager.ResetToDefault();
            IsDisplayActive = false;
            Logger.Log("Display powered off (reset to default)");
        }
        else
        {
            // Reapply the active profile or DC values
            if (DynamicControls.IsEnabled)
            {
                DisplayManager.ApplySettings(DynamicControls.Gamma, DynamicControls.Contrast, DynamicControls.Vibrance);
            }
            else if (_staticInstance?._activeProfileIndex >= 0 &&
                     _staticInstance._activeProfileIndex < ProfileManager.Profiles.Count)
            {
                var p = ProfileManager.Profiles[_staticInstance._activeProfileIndex];
                DisplayManager.ApplySettings(p.Gamma, p.Contrast, p.Vibrance);
            }
            IsDisplayActive = true;
            Logger.Log("Display powered on");
        }
        DisplayPowerChanged?.Invoke(IsDisplayActive);
    }

    private static MainWindow? _staticInstance;

    // ── Mode-based hotkey management ──

    public static void RegisterProfileHotkeys()
    {
        foreach (var profile in ProfileManager.Profiles)
            HotkeyManager.RegisterHotkey(profile);
    }

    public static void UnregisterProfileHotkeys()
    {
        foreach (var profile in ProfileManager.Profiles)
        {
            if (profile.HotkeyId > 0)
            {
                HotkeyManager.UnregisterHotkey(profile.HotkeyId);
                profile.HotkeyId = -1;
            }
        }
    }

    public static void SwitchToDcMode()
    {
        UnregisterProfileHotkeys();
        DynamicControls.IsEnabled = true;
        DynamicControls.RegisterHotkeys(HotkeyManager, SettingsManager.DcKeybinds);
        SettingsManager.DynamicControlsEnabled = true;
        DynamicControlsModeChanged?.Invoke(true);
        Logger.Log("Switched to Dynamic Controls mode");
    }

    public static void SwitchToProfileMode()
    {
        DynamicControls.UnregisterHotkeys(HotkeyManager);
        DynamicControls.IsEnabled = false;
        RegisterProfileHotkeys();
        SettingsManager.DynamicControlsEnabled = false;
        DynamicControlsModeChanged?.Invoke(false);
        Logger.Log("Switched to Profile mode");
    }

    private void RegisterDcToggleHotkey()
    {
        if (_dcToggleHotkeyId > 0)
            HotkeyManager.UnregisterRawHotkey(_dcToggleHotkeyId);
        _dcToggleHotkeyId = -1;

        if (SettingsManager.DcToggleKey != 0)
        {
            _dcToggleHotkeyId = HotkeyManager.RegisterRawHotkey(
                SettingsManager.DcToggleKey, SettingsManager.DcToggleMod);
        }
    }

    public void UpdateDcToggleHotkey() => RegisterDcToggleHotkey();

    // ── WndProc hotkey dispatch ──

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg != WM_HOTKEY) return IntPtr.Zero;

        int id = wParam.ToInt32();

        if (id == _dcToggleHotkeyId && _dcToggleHotkeyId > 0)
        {
            if (DynamicControls.IsEnabled)
                SwitchToProfileMode();
            else
                SwitchToDcMode();
            BuildTrayContextMenu();
            handled = true;
            return IntPtr.Zero;
        }

        if (DynamicControls.IsEnabled)
            DynamicControls.ProcessHotkey(id);
        else
            HotkeyManager.ProcessHotkey(wParam);

        handled = true;
        return IntPtr.Zero;
    }

    private void OnProfileHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        _activeProfileIndex = ProfileManager.IndexOf(e.Profile);
        if (_activeProfileIndex < 0) return;

        if (IsDisplayActive)
        {
            DisplayManager.ApplySettings(e.Profile.Gamma, e.Profile.Contrast, e.Profile.Vibrance);
        }
        Logger.Log($"Hotkey applied profile: {e.Profile.Name}");

        ActiveProfileChanged?.Invoke(_activeProfileIndex);
        BuildTrayContextMenu();
        ShowProfileFlyout(e.Profile.Name);
    }

    // ── Profile flyout ──

    private void ShowProfileFlyout(string profileName)
    {
        Dispatcher.Invoke(() =>
        {
            _profileFlyout ??= new ProfileFlyoutWindow();
            _profileFlyout.ShowProfileFlyout(profileName, IsDisplayActive);
        });
    }

    // ── System tray context menu ──

    public void BuildTrayContextMenu()
    {
        Dispatcher.Invoke(() =>
        {
            TrayContextMenu.Items.Clear();

            // Open
            var openItem = new Wpf.Ui.Controls.MenuItem
            {
                Header = "Open DisplayHub",
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Open24 }
            };
            openItem.Click += (_, _) => OpenSettingsWindow();
            TrayContextMenu.Items.Add(openItem);

            TrayContextMenu.Items.Add(new Separator());

            // Active profile header
            bool dcActive = DynamicControls.IsEnabled;
            var profiles = ProfileManager.Profiles;

            string activeLabel = _activeProfileIndex >= 0 && _activeProfileIndex < profiles.Count
                ? $"Active: {profiles[_activeProfileIndex].Name}"
                : "No active profile";
            var headerItem = new MenuItem
            {
                Header = activeLabel,
                IsEnabled = false,
                FontWeight = FontWeights.SemiBold
            };
            TrayContextMenu.Items.Add(headerItem);

            TrayContextMenu.Items.Add(new Separator());

            // Profile list
            int profileCount = Math.Min(profiles.Count, MaxTrayProfiles);
            for (int i = 0; i < profileCount; i++)
            {
                var profile = profiles[i];
                int index = i;
                var profileItem = new MenuItem
                {
                    Header = profile.Name,
                    IsChecked = index == _activeProfileIndex,
                    IsEnabled = !dcActive,
                    FontWeight = index == _activeProfileIndex ? FontWeights.SemiBold : FontWeights.Normal
                };
                profileItem.Click += (_, _) => ApplyProfileFromTray(index);
                TrayContextMenu.Items.Add(profileItem);
            }

            if (profiles.Count > MaxTrayProfiles)
            {
                var moreItem = new MenuItem { Header = $"More... ({profiles.Count - MaxTrayProfiles} more)", IsEnabled = !dcActive };
                for (int i = MaxTrayProfiles; i < profiles.Count; i++)
                {
                    var profile = profiles[i];
                    int index = i;
                    var subItem = new MenuItem
                    {
                        Header = profile.Name,
                        IsChecked = index == _activeProfileIndex
                    };
                    subItem.Click += (_, _) => ApplyProfileFromTray(index);
                    moreItem.Items.Add(subItem);
                }
                TrayContextMenu.Items.Add(moreItem);
            }

            TrayContextMenu.Items.Add(new Separator());

            // DC toggle
            var dcItem = new MenuItem
            {
                Header = dcActive ? "Dynamic Controls: On" : "Dynamic Controls: Off",
                IsChecked = dcActive
            };
            dcItem.Click += (_, _) =>
            {
                if (DynamicControls.IsEnabled) SwitchToProfileMode();
                else SwitchToDcMode();
                BuildTrayContextMenu();
            };
            TrayContextMenu.Items.Add(dcItem);

            TrayContextMenu.Items.Add(new Separator());

            // Exit
            var exitItem = new Wpf.Ui.Controls.MenuItem
            {
                Header = "Exit",
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowExit20 }
            };
            exitItem.Click += (_, _) => ExitApplication();
            TrayContextMenu.Items.Add(exitItem);
        });
    }

    private void ApplyProfileFromTray(int index)
    {
        var profiles = ProfileManager.Profiles;
        if (index < 0 || index >= profiles.Count) return;

        var profile = profiles[index];
        _activeProfileIndex = index;

        if (IsDisplayActive)
            DisplayManager.ApplySettings(profile.Gamma, profile.Contrast, profile.Vibrance);

        Logger.Log($"Tray applied profile: {profile.Name}");
        ActiveProfileChanged?.Invoke(index);
        BuildTrayContextMenu();
        ShowProfileFlyout(profile.Name);
    }

    // ── Window management ──

    public void OpenSettingsWindow()
    {
        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
        _settingsWindow.Focus();
    }

    private void TrayIcon_LeftClick(object sender, RoutedEventArgs e) => OpenSettingsWindow();

    private void ExitApplication()
    {
        DisplayManager?.ResetToDefault();
        HotkeyManager?.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        base.OnClosed(e);
    }
}
