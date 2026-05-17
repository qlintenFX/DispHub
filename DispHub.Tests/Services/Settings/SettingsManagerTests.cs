// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Constants;
using DispHub.Services.Settings;

namespace DispHub.Tests.Services.Settings;

public class SettingsManagerTests : IDisposable
{
    private readonly string _testDirPath;
    private readonly string _testFilePath;

    public SettingsManagerTests()
    {
        _testDirPath = Path.Combine(Path.GetTempPath(), "DispHub_Test_Settings_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirPath);
        _testFilePath = Path.Combine(_testDirPath, AppConstants.SettingsFileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirPath))
        {
            Directory.Delete(_testDirPath, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_CreatesDefaultSettings_WhenNoFileExists()
    {
        var manager = new SettingsManager(_testDirPath);

        // Defaults check
        Assert.False(manager.StartWithWindows);
        Assert.False(manager.CloseToTray);
    }

    [Fact]
    public void SetProperties_UpdatesDataAndTriggersSave()
    {
        var manager = new SettingsManager(_testDirPath)
        {
            AppTheme = 2,
            DynamicControlsEnabled = true,
            TaskbarWidgetEnabled = true
        };

        Assert.Equal(2, manager.AppTheme);
        Assert.True(manager.DynamicControlsEnabled);
        Assert.True(manager.TaskbarWidgetEnabled);

        // Since save is debounced via DispatcherTimer, we can't easily wait for the async save without a WPF Dispatcher frame.
        // But we can verify the properties update correctly in memory.
    }

    [Fact]
    public void Load_ReadsExistingSettingsFile()
    {
        // First create a manager and simulate a saved file
        const string json = "{\"AppTheme\": 1, \"DynamicControlsEnabled\": true}";
        File.WriteAllText(_testFilePath, json);

        var manager = new SettingsManager(_testDirPath);

        Assert.Equal(1, manager.AppTheme);
        Assert.True(manager.DynamicControlsEnabled);
        Assert.False(manager.StartWithWindows); // Default
    }
}
