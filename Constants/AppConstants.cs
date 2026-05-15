// SPDX-License-Identifier: GPL-3.0-or-later
namespace DispHub.Constants;

public static class AppConstants
{
    public const string AppName = "DispHub";
    public const string Version = "1.0.0";
    public const string AppDataFolder = "DispHub";
    public const string ProfilesFileName = "profiles.json";
    public const string SettingsFileName = "settings.json";
    public const string LogFileName = "disphub.log";

    public static string AppDataPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppDataFolder);

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

    // Color Temperature (Night Light)
    // Slider value mapping:
    // 0   = 2700K (warmest)
    // 50  = 6500K (neutral white point)
    // 100 = 9500K (coolest)
    public const int ColorTempMin = 0;
    public const int ColorTempMax = 100;
    public const int ColorTempNeutralValue = 50;
    public const int ColorTempMinKelvin = 2700;
    public const int ColorTempNeutralKelvin = 6500;
    public const int ColorTempMaxKelvin = 9500;
    public const int ColorTempDefault = ColorTempNeutralValue;
    public const int ColorTempStep = 5;

    // Gamma Ramp
    public const int GammaRampSize = 256;
    public const double GammaRampMaxValue = 65535.0;

    // NVIDIA Specifics
    public const int NvidiaVibranceMin = 0;
    public const int NvidiaVibranceMax = 63;
    public const int NvidiaVibranceDefault = 0;

    // Hotkey Modifiers
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // Startup Registry
    public const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
}
