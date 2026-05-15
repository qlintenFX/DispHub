// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Windows.Threading;
using DispHub.Constants;
using DispHub.Services.Logging;
using Microsoft.Win32;

namespace DispHub.Services.Settings;

public class DcKeybindSettings
{
    public uint GammaUpKey { get; set; } = 0x26;       // VK_UP
    public uint GammaUpMod { get; set; } = AppConstants.MOD_SHIFT;
    public uint GammaDownKey { get; set; } = 0x28;     // VK_DOWN
    public uint GammaDownMod { get; set; } = AppConstants.MOD_SHIFT;
    public uint ContrastUpKey { get; set; } = 0x27;    // VK_RIGHT
    public uint ContrastUpMod { get; set; } = AppConstants.MOD_SHIFT;
    public uint ContrastDownKey { get; set; } = 0x25;  // VK_LEFT
    public uint ContrastDownMod { get; set; } = AppConstants.MOD_SHIFT;
    public uint VibranceUpKey { get; set; } = 0x21;    // VK_PRIOR (Page Up)
    public uint VibranceUpMod { get; set; } = AppConstants.MOD_SHIFT;
    public uint VibranceDownKey { get; set; } = 0x22;  // VK_NEXT (Page Down)
    public uint VibranceDownMod { get; set; } = AppConstants.MOD_SHIFT;
}

public class SettingsData
{
    public bool StartWithWindows { get; set; }
    public bool CloseToTray { get; set; }
    public int AppTheme { get; set; }  // 0=System, 1=Light, 2=Dark
    public int AccentColor { get; set; }  // 0=System, 1-10=Preset colors
    public int TrayLeftClickBehavior { get; set; }  // 0=Open Settings, 1=Do Nothing
    public bool DynamicControlsEnabled { get; set; }
    public uint DcToggleKey { get; set; }     // VK code, 0 = no toggle hotkey
    public uint DcToggleMod { get; set; }     // Modifier bitmask
    public uint MasterToggleKey { get; set; }  // VK code for master on/off toggle
    public uint MasterToggleMod { get; set; }  // Modifier bitmask for master toggle
    public DcKeybindSettings DcKeybinds { get; set; } = new();
    public int LastActiveProfileIndex { get; set; }  // Persisted active profile
    public bool TaskbarWidgetEnabled { get; set; }
    public int TaskbarWidgetPosition { get; set; }  // 0=Left, 1=Center, 2=Right
    public int TaskbarWidgetManualPadding { get; set; }
    public bool TaskbarWidgetAutoPadding { get; set; } = true;
    public bool TaskbarWidgetClickable { get; set; } = true;
    public bool TaskbarWidgetBackgroundBlur { get; set; }
    public bool TaskbarWidgetHideWhenInactive { get; set; }
    public bool FlyoutEnabled { get; set; } = true;
    public int FlyoutDuration { get; set; } = 1800;  // ms
}

public class SettingsManager
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private readonly SettingsData _data;
    private readonly string _settingsFilePath;
    private readonly DispatcherTimer _saveDebounceTimer;
    private bool _savePending;

    public bool StartWithWindows
    {
        get => _data.StartWithWindows;
        set
        {
            _data.StartWithWindows = value;
            ApplyStartWithWindows(value);
            Save();
        }
    }

    public bool CloseToTray
    {
        get => _data.CloseToTray;
        set { _data.CloseToTray = value; Save(); }
    }

    public int AppTheme
    {
        get => _data.AppTheme;
        set { _data.AppTheme = value; Save(); }
    }

    public int AccentColor
    {
        get => _data.AccentColor;
        set { _data.AccentColor = value; Save(); }
    }

    public bool DynamicControlsEnabled
    {
        get => _data.DynamicControlsEnabled;
        set { _data.DynamicControlsEnabled = value; Save(); }
    }

    public uint DcToggleKey
    {
        get => _data.DcToggleKey;
        set { _data.DcToggleKey = value; Save(); }
    }

    public uint DcToggleMod
    {
        get => _data.DcToggleMod;
        set { _data.DcToggleMod = value; Save(); }
    }

    public uint MasterToggleKey
    {
        get => _data.MasterToggleKey;
        set { _data.MasterToggleKey = value; Save(); }
    }

    public uint MasterToggleMod
    {
        get => _data.MasterToggleMod;
        set { _data.MasterToggleMod = value; Save(); }
    }

    public DcKeybindSettings DcKeybinds
    {
        get => _data.DcKeybinds;
        set { _data.DcKeybinds = value; Save(); }
    }

    public int TrayLeftClickBehavior
    {
        get => _data.TrayLeftClickBehavior;
        set { _data.TrayLeftClickBehavior = value; Save(); }
    }

    public int LastActiveProfileIndex
    {
        get => _data.LastActiveProfileIndex;
        set { _data.LastActiveProfileIndex = value; Save(); }
    }

    public bool TaskbarWidgetEnabled
    {
        get => _data.TaskbarWidgetEnabled;
        set { _data.TaskbarWidgetEnabled = value; Save(); }
    }

    public int TaskbarWidgetPosition
    {
        get => _data.TaskbarWidgetPosition;
        set { _data.TaskbarWidgetPosition = value; Save(); }
    }

    public int TaskbarWidgetManualPadding
    {
        get => _data.TaskbarWidgetManualPadding;
        set { _data.TaskbarWidgetManualPadding = value; Save(); }
    }

    public bool TaskbarWidgetAutoPadding
    {
        get => _data.TaskbarWidgetAutoPadding;
        set { _data.TaskbarWidgetAutoPadding = value; Save(); }
    }

    public bool TaskbarWidgetClickable
    {
        get => _data.TaskbarWidgetClickable;
        set { _data.TaskbarWidgetClickable = value; Save(); }
    }

    public bool TaskbarWidgetBackgroundBlur
    {
        get => _data.TaskbarWidgetBackgroundBlur;
        set { _data.TaskbarWidgetBackgroundBlur = value; Save(); }
    }

    public bool TaskbarWidgetHideWhenInactive
    {
        get => _data.TaskbarWidgetHideWhenInactive;
        set { _data.TaskbarWidgetHideWhenInactive = value; Save(); }
    }

    public bool FlyoutEnabled
    {
        get => _data.FlyoutEnabled;
        set { _data.FlyoutEnabled = value; Save(); }
    }

    public int FlyoutDuration
    {
        get => _data.FlyoutDuration;
        set { _data.FlyoutDuration = value; Save(); }
    }

    public void SaveDcKeybinds() => Save();

    public SettingsManager()
    {
        string appDataPath = AppConstants.AppDataPath;
        if (!Directory.Exists(appDataPath))
            Directory.CreateDirectory(appDataPath);

        _settingsFilePath = Path.Combine(appDataPath, AppConstants.SettingsFileName);
        _data = Load();

        // Debounce saves to avoid blocking UI on rapid changes
        _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            if (_savePending)
            {
                _savePending = false;
                _ = SaveToFileAsync();
            }
        };
    }

    private SettingsData Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load settings", ex);
        }
        return new SettingsData();
    }

    private void Save()
    {
        // Debounce: mark pending and restart timer
        _savePending = true;
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private async Task SaveToFileAsync()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, _jsonOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save settings", ex);
        }
    }

    private static void ApplyStartWithWindows(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(AppConstants.StartupRegistryKey, true);
            if (key == null) return;
            if (enabled)
            {
                string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(appPath))
                    key.SetValue(AppConstants.AppName, appPath);
            }
            else
            {
                key.DeleteValue(AppConstants.AppName, false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update startup setting", ex);
        }
    }
}
