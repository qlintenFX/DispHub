namespace DisplayHub.Constants;

/// <summary>
/// Centralized constants for the DisplayHub application.
/// </summary>
public static class AppConstants
{
    public const string ApplicationName = "DisplayHub";
    public const string Version = "3.0.0";

    // Registry Keys
    public const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public const string SettingsRegistryKey = @"SOFTWARE\DisplayHub";
    public const string MinimizeToTrayValue = "MinimizeToTray";
    public const string DynamicControlsEnabledValue = "DynamicControlsEnabled";

    // Display Limits
    public const double GammaMin = 0.30;
    public const double GammaMax = 2.80;
    public const double GammaDefault = 1.0;
    public const double GammaStep = 0.05;

    public const double ContrastMin = 0.0;
    public const double ContrastMax = 1.0;
    public const double ContrastDefault = 0.5;
    public const double ContrastStep = 0.05;

    public const int VibranceMin = 0;
    public const int VibranceMax = 100;
    public const int VibranceDefault = 50;
    public const int VibranceStep = 5;

    // Gamma Ramp
    public const int GammaRampSize = 256;
    public const double GammaRampMaxValue = 65535.0;

    // NVIDIA Specifics
    public const int NvidiaMaxPhysicalGpus = 64;
    public const int NvidiaVibranceMin = 0;
    public const int NvidiaVibranceMax = 63;
    public const int NvidiaVibranceDefault = 0;

    // Windows Messages
    public const int WM_HOTKEY = 0x0312;

    // Hotkey Modifiers (Windows API values)
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // Logging
    public const string DefaultLogFileName = "displayhub.log";
    public const string ErrorLogFileName = "displayhub_error.log";

    // Profile Persistence
    public const string ProfilesFolderName = "DisplayHub";
    public const string ProfilesFileName = "profiles.json";
}
