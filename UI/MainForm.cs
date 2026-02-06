using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using KeyedColors.Constants;
using KeyedColors.Models;
using KeyedColors.Services.Display;
using KeyedColors.Services.Hotkeys;
using KeyedColors.Services.Logging;
using KeyedColors.Services.Profiles;
using KeyedColors.Services.Settings;
using KeyedColors.UI.Dialogs;

namespace KeyedColors.UI;

public partial class MainForm : Form
{
    private ProfileManager? profileManager;
    private DisplayManager? displayManager;
    private HotkeyManager? hotkeyManager;
    private DynamicControls? dynamicControls;
    private NotifyIcon? trayIcon;
    private ToolStripMenuItem? trayProfilesMenu;
    private ContextMenuStrip? trayContextMenu;
    private Profile? currentProfile;
    private bool isMinimized;
    private bool resourcesCleaned;
    private bool minimizeToTray = true;

    private readonly ISettingsManager settingsManager;

    public MainForm() : this(new SettingsManager()) { }

    public MainForm(ISettingsManager settingsManager)
    {
        this.settingsManager = settingsManager;

        try
        {
            Logger.Log("MainForm constructor starting");
            InitializeComponent();

            profileManager = new ProfileManager();

            IVibranceService vibranceService = VibranceServiceFactory.Create();
            displayManager = new DisplayManager(vibranceService);

            hotkeyManager = new HotkeyManager(Handle);
            hotkeyManager.HotkeyPressed += HotkeyManager_HotkeyPressed;

            dynamicControls = new DynamicControls(displayManager);
            dynamicControls.ValuesChanged += DynamicControls_ValuesChanged;

            SetupTrayIcon();
            LoadProfilesToUI();
            RegisterAllHotkeys();
            UpdateDynamicControlsUI();

            LoadSettings();

            Logger.Log("MainForm initialization completed");
        }
        catch (Exception ex)
        {
            Logger.LogError("MainForm initialization failed", ex);
            MessageBox.Show(
                $"Error loading the application: {ex.Message}",
                "Load Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void LoadSettings()
    {
        // Start with Windows
        bool startupEnabled = settingsManager.GetStartWithWindows();
        startWithWindowsCheckBox.CheckedChanged -= startWithWindowsCheckBox_CheckedChanged;
        startWithWindowsCheckBox.Checked = startupEnabled;
        startWithWindowsCheckBox.CheckedChanged += startWithWindowsCheckBox_CheckedChanged;

        // Minimize to tray
        minimizeToTray = settingsManager.GetMinimizeToTray();
        minimizeToTrayCheckBox.CheckedChanged -= minimizeToTrayCheckBox_CheckedChanged;
        minimizeToTrayCheckBox.Checked = minimizeToTray;
        minimizeToTrayCheckBox.CheckedChanged += minimizeToTrayCheckBox_CheckedChanged;

        // Dynamic controls state
        bool dynamicEnabled = settingsManager.GetDynamicControlsEnabled();
        if (dynamicEnabled && dynamicControls != null && hotkeyManager != null)
        {
            dynamicControlsToggle.CheckedChanged -= dynamicControlsToggle_CheckedChanged;
            dynamicControlsToggle.Checked = true;
            dynamicControls.IsEnabled = true;

            // Unregister profile hotkeys to avoid conflicts with dynamic hotkeys
            hotkeyManager.UnregisterAllHotkeys();

            dynamicControls.RegisterHotkeys(hotkeyManager, Handle);
            dynamicControls.SetValues(dynamicControls.Gamma, dynamicControls.Contrast, dynamicControls.Vibrance);
            SetProfileControlsEnabled(false);
            dynamicControlsToggle.CheckedChanged += dynamicControlsToggle_CheckedChanged;
            UpdateTrayProfilesMenu();
        }
    }

    // ─── Resource Cleanup ──────────────────────────────────────────────

    private void CleanupResources()
    {
        if (resourcesCleaned) return;

        if (dynamicControls != null)
        {
            dynamicControls.ValuesChanged -= DynamicControls_ValuesChanged;
            if (hotkeyManager != null)
                dynamicControls.UnregisterHotkeys(hotkeyManager);
        }

        if (hotkeyManager != null)
        {
            hotkeyManager.HotkeyPressed -= HotkeyManager_HotkeyPressed;
            hotkeyManager.UnregisterAllHotkeys();
        }

        if (displayManager != null)
        {
            displayManager.ResetToDefault();
            displayManager.Dispose();
            displayManager = null;
        }

        if (trayIcon != null)
        {
            trayIcon.MouseDoubleClick -= TrayIcon_MouseDoubleClick;
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }

        if (trayContextMenu != null)
        {
            trayContextMenu.Dispose();
            trayContextMenu = null;
        }

        trayProfilesMenu = null;
        resourcesCleaned = true;
    }

    // ─── WndProc (Hotkey Handling) ─────────────────────────────────────

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == AppConstants.WM_HOTKEY)
        {
            if (dynamicControls is { IsEnabled: true })
            {
                if (dynamicControls.ProcessHotkey(m.WParam))
                    return;

                // Dynamic controls active but didn't handle this key - ignore profile hotkeys
                base.WndProc(ref m);
                return;
            }

            hotkeyManager?.ProcessHotkey(m.WParam);
        }

        base.WndProc(ref m);
    }

    private void HotkeyManager_HotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        if (dynamicControls is { IsEnabled: true })
        {
            Logger.Log("Profile hotkey ignored - Dynamic Controls active");
            return;
        }

        ApplyProfile(e.Profile);
    }

