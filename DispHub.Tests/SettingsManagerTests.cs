using System.Reflection;
using DispHub.Constants;
using DispHub.Services.Settings;

namespace DispHub.Tests;

public class SettingsManagerTests
{
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);

    [Fact]
    public async Task SettingsManager_PersistsSelectedSettingsValues()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var scope = new AppDataScope();

            var manager = new SettingsManager
            {
                CloseToTray = true,
                AppTheme = 2,
                AccentColor = 7,
                DynamicControlsEnabled = true,
                DcToggleKey = 0x44,
                DcToggleMod = AppConstants.MOD_CONTROL,
                MasterToggleKey = 0x4D,
                MasterToggleMod = AppConstants.MOD_ALT,
                TrayLeftClickBehavior = 1,
                LastActiveProfileIndex = 2,
                TaskbarWidgetEnabled = true,
                TaskbarWidgetPosition = 2,
                TaskbarWidgetManualPadding = 11,
                TaskbarWidgetAutoPadding = false,
                TaskbarWidgetClickable = false,
                TaskbarWidgetBackgroundBlur = true,
                TaskbarWidgetHideWhenInactive = true,
                FlyoutEnabled = false,
                FlyoutDuration = 2500
            };

            MethodInfo? saveMethod = typeof(SettingsManager).GetMethod("SaveToFileAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(saveMethod);
            var saveTask = (Task?)saveMethod!.Invoke(manager, null);
            Assert.NotNull(saveTask);
            await saveTask!;

            var reloaded = new SettingsManager();
            Assert.True(reloaded.CloseToTray);
            Assert.Equal(2, reloaded.AppTheme);
            Assert.Equal(7, reloaded.AccentColor);
            Assert.True(reloaded.DynamicControlsEnabled);
            Assert.Equal(0x44u, reloaded.DcToggleKey);
            Assert.Equal(AppConstants.MOD_CONTROL, reloaded.DcToggleMod);
            Assert.Equal(0x4Du, reloaded.MasterToggleKey);
            Assert.Equal(AppConstants.MOD_ALT, reloaded.MasterToggleMod);
            Assert.Equal(1, reloaded.TrayLeftClickBehavior);
            Assert.Equal(2, reloaded.LastActiveProfileIndex);
            Assert.True(reloaded.TaskbarWidgetEnabled);
            Assert.Equal(2, reloaded.TaskbarWidgetPosition);
            Assert.Equal(11, reloaded.TaskbarWidgetManualPadding);
            Assert.False(reloaded.TaskbarWidgetAutoPadding);
            Assert.False(reloaded.TaskbarWidgetClickable);
            Assert.True(reloaded.TaskbarWidgetBackgroundBlur);
            Assert.True(reloaded.TaskbarWidgetHideWhenInactive);
            Assert.False(reloaded.FlyoutEnabled);
            Assert.Equal(2500, reloaded.FlyoutDuration);

            string settingsPath = Path.Combine(AppConstants.AppDataPath, AppConstants.SettingsFileName);
            Assert.True(File.Exists(settingsPath));
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    private sealed class AppDataScope : IDisposable
    {
        private readonly string? _originalAppData;
        private readonly string _tempAppData;

        public AppDataScope()
        {
            _originalAppData = Environment.GetEnvironmentVariable("APPDATA", EnvironmentVariableTarget.Process);
            _tempAppData = Path.Combine(Path.GetTempPath(), "DispHubTests_AppData_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempAppData);
            Environment.SetEnvironmentVariable("APPDATA", _tempAppData, EnvironmentVariableTarget.Process);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("APPDATA", _originalAppData, EnvironmentVariableTarget.Process);
            try
            {
                if (Directory.Exists(_tempAppData))
                {
                    Directory.Delete(_tempAppData, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

