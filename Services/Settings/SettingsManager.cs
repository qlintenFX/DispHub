using System.Text.Json;
using DisplayHub.Constants;
using DisplayHub.Services.Logging;
using Microsoft.Win32;

namespace DisplayHub.Services.Settings;

public class SettingsData
{
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool DynamicControlsEnabled { get; set; } = false;
    public bool ResetOnExit { get; set; } = false;
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

    public bool MinimizeToTray
    {
        get => _data.MinimizeToTray;
        set { _data.MinimizeToTray = value; Save(); }
    }

    public bool DynamicControlsEnabled
    {
        get => _data.DynamicControlsEnabled;
        set { _data.DynamicControlsEnabled = value; Save(); }
    }

    public bool ResetOnExit
    {
        get => _data.ResetOnExit;
        set { _data.ResetOnExit = value; Save(); }
    }

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
