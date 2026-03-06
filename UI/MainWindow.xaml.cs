using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DisplayHub.Constants;
using DisplayHub.Models;
using DisplayHub.Services.Display;
using DisplayHub.Services.Hotkeys;
using DisplayHub.Services.Logging;
using DisplayHub.Services.Profiles;
using DisplayHub.Services.Settings;
using DisplayHub.UI.Pages;
using Wpf.Ui.Controls;

namespace DisplayHub.UI;

public partial class MainWindow : FluentWindow
{
    internal ProfileManager? ProfileManager { get; private set; }
    internal DisplayManager? DisplayManager { get; private set; }
    internal HotkeyManager? HotkeyManager { get; private set; }
    internal DynamicControls? DynamicControls { get; private set; }
    internal ISettingsManager SettingsManager { get; }
    internal Profile? CurrentProfile { get; set; }

    private System.Windows.Forms.NotifyIcon? trayIcon;
    private System.Windows.Forms.ContextMenuStrip? trayContextMenu;
    private System.Windows.Forms.ToolStripMenuItem? trayProfilesMenu;
    private bool minimizeToTray = true;
    private bool resourcesCleaned;
    private HwndSource? hwndSource;

    // Page instances (reused)
    private ProfilesPage? profilesPage;
    private DynamicControlsPage? dynamicControlsPage;
    private SettingsPage? settingsPage;
    private AboutPage? aboutPage;

