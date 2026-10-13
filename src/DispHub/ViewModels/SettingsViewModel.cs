using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Hotkeys;
using DispHub.Core.Model;
using DispHub.Localization;
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
        AccentChoices =
        [
            new(null, Loc.Get("Settings_AccentWindows"), Brush(SystemAccentHex())),
            .. Accents.All.Select(a => new AccentChoice(a.Hex, Loc.Get($"Accent_{a.Key}"), Brush(a.Hex))),
        ];
        Load();
        _ = LoadAutostartAsync();
    }

    public IReadOnlyList<ThemePreference> Themes { get; } = Enum.GetValues<ThemePreference>();
    public IReadOnlyList<WindowBackdrop> Backdrops { get; } = [WindowBackdrop.Mica, WindowBackdrop.MicaAlt, WindowBackdrop.Acrylic, WindowBackdrop.Solid];
    public IReadOnlyList<TrayClickAction> TrayActions { get; } = Enum.GetValues<TrayClickAction>();
    public IReadOnlyList<OsdPosition> OsdPositions { get; } = Enum.GetValues<OsdPosition>();

    /// <summary>Same as Windows first, then the colours DispHub offers.</summary>
    public IReadOnlyList<AccentChoice> AccentChoices { get; }

    public IReadOnlyList<ScaleChoice> TextScales { get; } =
        [new(0.9, "90 %"), new(1.0, Loc.Get("Settings_TextNormal")), new(1.1, "110 %"), new(1.25, "125 %")];

    public IReadOnlyList<LanguageChoice> LanguageChoices { get; } =
        [new LanguageChoice(null, Loc.Get("Settings_LanguageWindows")), .. Loc.Languages.Select(l => new LanguageChoice(l, Loc.NativeName(l)))];

    public IReadOnlyList<PreviewPictureChoice> PreviewPictures { get; } =
        [.. Controls.ColorPreview.Pictures.Select(p => new PreviewPictureChoice(p, Loc.Get($"Preview_{p}"), Controls.ColorPreview.Original(p)))];

    /// <summary>Filled in by the main window: the pages the window can open on.</summary>
    public IReadOnlyList<StartPageChoice> StartPages { get; set; } = [];

    [ObservableProperty] public partial ThemePreference Theme { get; set; }
    [ObservableProperty] public partial AccentChoice? Accent { get; set; }
    [ObservableProperty] public partial bool ShowEffects { get; set; }
    [ObservableProperty] public partial string? QuickPanelHotkey { get; set; }
    [ObservableProperty] public partial WindowBackdrop Backdrop { get; set; }
    [ObservableProperty] public partial bool CompactNavigation { get; set; }
    [ObservableProperty] public partial ScaleChoice? TextScale { get; set; }
    [ObservableProperty] public partial bool Animations { get; set; }
    [ObservableProperty] public partial PreviewPictureChoice? PreviewPicture { get; set; }
    [ObservableProperty] public partial StartPageChoice? StartPage { get; set; }
    [ObservableProperty] public partial LanguageChoice? Language { get; set; }
    [ObservableProperty] public partial bool LanguageNeedsRestart { get; set; }
    [ObservableProperty] public partial bool CloseToTray { get; set; }
    [ObservableProperty] public partial TrayClickAction TrayLeftClick { get; set; }
    [ObservableProperty] public partial bool ShowOsd { get; set; }
    [ObservableProperty] public partial double OsdSeconds { get; set; }
    [ObservableProperty] public partial OsdPosition OsdPosition { get; set; }
    [ObservableProperty] public partial bool LaunchAtLogin { get; set; }
    [ObservableProperty] public partial string? LaunchAtLoginNote { get; set; }
    [ObservableProperty] public partial string? PauseHotkey { get; set; }
    [ObservableProperty] public partial string? NextHotkey { get; set; }
    [ObservableProperty] public partial string? PreviousHotkey { get; set; }
    [ObservableProperty] public partial string? EffectsHotkey { get; set; }
    [ObservableProperty] public partial string? HotkeyError { get; set; }

    partial void OnThemeChanged(ThemePreference value) => SaveAppearance(c => c.Ui.Theme = value);

    partial void OnAccentChanged(AccentChoice? value)
    {
        if (value is not null)
            SaveAppearance(c => c.Ui.AccentColor = value.Hex);
    }

    partial void OnShowEffectsChanged(bool value)
    {
        if (_loading)
            return;
        Save(c => c.Ui.ShowEffects = value);
        _app.RefreshHotkeys();
    }

    partial void OnQuickPanelHotkeyChanged(string? value) => SetHotkey(AppController.QuickPanelAction, value);

    partial void OnBackdropChanged(WindowBackdrop value) => SaveAppearance(c => c.Ui.Backdrop = value);

    partial void OnCompactNavigationChanged(bool value) => Save(c => c.Ui.CompactNavigation = value);

    partial void OnTextScaleChanged(ScaleChoice? value)
    {
        if (value is not null)
            Save(c => c.Ui.TextScale = value.Scale);
    }

    partial void OnAnimationsChanged(bool value) => Save(c => c.Ui.ReduceMotion = !value);

    partial void OnPreviewPictureChanged(PreviewPictureChoice? value)
    {
        if (_loading || value is null || value.Id == Controls.ColorPreview.Picture)
            return;
        // Switch first: saving raises ConfigChanged, which redraws every preview.
        Controls.ColorPreview.Use(value.Id);
        Save(c => c.Ui.PreviewPicture = value.Id);
    }

    partial void OnStartPageChanged(StartPageChoice? value)
    {
        if (value is not null)
            Save(c => c.Ui.StartPage = value.Key);
    }

    partial void OnLanguageChanged(LanguageChoice? value)
    {
        if (_loading || value is null || value.Code == _app.Config.Ui.Language)
            return;
        _app.Config.Ui.Language = value.Code;
        _app.SettingsChanged();
        LanguageNeedsRestart = true;
    }

    partial void OnCloseToTrayChanged(bool value) => Save(c => c.Ui.CloseToTray = value);

    partial void OnTrayLeftClickChanged(TrayClickAction value) => Save(c => c.Ui.TrayLeftClick = value);

    partial void OnShowOsdChanged(bool value) => Save(c => c.Ui.ShowOsd = value);

    partial void OnOsdSecondsChanged(double value) => Save(c => c.Ui.OsdDurationMs = (int)Math.Round(value * 1000));

    partial void OnOsdPositionChanged(OsdPosition value) => Save(c => c.Ui.OsdPosition = value);

    partial void OnPauseHotkeyChanged(string? value) => SetHotkey(AppController.PauseAction, value);

    partial void OnNextHotkeyChanged(string? value) => SetHotkey(AppController.NextAction, value);

    partial void OnPreviousHotkeyChanged(string? value) => SetHotkey(AppController.PreviousAction, value);

    partial void OnEffectsHotkeyChanged(string? value) => SetHotkey(AppController.EffectsAction, value);

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
    private static void Restart() => ((App)System.Windows.Application.Current).RequestRestart();

    [RelayCommand]
    private void ResetAppearance()
    {
        UiConfig ui = _app.Config.Ui;
        ui.Theme = ThemePreference.System;
        ui.AccentColor = null;
        ui.Backdrop = WindowBackdrop.Mica;
        ui.CompactNavigation = false;
        ui.TextScale = 1.0;
        ui.ReduceMotion = false;
        _app.SettingsChanged();
        _theme.Apply(ui);
    }

    private void Load()
    {
        _loading = true;
        try
        {
            UiConfig ui = _app.Config.Ui;
            Theme = ui.Theme;
            Accent = AccentChoices.FirstOrDefault(a => string.Equals(a.Hex, ui.AccentColor, StringComparison.OrdinalIgnoreCase)) ?? AccentChoices[0];
            ShowEffects = ui.ShowEffects;
            QuickPanelHotkey = _app.Config.Hotkeys.QuickPanel;
            Backdrop = ui.Backdrop;
            CompactNavigation = ui.CompactNavigation;
            TextScale = TextScales.MinBy(s => Math.Abs(s.Scale - ui.TextScale));
            Animations = !ui.ReduceMotion;
            PreviewPicture = PreviewPictures.FirstOrDefault(p => p.Id == Controls.ColorPreview.Picture);
            StartPage = StartPages.FirstOrDefault(p => p.Key == ui.StartPage) ?? (StartPages.Count > 0 ? StartPages[0] : null);
            Language = LanguageChoices.FirstOrDefault(l => l.Code == ui.Language) ?? LanguageChoices[0];
            CloseToTray = ui.CloseToTray;
            TrayLeftClick = ui.TrayLeftClick;
            ShowOsd = ui.ShowOsd;
            OsdSeconds = ui.OsdDurationMs / 1000.0;
            OsdPosition = ui.OsdPosition;
            PauseHotkey = _app.Config.Hotkeys.TogglePause;
            NextHotkey = _app.Config.Hotkeys.NextProfile;
            PreviousHotkey = _app.Config.Hotkeys.PreviousProfile;
            EffectsHotkey = _app.Config.Hotkeys.ToggleEffects;
            HotkeyError = DescribeHotkeyFailures();
        }
        finally
        {
            _loading = false;
        }
    }

    internal void SetStartPages(IEnumerable<PageViewModel> pages)
    {
        StartPages = [.. pages.Select(p => new StartPageChoice(p.Key, p.Title))];
        OnPropertyChanged(nameof(StartPages));
        Load();
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

    private void Save(Action<AppConfig> change)
    {
        if (_loading)
            return;
        change(_app.Config);
        _app.SettingsChanged();
    }

    private void SaveAppearance(Action<AppConfig> change)
    {
        if (_loading)
            return;
        change(_app.Config);
        _theme.Apply(_app.Config.Ui);
        _app.SettingsChanged();
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
        foreach ((string action, string label) in new[] { (AppController.PauseAction, Loc.Get("Settings_Pause")), (AppController.NextAction, Loc.Get("Settings_Next")), (AppController.PreviousAction, Loc.Get("Settings_Previous")), (AppController.EffectsAction, Loc.Get("Settings_EffectsHotkey")), (AppController.QuickPanelAction, Loc.Get("Settings_QuickPanelHotkey")) })
        {
            if (_app.HotkeyFailures.TryGetValue(action, out string? reason))
                failures.Add(Loc.Format("Settings_HotkeyFailure", label, Loc.Get(reason)));
        }
        return failures.Count == 0 ? null : string.Join(Environment.NewLine, failures);
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static string SystemAccentHex()
    {
        Color accent = Wpf.Ui.Appearance.ApplicationAccentColorManager.GetColorizationColor();
        return $"#{accent.R:X2}{accent.G:X2}{accent.B:X2}";
    }
}

public sealed record LanguageChoice(string? Code, string Name)
{
    public override string ToString() => Name;
}

public sealed record PreviewPictureChoice(string Id, string Name, ImageSource Image)
{
    public override string ToString() => Name;
}

public sealed record AccentChoice(string? Hex, string Name, Brush Swatch)
{
    public override string ToString() => Name;
}

public sealed record ScaleChoice(double Scale, string Name)
{
    public override string ToString() => Name;
}

public sealed record StartPageChoice(string Key, string Name)
{
    public override string ToString() => Name;
}
