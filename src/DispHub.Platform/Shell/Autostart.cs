using Microsoft.Win32;
using Windows.ApplicationModel;

namespace DispHub.Platform.Shell;

public enum AutostartState
{
    Disabled,
    Enabled,
    /// <summary>The user turned it off in Task Manager or Settings; only they can turn it back on there.</summary>
    DisabledByUser,
    DisabledByPolicy,
}

public interface IAutostart
{
    Task<AutostartState> GetStateAsync();

    Task<AutostartState> SetEnabledAsync(bool enabled);
}

/// <summary>Packaged builds use the manifest's StartupTask; unpackaged builds use the HKCU Run key.</summary>
public sealed class Autostart : IAutostart
{
    public const string StartupArgument = "--startup";
    private const string TaskId = "DispHubStartup";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DispHub";

    public async Task<AutostartState> GetStateAsync()
    {
        if (!PackageInfo.IsPackaged)
            return ReadRunKey() is null ? AutostartState.Disabled : AutostartState.Enabled;

        StartupTask task = await StartupTask.GetAsync(TaskId);
        return Map(task.State);
    }

    public async Task<AutostartState> SetEnabledAsync(bool enabled)
    {
        if (!PackageInfo.IsPackaged)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
                key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {StartupArgument}");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            return await GetStateAsync();
        }

        StartupTask task = await StartupTask.GetAsync(TaskId);
        if (enabled)
            return Map(await task.RequestEnableAsync());
        task.Disable();
        return Map(task.State);
    }

    private static string? ReadRunKey()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    private static AutostartState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy => AutostartState.Enabled,
        StartupTaskState.DisabledByUser => AutostartState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => AutostartState.DisabledByPolicy,
        _ => AutostartState.Disabled,
    };
}
