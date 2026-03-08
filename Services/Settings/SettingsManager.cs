using System.Text.Json;
using DisplayHub.Constants;
using DisplayHub.Services.Logging;
using Microsoft.Win32;

namespace DisplayHub.Services.Settings;

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
    public int TrayLeftClickBehavior { get; set; }  // 0=Open Settings, 1=Do Nothing
    public bool DynamicControlsEnabled { get; set; }
    public uint DcToggleKey { get; set; }     // VK code, 0 = no toggle hotkey
    public uint DcToggleMod { get; set; }     // Modifier bitmask
    public DcKeybindSettings DcKeybinds { get; set; } = new();
    public bool TaskbarWidgetEnabled { get; set; }
    public int TaskbarWidgetPosition { get; set; }  // 0=Left, 1=Center, 2=Right
    public int TaskbarWidgetPadding { get; set; } = 10;
}

public class SettingsManager
{
    private SettingsData _data;
    private readonly string _settingsFilePath;

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

    public int TaskbarWidgetPadding
    {
        get => _data.TaskbarWidgetPadding;
        set { _data.TaskbarWidgetPadding = value; Save(); }
    }

    public void SaveDcKeybinds() => Save();

    public SettingsManager()
    {
        string appDataPath = AppConstants.AppDataPath;
        if (!Directory.Exists(appDataPath))
            Directory.CreateDirectory(appDataPath);

        _settingsFilePath = Path.Combine(appDataPath, AppConstants.SettingsFileName);
        _data = Load();
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
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_data, options);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save settings", ex);
        }
    }

    private void ApplyStartWithWindows(bool enabled)
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
