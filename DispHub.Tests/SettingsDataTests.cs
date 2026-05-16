using DispHub.Constants;
using DispHub.Services.Settings;

namespace DispHub.Tests;

public class SettingsDataTests
{
    [Fact]
    public void DefaultSettings_HaveExpectedValues()
    {
        var settings = new SettingsData();

        Assert.False(settings.StartWithWindows);
        Assert.False(settings.CloseToTray);
        Assert.Equal(0, settings.AppTheme);
        Assert.Equal(0, settings.AccentColor);
        Assert.Equal(0, settings.TrayLeftClickBehavior);
        Assert.False(settings.DynamicControlsEnabled);
        Assert.Equal(0u, settings.DcToggleKey);
        Assert.Equal(0u, settings.DcToggleMod);
        Assert.Equal(0u, settings.MasterToggleKey);
        Assert.Equal(0u, settings.MasterToggleMod);
        Assert.Equal(0, settings.LastActiveProfileIndex);
    }

    [Fact]
    public void DefaultSettings_TaskbarWidgetDefaults()
    {
        var settings = new SettingsData();

        Assert.False(settings.TaskbarWidgetEnabled);
        Assert.Equal(0, settings.TaskbarWidgetPosition);
        Assert.Equal(0, settings.TaskbarWidgetManualPadding);
        Assert.True(settings.TaskbarWidgetAutoPadding);
        Assert.True(settings.TaskbarWidgetClickable);
        Assert.False(settings.TaskbarWidgetBackgroundBlur);
        Assert.False(settings.TaskbarWidgetHideWhenInactive);
    }

    [Fact]
    public void DefaultSettings_FlyoutDefaults()
    {
        var settings = new SettingsData();

        Assert.True(settings.FlyoutEnabled);
        Assert.Equal(1800, settings.FlyoutDuration);
    }

    [Fact]
    public void SettingsData_PropertiesAreSettable()
    {
        var settings = new SettingsData
        {
            StartWithWindows = true,
            CloseToTray = true,
            AppTheme = 2,
            AccentColor = 5,
            TrayLeftClickBehavior = 1,
            DynamicControlsEnabled = true,
            DcToggleKey = 0x41,
            DcToggleMod = AppConstants.MOD_CONTROL,
            MasterToggleKey = 0x42,
            MasterToggleMod = AppConstants.MOD_ALT,
            LastActiveProfileIndex = 3,
            TaskbarWidgetEnabled = true,
            TaskbarWidgetPosition = 2,
            TaskbarWidgetManualPadding = 10,
            TaskbarWidgetAutoPadding = false,
            TaskbarWidgetClickable = false,
            TaskbarWidgetBackgroundBlur = true,
            TaskbarWidgetHideWhenInactive = true,
            FlyoutEnabled = false,
            FlyoutDuration = 2500
        };

        Assert.True(settings.StartWithWindows);
        Assert.True(settings.CloseToTray);
        Assert.Equal(2, settings.AppTheme);
        Assert.Equal(5, settings.AccentColor);
        Assert.Equal(1, settings.TrayLeftClickBehavior);
        Assert.True(settings.DynamicControlsEnabled);
        Assert.Equal(0x41u, settings.DcToggleKey);
        Assert.Equal(AppConstants.MOD_CONTROL, settings.DcToggleMod);
        Assert.Equal(0x42u, settings.MasterToggleKey);
        Assert.Equal(AppConstants.MOD_ALT, settings.MasterToggleMod);
        Assert.Equal(3, settings.LastActiveProfileIndex);
        Assert.True(settings.TaskbarWidgetEnabled);
        Assert.Equal(2, settings.TaskbarWidgetPosition);
        Assert.Equal(10, settings.TaskbarWidgetManualPadding);
        Assert.False(settings.TaskbarWidgetAutoPadding);
        Assert.False(settings.TaskbarWidgetClickable);
        Assert.True(settings.TaskbarWidgetBackgroundBlur);
        Assert.True(settings.TaskbarWidgetHideWhenInactive);
        Assert.False(settings.FlyoutEnabled);
        Assert.Equal(2500, settings.FlyoutDuration);
    }

    [Fact]
    public void DcKeybindSettings_DefaultsAreShiftModifier()
    {
        var keybinds = new DcKeybindSettings();

        Assert.Equal(AppConstants.MOD_SHIFT, keybinds.GammaUpMod);
        Assert.Equal(AppConstants.MOD_SHIFT, keybinds.GammaDownMod);
        Assert.Equal(AppConstants.MOD_SHIFT, keybinds.ContrastUpMod);
        Assert.Equal(AppConstants.MOD_SHIFT, keybinds.ContrastDownMod);
        Assert.Equal(AppConstants.MOD_SHIFT, keybinds.VibranceUpMod);
        Assert.Equal(AppConstants.MOD_SHIFT, keybinds.VibranceDownMod);
    }

    [Fact]
    public void DcKeybindSettings_DefaultKeys_AreArrowAndPageKeys()
    {
        var keybinds = new DcKeybindSettings();

        Assert.Equal(0x26u, keybinds.GammaUpKey);    // VK_UP
        Assert.Equal(0x28u, keybinds.GammaDownKey);   // VK_DOWN
        Assert.Equal(0x27u, keybinds.ContrastUpKey);   // VK_RIGHT
        Assert.Equal(0x25u, keybinds.ContrastDownKey);  // VK_LEFT
        Assert.Equal(0x21u, keybinds.VibranceUpKey);    // VK_PRIOR
        Assert.Equal(0x22u, keybinds.VibranceDownKey);  // VK_NEXT
    }

    [Fact]
    public void DcKeybindSettings_PropertiesAreSettable()
    {
        var keybinds = new DcKeybindSettings
        {
            GammaUpKey = 0x57,
            GammaUpMod = AppConstants.MOD_CONTROL,
            GammaDownKey = 0x53,
            GammaDownMod = AppConstants.MOD_ALT,
            ContrastUpKey = 0x44,
            ContrastUpMod = AppConstants.MOD_WIN,
            ContrastDownKey = 0x41,
            ContrastDownMod = AppConstants.MOD_SHIFT,
            VibranceUpKey = 0x45,
            VibranceUpMod = AppConstants.MOD_CONTROL,
            VibranceDownKey = 0x51,
            VibranceDownMod = AppConstants.MOD_ALT
        };

        Assert.Equal(0x57u, keybinds.GammaUpKey);
        Assert.Equal(AppConstants.MOD_CONTROL, keybinds.GammaUpMod);
        Assert.Equal(0x53u, keybinds.GammaDownKey);
        Assert.Equal(AppConstants.MOD_ALT, keybinds.GammaDownMod);
    }

    [Fact]
    public void SettingsData_DcKeybinds_IsInitializedByDefault()
    {
        var settings = new SettingsData();
        Assert.NotNull(settings.DcKeybinds);
    }

    [Fact]
    public void SettingsData_Serialization_RoundTrips()
    {
        var settings = new SettingsData
        {
            AppTheme = 2,
            FlyoutDuration = 3000,
            TaskbarWidgetPosition = 1
        };

        string json = System.Text.Json.JsonSerializer.Serialize(settings);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized!.AppTheme);
        Assert.Equal(3000, deserialized.FlyoutDuration);
        Assert.Equal(1, deserialized.TaskbarWidgetPosition);
    }
}