    // ─── System Tray ───────────────────────────────────────────────────

    private void SetupTrayIcon()
    {
        try
        {
            Icon appIcon = LoadApplicationIcon();

            trayIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = AppConstants.ApplicationName,
                Visible = true
            };

            trayContextMenu = new ContextMenuStrip();
            trayProfilesMenu = new ToolStripMenuItem("Profiles");
            trayContextMenu.Items.Add(trayProfilesMenu);
            UpdateTrayProfilesMenu();
            trayContextMenu.Items.Add(new ToolStripSeparator());
            trayContextMenu.Items.Add("Show", null, ShowForm_Click);
            trayContextMenu.Items.Add("Exit", null, Exit_Click);

            trayIcon.ContextMenuStrip = trayContextMenu;
            trayIcon.MouseDoubleClick += TrayIcon_MouseDoubleClick;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to set up tray icon", ex);
            try
            {
                trayIcon = new NotifyIcon
                {
                    Icon = SystemIcons.Application,
                    Text = AppConstants.ApplicationName,
                    Visible = true
                };
            }
            catch (Exception ex2)
            {
                Logger.LogError("Critical: Failed to create fallback tray icon", ex2);
            }
        }
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            return Properties.Resources.AppIcon;
        }
        catch
        {
            // Fall through to embedded resource
        }

        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream? iconStream = assembly.GetManifestResourceStream("KeyedColors.logo.ico");
            if (iconStream != null)
                return new Icon(iconStream);
        }
        catch
        {
            // Fall through to file system
        }

        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath))
                return new Icon(iconPath);
        }
        catch
        {
            // Fall through to executable icon
        }

        try
        {
            Icon? exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null)
                return exeIcon;
        }
        catch
        {
            // Fall through to system icon
        }

        return SystemIcons.Application;
    }

    private void UpdateTrayProfilesMenu()
    {
        if (profileManager == null || trayProfilesMenu == null) return;

        trayProfilesMenu.DropDownItems.Clear();
        trayProfilesMenu.Enabled = dynamicControls == null || !dynamicControls.IsEnabled;

        foreach (Profile profile in profileManager.Profiles)
        {
            var item = new ToolStripMenuItem(profile.Name) { Tag = profile };
            item.Click += (s, _) =>
            {
                if (s is ToolStripMenuItem menuItem && menuItem.Tag is Profile p)
                    ApplyProfile(p);
            };
            trayProfilesMenu.DropDownItems.Add(item);
        }
    }

    private void TrayIcon_MouseDoubleClick(object? sender, MouseEventArgs e) => ShowForm_Click(sender, e);

    private void ShowForm_Click(object? sender, EventArgs e)
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        isMinimized = false;
    }

    private void Exit_Click(object? sender, EventArgs e) => Application.Exit();

    // ─── Profile Management ────────────────────────────────────────────

    private void LoadProfilesToUI()
    {
        if (profileManager == null || profileListBox == null) return;

        profileListBox.Items.Clear();
        foreach (Profile profile in profileManager.Profiles)
            profileListBox.Items.Add(profile);

        if (profileListBox.Items.Count > 0)
            profileListBox.SelectedIndex = 0;

        UpdateTrayProfilesMenu();
    }

    private void RegisterAllHotkeys()
    {
        if (hotkeyManager == null || profileManager == null) return;

        hotkeyManager.UnregisterAllHotkeys();

        foreach (Profile profile in profileManager.Profiles)
        {
            if (profile.HotKey == Keys.None) continue;

            int id = hotkeyManager.RegisterHotkey(profile);
            if (id > 0)
            {
                profile.HotkeyId = id;
            }
            else
            {
                profile.HotKey = Keys.None;
                profile.HotKeyModifier = Keys.None;
                profile.HotkeyId = -1;
            }
        }
    }

    private void ApplyProfile(Profile profile)
    {
        if (profile == null || displayManager == null) return;

        currentProfile = profile;
        displayManager.ApplySettings(profile.Gamma, profile.Contrast, profile.Vibrance);

        if (!isMinimized)
        {
            if (profileListBox.Items.Contains(profile))
                profileListBox.SelectedItem = profile;

            SetTrackBarValues(gammaTrackBar, (int)(profile.Gamma * 100),
                              contrastTrackBar, (int)(profile.Contrast * 100),
                              vibranceTrackBar, profile.Vibrance);
            UpdateProfileSettingsLabel();
        }
    }

    // ─── Profile Tab: Trackbar + Label ─────────────────────────────────

    private void UpdateProfileSettingsLabel()
    {
        if (gammaTrackBar == null || contrastTrackBar == null || vibranceTrackBar == null || settingsLabel == null) return;

        double gamma = gammaTrackBar.Value / 100.0;
        settingsLabel.Text = $"Gamma: {gamma:F2}, Contrast: {contrastTrackBar.Value}%, Vibrance: {vibranceTrackBar.Value}%";
    }

    private void ProfileTrackBar_ValueChanged(object? sender, EventArgs e)
    {
        UpdateProfileSettingsLabel();

        if (displayManager != null && gammaTrackBar != null && contrastTrackBar != null && vibranceTrackBar != null)
        {
            double gamma = gammaTrackBar.Value / 100.0;
            double contrast = contrastTrackBar.Value / 100.0;
            displayManager.ApplySettings(gamma, contrast, vibranceTrackBar.Value);
        }
    }

    // ─── Profile Tab: Events ───────────────────────────────────────────

    private void profileListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (profileListBox.SelectedItem is not Profile selected) return;

        SetTrackBarValues(gammaTrackBar, (int)(selected.Gamma * 100),
                          contrastTrackBar, (int)(selected.Contrast * 100),
                          vibranceTrackBar, Math.Clamp(selected.Vibrance, 0, 100));

        string hotkeyText = selected.HotKey != Keys.None
            ? $"{selected.HotKeyModifier}+{selected.HotKey}"
            : "None";
        hotkeyLabel.Text = $"Hotkey: {hotkeyText}";

        ApplyProfile(selected);
    }

    private void addProfileButton_Click(object? sender, EventArgs e)
    {
        if (profileManager == null || gammaTrackBar == null || contrastTrackBar == null || vibranceTrackBar == null) return;

        string? name = ProfileNameDialog.Show(this);
        if (name == null) return;

        double gamma = gammaTrackBar.Value / 100.0;
        double contrast = contrastTrackBar.Value / 100.0;
        var newProfile = new Profile(name, gamma, contrast, vibranceTrackBar.Value);
        profileManager.AddProfile(newProfile);

        LoadProfilesToUI();
        profileListBox.SelectedItem = newProfile;
    }

    private void updateProfileButton_Click(object? sender, EventArgs e)
    {
        if (profileManager == null || profileListBox?.SelectedItem is not Profile selected) return;
        if (gammaTrackBar == null || contrastTrackBar == null || vibranceTrackBar == null) return;

        selected.Gamma = gammaTrackBar.Value / 100.0;
        selected.Contrast = contrastTrackBar.Value / 100.0;
        selected.Vibrance = vibranceTrackBar.Value;
        profileManager.SaveProfiles();

        int selectedIndex = profileListBox.SelectedIndex;
        LoadProfilesToUI();
        profileListBox.SelectedIndex = selectedIndex;
    }

    private void deleteProfileButton_Click(object? sender, EventArgs e)
    {
        if (profileManager == null || profileListBox?.SelectedItem is not Profile selected) return;

        DialogResult result = MessageBox.Show(
            $"Are you sure you want to delete the profile '{selected.Name}'?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        if (selected.HotkeyId > 0)
            hotkeyManager?.UnregisterHotkey(selected.HotkeyId);

        int index = profileListBox.SelectedIndex;
        profileManager.RemoveProfile(index);

        LoadProfilesToUI();

        if (index >= profileListBox.Items.Count)
            index = profileListBox.Items.Count - 1;
        if (index >= 0)
            profileListBox.SelectedIndex = index;
    }

    private void setHotkeyButton_Click(object? sender, EventArgs e)
    {
        if (profileListBox?.SelectedItem is not Profile selected) return;
        if (hotkeyManager == null || profileManager == null) return;

        using var form = new Form
        {
            Text = "Set Hotkey",
            ClientSize = new Size(300, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label { Left = 20, Top = 20, Text = "Press a key combination:", AutoSize = true };
        var textBox = new TextBox { Left = 20, Top = 50, Width = 260, ReadOnly = true };

        if (selected.HotKey != Keys.None)
            textBox.Text = $"{selected.HotKeyModifier}+{selected.HotKey}";

        var confirmButton = new Button { Text = "OK", Left = 70, Width = 80, Top = 100, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Left = 160, Width = 80, Top = 100, DialogResult = DialogResult.Cancel };
        form.AcceptButton = confirmButton;
        form.CancelButton = cancelButton;

        Keys modifiers = Keys.None;
        Keys key = Keys.None;

        textBox.KeyDown += (_, ke) =>
        {
            modifiers = Keys.None;
            if (ke.Control) modifiers |= Keys.Control;
            if (ke.Alt) modifiers |= Keys.Alt;
            if (ke.Shift) modifiers |= Keys.Shift;

            key = ke.KeyCode;

            if (modifiers != Keys.None && key != Keys.None &&
                key != Keys.ControlKey && key != Keys.ShiftKey && key != Keys.Menu)
            {
                textBox.Text = $"{modifiers}+{key}";
                ke.Handled = true;
                ke.SuppressKeyPress = true;
            }
        };

        form.Controls.AddRange(new Control[] { label, textBox, confirmButton, cancelButton });

        if (form.ShowDialog(this) != DialogResult.OK || key == Keys.None || modifiers == Keys.None)
            return;

        if (selected.HotkeyId > 0)
            hotkeyManager.UnregisterHotkey(selected.HotkeyId);

        selected.HotKey = key;
        selected.HotKeyModifier = modifiers;

        int id = hotkeyManager.RegisterHotkey(selected);
        if (id > 0)
        {
            selected.HotkeyId = id;
            profileManager.SaveProfiles();

            hotkeyLabel.Text = $"Hotkey: {modifiers}+{key}";
            int selectedIndex = profileListBox.SelectedIndex;
            LoadProfilesToUI();
            profileListBox.SelectedIndex = selectedIndex;
        }
        else
        {
            MessageBox.Show(
                "Failed to register the hotkey. It may be in use by another application.",
                "Hotkey Registration Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void resetButton_Click(object? sender, EventArgs e)
    {
        if (displayManager == null) return;

        displayManager.ResetToDefault();
        SetTrackBarValues(gammaTrackBar, 100, contrastTrackBar, 50, vibranceTrackBar, 50);
        UpdateProfileSettingsLabel();
    }

    // ─── Dynamic Controls ──────────────────────────────────────────────

    private void DynamicControls_ValuesChanged(object? sender, EventArgs e)
    {
        UpdateDynamicControlsUI();
    }

    private void UpdateDynamicControlsUI()
    {
        if (dynamicControls == null) return;
        if (dynamicGammaTrackBar == null || dynamicContrastTrackBar == null || dynamicVibranceTrackBar == null) return;
        if (dynamicSettingsLabel == null) return;

        // Detach → update → reattach to avoid recursive events
        dynamicGammaTrackBar.ValueChanged -= DynamicTrackBar_ValueChanged;
        dynamicContrastTrackBar.ValueChanged -= DynamicTrackBar_ValueChanged;
        dynamicVibranceTrackBar.ValueChanged -= DynamicTrackBar_ValueChanged;

        dynamicGammaTrackBar.Value = (int)(dynamicControls.Gamma * 100);
        dynamicContrastTrackBar.Value = (int)(dynamicControls.Contrast * 100);
        dynamicVibranceTrackBar.Value = dynamicControls.Vibrance;

        dynamicGammaTrackBar.ValueChanged += DynamicTrackBar_ValueChanged;
        dynamicContrastTrackBar.ValueChanged += DynamicTrackBar_ValueChanged;
        dynamicVibranceTrackBar.ValueChanged += DynamicTrackBar_ValueChanged;

        dynamicSettingsLabel.Text = $"Gamma: {dynamicControls.Gamma:F2}, Contrast: {dynamicControls.Contrast * 100:F0}%, Vibrance: {dynamicControls.Vibrance}%";
    }

    private void DynamicTrackBar_ValueChanged(object? sender, EventArgs e)
    {
        if (dynamicControls == null) return;
        if (dynamicGammaTrackBar == null || dynamicContrastTrackBar == null || dynamicVibranceTrackBar == null) return;

        double gamma = dynamicGammaTrackBar.Value / 100.0;
        double contrast = dynamicContrastTrackBar.Value / 100.0;
        int vibrance = dynamicVibranceTrackBar.Value;
        dynamicControls.SetValues(gamma, contrast, vibrance);
        UpdateDynamicControlsUI();
    }

    private void dynamicControlsToggle_CheckedChanged(object? sender, EventArgs e)
    {
        if (dynamicControls == null || hotkeyManager == null || displayManager == null) return;

        bool isEnabled = dynamicControlsToggle.Checked;

        try
        {
            Logger.Log($"Dynamic controls toggled: {isEnabled}");
            dynamicControls.IsEnabled = isEnabled;

            if (isEnabled)
            {
                // Unregister profile hotkeys first to avoid conflicts
                // (e.g., a profile using Shift+PageUp would block dynamic Shift+PageUp)
                hotkeyManager.UnregisterAllHotkeys();

                dynamicControls.RegisterHotkeys(hotkeyManager, Handle);
                dynamicControls.SetValues(dynamicControls.Gamma, dynamicControls.Contrast, dynamicControls.Vibrance);
                SetProfileControlsEnabled(false);
            }
            else
            {
                dynamicControls.UnregisterHotkeys(hotkeyManager);

                // Re-register profile hotkeys now that dynamic controls are off
                RegisterAllHotkeys();

                if (currentProfile != null)
                {
                    ApplyProfile(currentProfile);
                }
                else
                {
                    displayManager.ResetToDefault();
                    SetTrackBarValues(gammaTrackBar, 100, contrastTrackBar, 50, vibranceTrackBar, 50);
                    UpdateProfileSettingsLabel();
                }

                SetProfileControlsEnabled(true);
            }

            settingsManager.SetDynamicControlsEnabled(isEnabled);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to toggle dynamic controls", ex);
            MessageBox.Show(
                $"Failed to toggle dynamic controls: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            dynamicControlsToggle.CheckedChanged -= dynamicControlsToggle_CheckedChanged;
            dynamicControlsToggle.Checked = dynamicControls.IsEnabled;
            dynamicControlsToggle.CheckedChanged += dynamicControlsToggle_CheckedChanged;
        }
        finally
        {
            UpdateTrayProfilesMenu();
        }
    }

    private void dynamicControlsResetButton_Click(object? sender, EventArgs e)
    {
        dynamicControls?.SetValues(AppConstants.GammaDefault, AppConstants.ContrastDefault, AppConstants.VibranceDefault);
        UpdateDynamicControlsUI();
    }

    private void dynamicSaveToProfileButton_Click(object? sender, EventArgs e)
    {
        if (dynamicControls == null || profileManager == null || !dynamicControls.IsEnabled) return;

        string? name = ProfileNameDialog.Show(this, "Dynamic Profile", "Save as Profile");
        if (name == null) return;

        var newProfile = new Profile(name, dynamicControls.Gamma, dynamicControls.Contrast, dynamicControls.Vibrance);
        profileManager.AddProfile(newProfile);
        LoadProfilesToUI();

        MessageBox.Show(
            $"Dynamic settings saved as profile '{name}'",
            "Profile Saved",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    // ─── Settings Tab ──────────────────────────────────────────────────

    private void startWithWindowsCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        try
        {
            settingsManager.SetStartWithWindows(startWithWindowsCheckBox.Checked);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update startup setting", ex);
            MessageBox.Show(
                $"Failed to update startup setting: {ex.Message}",
                "Settings Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            startWithWindowsCheckBox.CheckedChanged -= startWithWindowsCheckBox_CheckedChanged;
            startWithWindowsCheckBox.Checked = settingsManager.GetStartWithWindows();
            startWithWindowsCheckBox.CheckedChanged += startWithWindowsCheckBox_CheckedChanged;
        }
    }

    private void minimizeToTrayCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        try
        {
            minimizeToTray = minimizeToTrayCheckBox.Checked;
            settingsManager.SetMinimizeToTray(minimizeToTray);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update minimize to tray setting", ex);
            MessageBox.Show(
                $"Failed to update minimize to tray setting: {ex.Message}",
                "Settings Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            minimizeToTrayCheckBox.CheckedChanged -= minimizeToTrayCheckBox_CheckedChanged;
            minimizeToTrayCheckBox.Checked = minimizeToTray;
            minimizeToTrayCheckBox.CheckedChanged += minimizeToTrayCheckBox_CheckedChanged;
        }
    }

    // ─── Form Lifecycle ────────────────────────────────────────────────

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && minimizeToTray)
        {
            e.Cancel = true;
            Hide();
            isMinimized = true;
            return;
        }

        CleanupResources();
        base.OnFormClosing(e);
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private void SetProfileControlsEnabled(bool enabled)
    {
        profileListBox.Enabled = enabled;
        addProfileButton.Enabled = enabled;
        updateProfileButton.Enabled = enabled;
        deleteProfileButton.Enabled = enabled;
        setHotkeyButton.Enabled = enabled;
        gammaTrackBar.Enabled = enabled;
        contrastTrackBar.Enabled = enabled;
        vibranceTrackBar.Enabled = enabled;
        resetButton.Enabled = enabled;

        if (trayProfilesMenu != null)
            trayProfilesMenu.Enabled = enabled;
    }

    private static void SetTrackBarValues(TrackBar? gamma, int gammaValue,
                                           TrackBar? contrast, int contrastValue,
                                           TrackBar? vibrance, int vibranceValue)
    {
        if (gamma != null) gamma.Value = Math.Clamp(gammaValue, gamma.Minimum, gamma.Maximum);
        if (contrast != null) contrast.Value = Math.Clamp(contrastValue, contrast.Minimum, contrast.Maximum);
        if (vibrance != null) vibrance.Value = Math.Clamp(vibranceValue, vibrance.Minimum, vibrance.Maximum);
    }
}
