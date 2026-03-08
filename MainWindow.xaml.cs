// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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

    public static event Action<int>? ActiveProfileChanged;
    public static event Action<bool>? DynamicControlsModeChanged;
    public static event Action<bool>? DisplayPowerChanged;

    public static bool IsDisplayActive { get; private set; } = true;
    public static int ActiveProfileIndex => _staticInstance?._activeProfileIndex ?? -1;

    private const int MaxTrayProfiles = 10;

    private SettingsWindow? _settingsWindow;
    private ProfileFlyoutWindow? _profileFlyout;
    private TaskbarWidgetWindow? _taskbarWidget;
    private HwndSource? _hwndSource;
    private int _dcToggleHotkeyId = -1;
    private int _activeProfileIndex = -1;
    private static MainWindow? _staticInstance;

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

        // Register hotkeys for the saved mode
        if (SettingsManager.DynamicControlsEnabled)
        {
            DynamicControls.IsEnabled = true;
            DynamicControls.RegisterHotkeys(HotkeyManager, SettingsManager.DcKeybinds);
            DynamicControlsModeChanged?.Invoke(true);
        }
        else
        {
            foreach (var p in ProfileManager.Profiles)
                HotkeyManager.RegisterHotkey(p);
        }

        RegisterDcToggleHotkey();
        _profileFlyout = new ProfileFlyoutWindow();

        BuildTrayContextMenu();

        if (SettingsManager.TaskbarWidgetEnabled)
            ShowTaskbarWidget();

        OpenSettingsWindow();
    }

    // ══════════════════════════════════════════════════════════════
    //  POWER TOGGLE
    // ══════════════════════════════════════════════════════════════

    public static void ToggleDisplayPower()
    {
        if (IsDisplayActive)
            PowerOff();
        else
            PowerOn();

        DisplayPowerChanged?.Invoke(IsDisplayActive);
        _staticInstance?.BuildTrayContextMenu();
        _staticInstance?.UpdateWidgetDisplay();
    }

    private static void PowerOff()
    {
        DisplayManager.ResetToDefault();
        UnregisterAllHotkeys();
        IsDisplayActive = false;
        Logger.Log("Display powered OFF");
    }

    private static void PowerOn()
    {
        // CRITICAL: Set state to active FIRST so guards pass
        IsDisplayActive = true;

        // Re-register hotkeys for current mode
        if (DynamicControls.IsEnabled)
            DynamicControls.RegisterHotkeys(HotkeyManager, SettingsManager.DcKeybinds);
        else
            foreach (var p in ProfileManager.Profiles)
                HotkeyManager.RegisterHotkey(p);

        _staticInstance?.RegisterDcToggleHotkey();

        // Reapply display settings
        if (DynamicControls.IsEnabled)
        {
            DisplayManager.ApplySettings(
                DynamicControls.Gamma, DynamicControls.Contrast, DynamicControls.Vibrance);
        }
        else if (_staticInstance != null &&
                 _staticInstance._activeProfileIndex >= 0 &&
                 _staticInstance._activeProfileIndex < ProfileManager.Profiles.Count)
        {
            var p = ProfileManager.Profiles[_staticInstance._activeProfileIndex];
            DisplayManager.ApplySettings(p.Gamma, p.Contrast, p.Vibrance);
            ActiveProfileChanged?.Invoke(_staticInstance._activeProfileIndex);
        }

        Logger.Log("Display powered ON — hotkeys re-registered, profile restored");
    }

    private static void UnregisterAllHotkeys()
    {
        // Profile hotkeys
        foreach (var profile in ProfileManager.Profiles)
        {
            if (profile.HotkeyId > 0)
            {
                HotkeyManager.UnregisterHotkey(profile.HotkeyId);
                profile.HotkeyId = -1;
            }
        }

        // DC hotkeys
        DynamicControls.UnregisterHotkeys(HotkeyManager);

        // DC toggle hotkey
        if (_staticInstance != null && _staticInstance._dcToggleHotkeyId > 0)
        {
            HotkeyManager.UnregisterRawHotkey(_staticInstance._dcToggleHotkeyId);
            _staticInstance._dcToggleHotkeyId = -1;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  MODE SWITCHING
    // ══════════════════════════════════════════════════════════════

    public static void SwitchToDcMode()
    {
        if (!IsDisplayActive) return;

        // Unregister profile hotkeys
        foreach (var profile in ProfileManager.Profiles)
        {
            if (profile.HotkeyId > 0)
            {
                HotkeyManager.UnregisterHotkey(profile.HotkeyId);
                profile.HotkeyId = -1;
            }
        }

        DynamicControls.IsEnabled = true;
        DynamicControls.RegisterHotkeys(HotkeyManager, SettingsManager.DcKeybinds);
        SettingsManager.DynamicControlsEnabled = true;
        DynamicControlsModeChanged?.Invoke(true);
        _staticInstance?.UpdateWidgetDisplay();
        Logger.Log("Switched to Dynamic Controls mode");
    }

    public static void SwitchToProfileMode()
    {
        if (!IsDisplayActive) return;

        DynamicControls.UnregisterHotkeys(HotkeyManager);
        DynamicControls.IsEnabled = false;

        foreach (var p in ProfileManager.Profiles)
            HotkeyManager.RegisterHotkey(p);

        SettingsManager.DynamicControlsEnabled = false;
        DynamicControlsModeChanged?.Invoke(false);
        _staticInstance?.UpdateWidgetDisplay();
        Logger.Log("Switched to Profile mode");
    }

    private void RegisterDcToggleHotkey()
    {
        if (_dcToggleHotkeyId > 0)
            HotkeyManager.UnregisterRawHotkey(_dcToggleHotkeyId);
        _dcToggleHotkeyId = -1;

        if (SettingsManager.DcToggleKey != 0)
            _dcToggleHotkeyId = HotkeyManager.RegisterRawHotkey(
                SettingsManager.DcToggleKey, SettingsManager.DcToggleMod);
    }

    public void UpdateDcToggleHotkey() => RegisterDcToggleHotkey();

    // ══════════════════════════════════════════════════════════════
    //  WndProc — hotkey dispatch
    // ══════════════════════════════════════════════════════════════

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg != WM_HOTKEY) return IntPtr.Zero;

        if (!IsDisplayActive) { handled = true; return IntPtr.Zero; }

        int id = wParam.ToInt32();

        if (id == _dcToggleHotkeyId && _dcToggleHotkeyId > 0)
        {
            if (DynamicControls.IsEnabled) SwitchToProfileMode();
            else SwitchToDcMode();
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
        if (!IsDisplayActive) return;

        _activeProfileIndex = ProfileManager.IndexOf(e.Profile);
        if (_activeProfileIndex < 0) return;

        DisplayManager.ApplySettings(e.Profile.Gamma, e.Profile.Contrast, e.Profile.Vibrance);
        Logger.Log($"Hotkey applied profile: {e.Profile.Name}");

        ActiveProfileChanged?.Invoke(_activeProfileIndex);
        BuildTrayContextMenu();
        UpdateWidgetDisplay();

        if (SettingsManager.FlyoutEnabled)
            ShowProfileFlyout(e.Profile.Name);
    }

    public static void ApplyProfile(int index)
    {
        if (!IsDisplayActive) return;
        var profiles = ProfileManager.Profiles;
        if (index < 0 || index >= profiles.Count) return;

        var profile = profiles[index];
        if (_staticInstance != null)
            _staticInstance._activeProfileIndex = index;

        DisplayManager.ApplySettings(profile.Gamma, profile.Contrast, profile.Vibrance);
        Logger.Log($"Applied profile: {profile.Name}");

        ActiveProfileChanged?.Invoke(index);
        _staticInstance?.BuildTrayContextMenu();
        _staticInstance?.UpdateWidgetDisplay();

        if (SettingsManager.FlyoutEnabled)
            _staticInstance?.ShowProfileFlyout(profile.Name);
    }

    // ── Profile flyout ──

    private void ShowProfileFlyout(string profileName)
    {
        Dispatcher.Invoke(() =>
        {
            _profileFlyout ??= new ProfileFlyoutWindow();
            _profileFlyout.ShowProfileFlyout(profileName);
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  SYSTEM TRAY
    // ══════════════════════════════════════════════════════════════

    public void BuildTrayContextMenu()
    {
        Dispatcher.Invoke(() =>
        {
            TrayContextMenu.Items.Clear();

            bool powerOff = !IsDisplayActive;
            bool dcActive = DynamicControls.IsEnabled;
            var profiles = ProfileManager.Profiles;

            var openItem = new Wpf.Ui.Controls.MenuItem
            {
                Header = "Open DisplayHub",
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Open24 }
            };
            openItem.Click += (_, _) => OpenSettingsWindow();
            TrayContextMenu.Items.Add(openItem);
            TrayContextMenu.Items.Add(new Separator());

            var powerItem = new MenuItem
            {
                Header = powerOff ? "⏻  Power: Off" : "⏻  Power: On",
                FontWeight = FontWeights.SemiBold,
                IsChecked = IsDisplayActive
            };
            powerItem.Click += (_, _) => ToggleDisplayPower();
            TrayContextMenu.Items.Add(powerItem);
            TrayContextMenu.Items.Add(new Separator());

            string activeLabel = _activeProfileIndex >= 0 && _activeProfileIndex < profiles.Count
                ? $"Active: {profiles[_activeProfileIndex].Name}"
                : "No active profile";
            TrayContextMenu.Items.Add(new MenuItem
            {
                Header = activeLabel, IsEnabled = false, FontWeight = FontWeights.SemiBold
            });
            TrayContextMenu.Items.Add(new Separator());

            bool profilesDisabled = dcActive || powerOff;
            int profileCount = Math.Min(profiles.Count, MaxTrayProfiles);
            for (int i = 0; i < profileCount; i++)
            {
                var profile = profiles[i];
                int index = i;
                var profileItem = new MenuItem
                {
                    Header = profile.Name,
                    IsChecked = index == _activeProfileIndex,
                    IsEnabled = !profilesDisabled,
                    FontWeight = index == _activeProfileIndex ? FontWeights.SemiBold : FontWeights.Normal
                };
                profileItem.Click += (_, _) => ApplyProfile(index);
                TrayContextMenu.Items.Add(profileItem);
            }

            if (profiles.Count > MaxTrayProfiles)
            {
                var moreItem = new MenuItem
                {
                    Header = $"More... ({profiles.Count - MaxTrayProfiles} more)",
                    IsEnabled = !profilesDisabled
                };
                for (int i = MaxTrayProfiles; i < profiles.Count; i++)
                {
                    var profile = profiles[i];
                    int index = i;
                    var subItem = new MenuItem
                    {
                        Header = profile.Name, IsChecked = index == _activeProfileIndex
                    };
                    subItem.Click += (_, _) => ApplyProfile(index);
                    moreItem.Items.Add(subItem);
                }
                TrayContextMenu.Items.Add(moreItem);
            }

            TrayContextMenu.Items.Add(new Separator());

            var dcItem = new MenuItem
            {
                Header = dcActive ? "Dynamic Controls: On" : "Dynamic Controls: Off",
                IsChecked = dcActive, IsEnabled = !powerOff
            };
            dcItem.Click += (_, _) =>
            {
                if (DynamicControls.IsEnabled) SwitchToProfileMode();
                else SwitchToDcMode();
                BuildTrayContextMenu();
            };
            TrayContextMenu.Items.Add(dcItem);
            TrayContextMenu.Items.Add(new Separator());

            var exitItem = new Wpf.Ui.Controls.MenuItem
            {
                Header = "Exit",
                Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.ArrowExit20 }
            };
            exitItem.Click += (_, _) => ExitApplication();
            TrayContextMenu.Items.Add(exitItem);
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  WINDOW MANAGEMENT
    // ══════════════════════════════════════════════════════════════

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

    private void TrayIcon_LeftClick(object sender, RoutedEventArgs e)
    {
        if (SettingsManager.TrayLeftClickBehavior == 0)
            OpenSettingsWindow();
    }

    // ══════════════════════════════════════════════════════════════
    //  TASKBAR WIDGET
    // ══════════════════════════════════════════════════════════════

    public static void SetTaskbarWidgetEnabled(bool enabled)
    {
        if (_staticInstance == null) return;
        if (enabled) _staticInstance.ShowTaskbarWidget();
        else _staticInstance.HideTaskbarWidget();
    }

    public static void RefreshTaskbarWidget()
    {
        _staticInstance?._taskbarWidget?.RefreshPosition();
    }

    private void ShowTaskbarWidget()
    {
        if (_taskbarWidget != null) return;
        _taskbarWidget = new TaskbarWidgetWindow();
        _taskbarWidget.Show();
        UpdateWidgetDisplay();
    }

    private void HideTaskbarWidget()
    {
        if (_taskbarWidget == null) return;
        _taskbarWidget.StopAndClose();
        _taskbarWidget = null;
    }

    private void UpdateWidgetDisplay()
    {
        if (_taskbarWidget == null) return;
        string name = _activeProfileIndex >= 0 && _activeProfileIndex < ProfileManager.Profiles.Count
            ? ProfileManager.Profiles[_activeProfileIndex].Name
            : "No Profile";
        bool dcMode = DynamicControls.IsEnabled;
        _taskbarWidget.UpdateDisplay(name, IsDisplayActive, dcMode);
    }

    // ══════════════════════════════════════════════════════════════
    //  CLEANUP
    // ══════════════════════════════════════════════════════════════

    private void ExitApplication()
    {
        HideTaskbarWidget();
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
