using Microsoft.Win32;
using DisplayHub.Constants;
using DisplayHub.Services.Logging;

namespace DisplayHub.Services.Settings;

/// <summary>
/// Manages application settings persisted in the Windows Registry.
/// </summary>
public interface ISettingsManager
{
    bool GetStartWithWindows();
    void SetStartWithWindows(bool enabled);
    bool GetMinimizeToTray();
    void SetMinimizeToTray(bool enabled);
    bool GetDynamicControlsEnabled();
    void SetDynamicControlsEnabled(bool enabled);
}

/// <summary>
/// Registry-based settings persistence for DisplayHub.
/// </summary>
public class SettingsManager : ISettingsManager
{
    public bool GetStartWithWindows()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(AppConstants.StartupRegistryKey, false);
            if (key == null) return false;

            string? value = key.GetValue(AppConstants.ApplicationName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to read startup setting", ex);
            return false;
        }
    }

    public void SetStartWithWindows(bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(AppConstants.StartupRegistryKey, true);
            if (key == null)
            {
                Logger.LogError("Failed to open startup registry key");
                return;
            }

            if (enabled)
            {
                string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                {
                    key.SetValue(AppConstants.ApplicationName, exePath);
                    Logger.Log($"Added to Windows startup: {exePath}");
                }
            }
            else
            {
                key.DeleteValue(AppConstants.ApplicationName, false);
                Logger.Log("Removed from Windows startup");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update startup setting", ex);
            throw;
        }
    }

    public bool GetMinimizeToTray()
    {
        return GetDwordSetting(AppConstants.MinimizeToTrayValue, defaultValue: true);
    }

    public void SetMinimizeToTray(bool enabled)
    {
        SetDwordSetting(AppConstants.MinimizeToTrayValue, enabled);
    }

    public bool GetDynamicControlsEnabled()
    {
        return GetDwordSetting(AppConstants.DynamicControlsEnabledValue, defaultValue: false);
    }

    public void SetDynamicControlsEnabled(bool enabled)
    {
        SetDwordSetting(AppConstants.DynamicControlsEnabledValue, enabled);
    }

    private bool GetDwordSetting(string valueName, bool defaultValue)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(AppConstants.SettingsRegistryKey, false);
            if (key == null) return defaultValue;

            object? value = key.GetValue(valueName);
            if (value == null) return defaultValue;

            return Convert.ToInt32(value) != 0;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to read setting '{valueName}'", ex);
            return defaultValue;
        }
    }

    private void SetDwordSetting(string valueName, bool enabled)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(AppConstants.SettingsRegistryKey, true);
            if (key == null)
            {
                Logger.LogError($"Failed to create registry key for setting '{valueName}'");
                return;
            }

            key.SetValue(valueName, enabled ? 1 : 0, RegistryValueKind.DWord);
            Logger.Log($"Setting '{valueName}' saved: {enabled}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save setting '{valueName}'", ex);
            throw;
        }
    }
}