    public MainWindow()
    {
        SettingsManager = new SettingsManager();
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Logger.Log("MainWindow loading");

            // Get window handle for hotkey registration
            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;

            // Hook WndProc for hotkey messages
            hwndSource = HwndSource.FromHwnd(hwnd);
            hwndSource?.AddHook(WndProc);

            ProfileManager = new ProfileManager();

            IVibranceService vibranceService = VibranceServiceFactory.Create();
            DisplayManager = new DisplayManager(vibranceService);

            HotkeyManager = new HotkeyManager(hwnd);
            HotkeyManager.HotkeyPressed += HotkeyManager_HotkeyPressed;

            DynamicControls = new DynamicControls(DisplayManager);

            SetupTrayIcon();
            RegisterAllHotkeys();
            LoadSettings();

            // Set up navigation (handler is in XAML)
            NavigateToPage("profiles");

            Logger.Log("MainWindow initialization completed");
        }
        catch (Exception ex)
        {
            Logger.LogError("MainWindow initialization failed", ex);
            System.Windows.MessageBox.Show(
                $"Error loading the application: {ex.Message}",
                "DisplayHub Error",
                System.Windows.MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadSettings()
    {
        minimizeToTray = SettingsManager.GetMinimizeToTray();

        bool dynamicEnabled = SettingsManager.GetDynamicControlsEnabled();
        if (dynamicEnabled && DynamicControls != null && HotkeyManager != null)
        {
            DynamicControls.IsEnabled = true;
            HotkeyManager.UnregisterAllHotkeys();

            var helper = new WindowInteropHelper(this);
            DynamicControls.RegisterHotkeys(HotkeyManager, helper.Handle);
            DynamicControls.SetValues(DynamicControls.Gamma, DynamicControls.Contrast, DynamicControls.Vibrance);
            UpdateTrayProfilesMenu();
        }
    }

    // ─── Navigation ────────────────────────────────────────────────────

    private void NavigationView_SelectionChanged(Wpf.Ui.Controls.NavigationView sender, RoutedEventArgs args)
    {
        if (sender.SelectedItem is Wpf.Ui.Controls.NavigationViewItem item && item.Tag is string tag)
        {
            NavigateToPage(tag);
        }
    }

    private void NavigateToPage(string tag)
    {
        switch (tag)
        {
            case "profiles":
                profilesPage ??= new ProfilesPage(this);
                ContentFrame.Navigate(profilesPage);
                break;
            case "dynamic":
                dynamicControlsPage ??= new DynamicControlsPage(this);
                ContentFrame.Navigate(dynamicControlsPage);
                break;
            case "settings":
                settingsPage ??= new SettingsPage(this);
                ContentFrame.Navigate(settingsPage);
                break;
            case "about":
                aboutPage ??= new AboutPage();
                ContentFrame.Navigate(aboutPage);
                break;
        }
    }

    // ─── Hotkey Handling (WndProc) ─────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == AppConstants.WM_HOTKEY)
        {
            if (DynamicControls is { IsEnabled: true })
            {
                if (DynamicControls.ProcessHotkey(wParam))
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                return IntPtr.Zero;
            }

            HotkeyManager?.ProcessHotkey(wParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void HotkeyManager_HotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        if (DynamicControls is { IsEnabled: true })
        {
            Logger.Log("Profile hotkey ignored - Dynamic Controls active");
            return;
        }

        ApplyProfile(e.Profile);
    }

    // ─── Profile Management ────────────────────────────────────────────

    internal void RegisterAllHotkeys()
    {
        if (HotkeyManager == null || ProfileManager == null) return;

        HotkeyManager.UnregisterAllHotkeys();

        foreach (Profile profile in ProfileManager.Profiles)
        {
            if (profile.HotKey == 0) continue;

            int id = HotkeyManager.RegisterHotkey(profile);
            if (id > 0)
            {
                profile.HotkeyId = id;
            }
            else
            {
                profile.HotKey = 0;
                profile.HotKeyModifier = 0;
                profile.HotkeyId = -1;
            }
        }
    }

    internal void ApplyProfile(Profile profile)
    {
        if (profile == null || DisplayManager == null) return;

        CurrentProfile = profile;
        DisplayManager.ApplySettings(profile.Gamma, profile.Contrast, profile.Vibrance);

        // Update UI if profiles page is active
        profilesPage?.OnProfileApplied(profile);
    }

    // ─── System Tray ───────────────────────────────────────────────────

    private void SetupTrayIcon()
    {
        try
        {
            // Use System.Drawing.Icon for tray (WPF doesn't have native tray support)
            System.Drawing.Icon appIcon;
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using var iconStream = assembly.GetManifestResourceStream("DisplayHub.logo.ico");
                appIcon = iconStream != null
                    ? new System.Drawing.Icon(iconStream)
                    : System.Drawing.SystemIcons.Application;
            }
            catch
            {
                appIcon = System.Drawing.SystemIcons.Application;
            }

            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = appIcon,
                Text = AppConstants.ApplicationName,
                Visible = true
            };

            trayContextMenu = new System.Windows.Forms.ContextMenuStrip();
            trayProfilesMenu = new System.Windows.Forms.ToolStripMenuItem("Profiles");
            trayContextMenu.Items.Add(trayProfilesMenu);
            UpdateTrayProfilesMenu();
            trayContextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayContextMenu.Items.Add("Show", null, (s, e) => ShowWindow());
            trayContextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            trayIcon.ContextMenuStrip = trayContextMenu;
            trayIcon.MouseDoubleClick += (s, e) => ShowWindow();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to set up tray icon", ex);
        }
    }

    internal void UpdateTrayProfilesMenu()
    {
        if (ProfileManager == null || trayProfilesMenu == null) return;

        trayProfilesMenu.DropDownItems.Clear();
        trayProfilesMenu.Enabled = DynamicControls == null || !DynamicControls.IsEnabled;

        foreach (Profile profile in ProfileManager.Profiles)
        {
            var item = new System.Windows.Forms.ToolStripMenuItem(profile.Name) { Tag = profile };
            item.Click += (s, _) =>
            {
                if (s is System.Windows.Forms.ToolStripMenuItem menuItem && menuItem.Tag is Profile p)
                    ApplyProfile(p);
            };
            trayProfilesMenu.DropDownItems.Add(item);
        }
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        CleanupResources();
        System.Windows.Application.Current.Shutdown();
    }

    // ─── Lifecycle ─────────────────────────────────────────────────────

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (minimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        CleanupResources();
    }

    private void CleanupResources()
    {
        if (resourcesCleaned) return;

        if (DynamicControls != null && HotkeyManager != null)
        {
            DynamicControls.UnregisterHotkeys(HotkeyManager);
        }

        if (HotkeyManager != null)
        {
            HotkeyManager.HotkeyPressed -= HotkeyManager_HotkeyPressed;
            HotkeyManager.UnregisterAllHotkeys();
        }

        if (DisplayManager != null)
        {
            DisplayManager.ResetToDefault();
            DisplayManager.Dispose();
        }

        hwndSource?.RemoveHook(WndProc);

        if (trayIcon != null)
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        trayContextMenu?.Dispose();
        resourcesCleaned = true;
    }
}
