using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Hotkeys;
using DispHub.Core.Model;
using DispHub.Platform;
using DispHub.Platform.Gamma;
using DispHub.Platform.Shell;
using DispHub.Services;

namespace DispHub.ViewModels;

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly AppController _app;
    private readonly IAutostart _autostart;
    private readonly ThemeService _theme;
    private bool _loading;

    public SettingsViewModel(AppController app, IAutostart autostart, ThemeService theme)
        : base("Settings", Wpf.Ui.Controls.SymbolRegular.Settings24)
    {
        _app = app;
        _autostart = autostart;
        _theme = theme;
        _app.ConfigChanged += (_, _) => Load();
        _app.MonitorsChanged += (_, _) => LoadMonitors();
        Load();
        LoadMonitors();
        _ = LoadAutostartAsync();
    }

    public IReadOnlyList<ThemePreference> Themes { get; } = Enum.GetValues<ThemePreference>();

    public IReadOnlyList<TrayClickAction> TrayActions { get; } = Enum.GetValues<TrayClickAction>();

    public ObservableCollection<MonitorItemViewModel> Monitors { get; } = [];

    [ObservableProperty]
    public partial ThemePreference Theme { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    [ObservableProperty]
    public partial TrayClickAction TrayLeftClick { get; set; }

    [ObservableProperty]
    public partial bool ShowOsd { get; set; }

    [ObservableProperty]
    public partial double OsdSeconds { get; set; }

    [ObservableProperty]
    public partial bool LaunchAtLogin { get; set; }

    [ObservableProperty]
    public partial string? LaunchAtLoginNote { get; set; }

    [ObservableProperty]
    public partial string? PauseHotkey { get; set; }

    [ObservableProperty]
    public partial string? NextHotkey { get; set; }

    [ObservableProperty]
    public partial string? PreviousHotkey { get; set; }

    [ObservableProperty]
    public partial string? HotkeyError { get; set; }

    [ObservableProperty]
    public partial bool ExtendedGammaRange { get; set; }

    partial void OnThemeChanged(ThemePreference value) => Save(c => c.Ui.Theme = value, () => _theme.Apply(value));

    partial void OnCloseToTrayChanged(bool value) => Save(c => c.Ui.CloseToTray = value);

    partial void OnTrayLeftClickChanged(TrayClickAction value) => Save(c => c.Ui.TrayLeftClick = value);

    partial void OnShowOsdChanged(bool value) => Save(c => c.Ui.ShowOsd = value);

    partial void OnOsdSecondsChanged(double value) => Save(c => c.Ui.OsdDurationMs = (int)Math.Round(value * 1000));

    partial void OnPauseHotkeyChanged(string? value) => SetHotkey(AppController.PauseAction, value);

    partial void OnNextHotkeyChanged(string? value) => SetHotkey(AppController.NextAction, value);

    partial void OnPreviousHotkeyChanged(string? value) => SetHotkey(AppController.PreviousAction, value);

    async partial void OnLaunchAtLoginChanged(bool value)
    {
        if (_loading)
            return;
        try
        {
            ApplyAutostart(await _autostart.SetEnabledAsync(value));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            await LoadAutostartAsync();
            LaunchAtLoginNote = "Windows didn't allow changing the startup setting.";
        }
    }

    [RelayCommand]
    private void EnableExtendedGammaRange()
    {
        if (GammaRange.TryEnableExtended())
            _app.OnDisplayChanged();
        ExtendedGammaRange = GammaRange.IsExtended;
    }

    private void Load()
    {
        _loading = true;
        try
        {
            UiConfig ui = _app.Config.Ui;
            Theme = ui.Theme;
            CloseToTray = ui.CloseToTray;
            TrayLeftClick = ui.TrayLeftClick;
            ShowOsd = ui.ShowOsd;
            OsdSeconds = ui.OsdDurationMs / 1000.0;
            PauseHotkey = _app.Config.Hotkeys.TogglePause;
            NextHotkey = _app.Config.Hotkeys.NextProfile;
            PreviousHotkey = _app.Config.Hotkeys.PreviousProfile;
            ExtendedGammaRange = GammaRange.IsExtended;
            HotkeyError = DescribeHotkeyFailures();
        }
        finally
        {
            _loading = false;
        }
    }

    private void LoadMonitors()
    {
        Monitors.Clear();
        foreach (MonitorStatus status in _app.Monitors)
            Monitors.Add(new MonitorItemViewModel(_app, status));
    }

    private async Task LoadAutostartAsync()
    {
        try
        {
            ApplyAutostart(await _autostart.GetStateAsync());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            LaunchAtLoginNote = "Couldn't read the startup setting.";
        }
    }

    private void ApplyAutostart(AutostartState state)
    {
        _loading = true;
        LaunchAtLogin = state == AutostartState.Enabled;
        _loading = false;
        LaunchAtLoginNote = state switch
        {
            AutostartState.DisabledByUser => "Turned off in Task Manager. Turn it back on in Settings > Apps > Startup.",
            AutostartState.DisabledByPolicy => "Your organisation's policy blocks startup apps.",
            _ => null,
        };
    }

    private void Save(Action<AppConfig> change, Action? then = null)
    {
        if (_loading)
            return;
        change(_app.Config);
        _app.SettingsChanged();
        then?.Invoke();
    }

    private void SetHotkey(string action, string? value)
    {
        if (_loading)
            return;
        HotkeyGesture? gesture = HotkeyGesture.TryParse(value, out HotkeyGesture parsed) ? parsed : null;
        _app.SetActionHotkey(action, gesture);
        HotkeyError = DescribeHotkeyFailures();
    }

    private string? DescribeHotkeyFailures()
    {
        var failures = new List<string>();
        foreach ((string action, string label) in new[] { (AppController.PauseAction, "Pause"), (AppController.NextAction, "Next profile"), (AppController.PreviousAction, "Previous profile") })
        {
            if (_app.HotkeyFailures.TryGetValue(action, out string? reason))
                failures.Add($"{label}: {reason}");
        }
        return failures.Count == 0 ? null : string.Join(Environment.NewLine, failures);
    }
}

public sealed partial class MonitorItemViewModel : ObservableObject
{
    private readonly AppController _app;
    private readonly string _key;

    public MonitorItemViewModel(AppController app, MonitorStatus status)
    {
        _app = app;
        _key = status.Monitor.Key;
        Name = status.Monitor.Name + (status.Monitor.IsPrimary ? " (main)" : "");
        Details = Describe(status);
        Excluded = app.Config.Monitors.TryGetValue(_key, out MonitorConfig? config) && config.Excluded;
    }

    public string Name { get; }

    public string Details { get; }

    [ObservableProperty]
    public partial bool Excluded { get; set; }

    partial void OnExcludedChanged(bool value)
    {
        if (!_app.Config.Monitors.TryGetValue(_key, out MonitorConfig? config))
        {
            config = new MonitorConfig { Name = Name };
            _app.Config.Monitors[_key] = config;
        }
        if (config.Excluded == value)
            return;
        config.Excluded = value;
        _app.SettingsChanged();
    }

    private static string Describe(MonitorStatus status)
    {
        string colours = status.State switch
        {
            MonitorColorState.Applied => "Colours applied",
            MonitorColorState.Limited => "Limited by Windows",
            MonitorColorState.Unsupported => "Gamma not supported",
            MonitorColorState.Hdr => "HDR on (gamma unavailable)",
            _ => "Original colours",
        };
        return $"{status.Monitor.DeviceName.TrimStart('\\', '.')} · {colours} · {(status.SupportsVibrance ? "Vibrance supported" : "No vibrance")}";
    }
}
