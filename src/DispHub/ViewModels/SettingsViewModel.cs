using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Hotkeys;
using DispHub.Core.Model;
using DispHub.Localization;
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
        : base(Loc.Get("Nav_Settings"), Wpf.Ui.Controls.SymbolRegular.Settings24)
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

    public IReadOnlyList<LanguageChoice> LanguageChoices { get; } =
        [new LanguageChoice(null, Loc.Get("Settings_LanguageWindows")), .. Loc.Languages.Select(l => new LanguageChoice(l, Loc.NativeName(l)))];

    [ObservableProperty]
    public partial LanguageChoice? Language { get; set; }

    [ObservableProperty]
    public partial bool LanguageNeedsRestart { get; set; }

    partial void OnLanguageChanged(LanguageChoice? value)
    {
        if (_loading || value is null || value.Code == _app.Config.Ui.Language)
            return;
        _app.Config.Ui.Language = value.Code;
        _app.SettingsChanged();
        LanguageNeedsRestart = true;
    }

    public IReadOnlyList<PreviewPictureChoice> PreviewPictures { get; } =
        [.. Controls.ColorPreview.Pictures.Select(p => new PreviewPictureChoice(p, Loc.Get($"Preview_{p}"), Controls.ColorPreview.Original(p)))];

    [ObservableProperty]
    public partial PreviewPictureChoice? PreviewPicture { get; set; }

    partial void OnPreviewPictureChanged(PreviewPictureChoice? value)
    {
        if (_loading || value is null || value.Id == Controls.ColorPreview.Picture)
            return;
        // Switch first: saving raises ConfigChanged, which redraws every preview.
        Controls.ColorPreview.Use(value.Id);
        Save(c => c.Ui.PreviewPicture = value.Id);
    }

    [RelayCommand]
    private static void Restart() => ((App)System.Windows.Application.Current).RequestRestart();

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
            LaunchAtLoginNote = Loc.Get("Settings_StartupDenied");
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
            Language = LanguageChoices.FirstOrDefault(l => l.Code == ui.Language) ?? LanguageChoices[0];
            PreviewPicture = PreviewPictures.FirstOrDefault(p => p.Id == Controls.ColorPreview.Picture);
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
            LaunchAtLoginNote = Loc.Get("Settings_StartupUnreadable");
        }
    }

    private void ApplyAutostart(AutostartState state)
    {
        _loading = true;
        LaunchAtLogin = state == AutostartState.Enabled;
        _loading = false;
        LaunchAtLoginNote = state switch
        {
            AutostartState.DisabledByUser => Loc.Get("Settings_StartupDisabledByUser"),
            AutostartState.DisabledByPolicy => Loc.Get("Settings_StartupDisabledByPolicy"),
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
        foreach ((string action, string label) in new[] { (AppController.PauseAction, Loc.Get("Settings_Pause")), (AppController.NextAction, Loc.Get("Settings_Next")), (AppController.PreviousAction, Loc.Get("Settings_Previous")) })
        {
            if (_app.HotkeyFailures.TryGetValue(action, out string? reason))
                failures.Add(Loc.Format("Settings_HotkeyFailure", label, Loc.Get(reason)));
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
        Name = status.Monitor.IsPrimary ? Loc.Format("Monitor_Main", status.Monitor.Name) : status.Monitor.Name;
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
            MonitorColorState.Applied => Loc.Get("Monitor_Applied"),
            MonitorColorState.Limited => Loc.Get("Monitor_Limited"),
            MonitorColorState.Unsupported => Loc.Get("Monitor_Unsupported"),
            MonitorColorState.Hdr => Loc.Get("Monitor_Hdr"),
            _ => Loc.Get("Monitor_Original"),
        };
        return $"{status.Monitor.DeviceName.TrimStart('\\', '.')} · {colours} · {Loc.Get(status.SupportsVibrance ? "Monitor_Vibrance" : "Monitor_NoVibrance")}";
    }
}

public sealed record LanguageChoice(string? Code, string Name)
{
    public override string ToString() => Name;
}

public sealed record PreviewPictureChoice(string Id, string Name, System.Windows.Media.ImageSource Image)
{
    public override string ToString() => Name;
}
