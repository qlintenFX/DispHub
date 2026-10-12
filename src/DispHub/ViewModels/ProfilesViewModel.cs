using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Color;
using DispHub.Core.Hotkeys;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Core.Sharing;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Platform.Gamma;
using DispHub.Services;

namespace DispHub.ViewModels;

public sealed partial class ProfilesViewModel : PageViewModel
{
    private readonly AppController _app;
    private readonly IUpgradeService _upgrade;
    private bool _externalReset;
    private bool _mirroringSelection;

    public ProfilesViewModel(AppController app, IUpgradeService upgrade)
        : base(Loc.Get("Nav_Profiles"), Wpf.Ui.Controls.SymbolRegular.Color24)
    {
        _app = app;
        _upgrade = upgrade;
        Editor = new ProfileEditorViewModel(app, upgrade);
        _app.ConfigChanged += (_, _) => Rebuild();
        _app.StateChanged += (_, _) => Refresh();
        _app.ProfileEdited += (_, id) =>
        {
            if (Profiles.FirstOrDefault(p => p.Id == id) is { } item && _app.FindProfile(id) is { } profile)
                item.Update(profile, item.IsActive, _app.HotkeyFailure(id));
        };
        _app.MonitorsChanged += (_, _) => RefreshWarnings();
        _app.ExternalResetDetected += (_, _) =>
        {
            _externalReset = true;
            RefreshWarnings();
        };
        Rebuild();
    }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    public ObservableCollection<WarningViewModel> Warnings { get; } = [];

    public ProfileEditorViewModel Editor { get; }

    [ObservableProperty]
    public partial ProfileItemViewModel? SelectedProfile { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial bool CanAddProfile { get; set; }

    public static string ProfileLimitText => Loc.Format("Profiles_FreeLimit", Core.Licensing.FeatureGate.FreeProfileLimit);

    partial void OnSelectedProfileChanged(ProfileItemViewModel? value)
    {
        if (value is null || _mirroringSelection)
            return;
        Editor.Load(_app.FindProfile(value.Id));
        if (_app.Current?.ProfileId != value.Id || _app.Config.IsPaused)
            _app.ActivateProfile(value.Id, ChangeSource.Editor);
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (value == _app.Config.IsPaused)
            _app.TogglePause(ChangeSource.Editor);
    }

    [RelayCommand]
    private void AddProfile()
    {
        if (_app.AddProfile() is { } profile)
            Select(profile.Id);
        else
            _upgrade.ShowUpgrade(Loc.Get("Upgrade_MoreProfiles"));
    }

    /// <summary>Starting points for a new profile.</summary>
    public IReadOnlyList<TemplateChoice> Templates { get; } =
        [.. ProfileTemplates.All.Select(t => new TemplateChoice(t.Key, Loc.Get($"Template_{t.Key}"), Loc.Get($"Template_{t.Key}Hint")))];

    [ObservableProperty]
    public partial bool IsImporting { get; set; }

    [ObservableProperty]
    public partial string ImportText { get; set; } = "";

    [ObservableProperty]
    public partial string? ImportError { get; set; }

    [RelayCommand]
    private void AddFromTemplate(string? key)
    {
        if (ProfileTemplates.All.FirstOrDefault(t => t.Key == key) is not { } template)
            return;
        AddPreset(new SharedPreset(Loc.Get($"Template_{template.Key}"), template.Color));
    }

    [RelayCommand]
    private void StartImport()
    {
        ImportText = "";
        ImportError = null;
        IsImporting = true;
    }

    [RelayCommand]
    private void CancelImport() => IsImporting = false;

    [RelayCommand]
    private void Import()
    {
        if (!SharedPreset.TryParse(ImportText, out SharedPreset? preset))
        {
            ImportError = Loc.Get("Presets_InvalidLink");
            return;
        }
        if (AddPreset(preset))
            IsImporting = false;
    }

    /// <summary>Adds a shared or template profile and shows it. Returns false if the free profile limit was hit.</summary>
    public bool AddPreset(SharedPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (_app.AddProfile(preset.Color, preset.Name) is not { } profile)
        {
            _upgrade.ShowUpgrade(Loc.Get("Upgrade_MoreProfiles"));
            return false;
        }
        _app.ActivateProfile(profile.Id, ChangeSource.Editor);
        Select(profile.Id);
        return true;
    }

    [RelayCommand]
    private void DuplicateProfile()
    {
        if (SelectedProfile is null)
            return;
        if (_app.DuplicateProfile(SelectedProfile.Id) is { } profile)
            Select(profile.Id);
        else
            _upgrade.ShowUpgrade(Loc.Get("Upgrade_MoreProfiles"));
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile is not null)
            _app.DeleteProfile(SelectedProfile.Id);
    }

    [RelayCommand]
    private void MoveLeft()
    {
        if (SelectedProfile is not null)
            _app.MoveProfile(SelectedProfile.Id, -1);
    }

    [RelayCommand]
    private void MoveRight()
    {
        if (SelectedProfile is not null)
            _app.MoveProfile(SelectedProfile.Id, 1);
    }

    private void Select(Guid id) => SelectedProfile = Profiles.FirstOrDefault(p => p.Id == id);

    private void Rebuild()
    {
        Guid? selected = SelectedProfile?.Id ?? _app.Current?.ProfileId;
        Profiles.Clear();
        foreach (Profile profile in _app.Config.Profiles)
            Profiles.Add(new ProfileItemViewModel(profile));
        CanAddProfile = _app.Features.CanAddProfile(_app.Config.Profiles.Count);
        Refresh();
        if (selected is { } id && Profiles.FirstOrDefault(p => p.Id == id) is { } item)
            SetSelectedSilently(item);
        else if (Profiles.FirstOrDefault(p => p.IsActive) is { } active)
            SetSelectedSilently(active);
    }

    private void Refresh()
    {
        RuleOutput? current = _app.Current;
        IsActive = !_app.Config.IsPaused;
        foreach (ProfileItemViewModel item in Profiles)
        {
            if (_app.FindProfile(item.Id) is { } profile)
                item.Update(profile, isActive: !_app.Config.IsPaused && current?.ProfileId == profile.Id, _app.HotkeyFailure(profile.Id));
        }

        // The editor follows what's on screen, e.g. after a hotkey switch.
        if (current?.ProfileId is { } activeId && SelectedProfile?.Id != activeId &&
            Profiles.FirstOrDefault(p => p.Id == activeId) is { } activeItem)
        {
            SetSelectedSilently(activeItem);
        }
        else if (SelectedProfile is not null)
        {
            Editor.Load(_app.FindProfile(SelectedProfile.Id));
        }

        StatusText = DescribeStatus(current);
        RefreshWarnings();
    }

    /// <summary>Selects without activating: this selection mirrors what's on screen, it isn't a user pick.</summary>
    private void SetSelectedSilently(ProfileItemViewModel item)
    {
        _mirroringSelection = true;
        try
        {
            SelectedProfile = item;
        }
        finally
        {
            _mirroringSelection = false;
        }
        Editor.Load(_app.FindProfile(item.Id));
    }

    private string DescribeStatus(RuleOutput? current)
    {
        if (_app.Config.IsPaused)
            return Loc.Get("Profiles_StatusPaused");
        if (current?.ProfileId is not { } id || _app.FindProfile(id) is not { } profile)
            return "";
        int monitors = _app.Monitors.Count(m => m.State is not MonitorColorState.Original || m.SupportsVibrance);
        string format = current.Reason switch
        {
            TargetReason.AppRule => "Profiles_StatusAppRule",
            TargetReason.Schedule => "Profiles_StatusSchedule",
            TargetReason.Manual => "Profiles_StatusManual",
            _ => "Profiles_Status",
        };
        int count = Math.Max(monitors, _app.Monitors.Count);
        string where = count == 1 ? Loc.Get("Profiles_OneDisplay") : Loc.Format("Profiles_Displays", count);
        return Loc.Format(format, profile.Name, where);
    }

    private void RefreshWarnings()
    {
        Warnings.Clear();
        IReadOnlyList<MonitorStatus> monitors = _app.Monitors;

        if (_app.LoadResult.Status is Core.Config.ConfigLoadStatus.RecoveredFromBackup or Core.Config.ConfigLoadStatus.ResetToDefaults)
        {
            Warnings.Add(new WarningViewModel(
                Loc.Get("Warning_RepairedTitle"),
                Loc.Get("Warning_RepairedText"),
                Wpf.Ui.Controls.InfoBarSeverity.Warning));
        }

        if (monitors.Any(m => m.State == MonitorColorState.Limited))
        {
            Warnings.Add(new WarningViewModel(
                Loc.Get("Warning_LimitedTitle"),
                Loc.Get("Warning_LimitedText"),
                Wpf.Ui.Controls.InfoBarSeverity.Informational,
                Loc.Get("Settings_AllowFullRange"),
                new RelayCommand(() =>
                {
                    if (GammaRange.TryEnableExtended())
                        _app.OnDisplayChanged();
                })));
        }

        if (monitors.Any(m => m.State == MonitorColorState.Hdr))
        {
            Warnings.Add(new WarningViewModel(
                Loc.Get("Warning_HdrTitle"),
                Loc.Get("Warning_HdrText"),
                Wpf.Ui.Controls.InfoBarSeverity.Informational));
        }

        if (_externalReset)
        {
            Warnings.Add(new WarningViewModel(
                Loc.Get("Warning_ResetTitle"),
                Loc.Get("Warning_ResetText"),
                Wpf.Ui.Controls.InfoBarSeverity.Warning));
        }
    }
}

public sealed partial class ProfileItemViewModel(Profile profile) : ObservableObject
{
    public Guid Id { get; } = profile.Id;

    [ObservableProperty]
    public partial string Name { get; set; } = profile.Name;

    [ObservableProperty]
    public partial string? Hotkey { get; set; } = profile.Hotkey;

    [ObservableProperty]
    public partial bool HasHotkeyProblem { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = Describe(profile.Color);

    [ObservableProperty]
    public partial ImageSource Preview { get; set; } = Controls.ColorPreview.Render(profile.Color);

    public void Update(Profile source, bool isActive, string? hotkeyFailure)
    {
        Name = source.Name;
        Hotkey = source.Hotkey;
        HasHotkeyProblem = hotkeyFailure is not null;
        IsActive = isActive;
        Summary = Describe(source.Color);
        Preview = Controls.ColorPreview.Render(source.Color);
    }

    public static string Describe(ColorSettings c)
    {
        var parts = new List<string>();
        if (c.TemperatureK != ColorSettings.NeutralTemperatureK)
            parts.Add($"{c.TemperatureK} K");
        if (c.Brightness != 0)
            parts.Add(Loc.Format("Summary_Brightness", c.Brightness.ToString("+0;-0", CultureInfo.CurrentCulture)));
        if (c.Contrast != 0)
            parts.Add(Loc.Format("Summary_Contrast", c.Contrast.ToString("+0;-0", CultureInfo.CurrentCulture)));
        if (Math.Abs(c.Gamma - 1) > 0.001)
            parts.Add(Loc.Format("Summary_Gamma", c.Gamma.ToString("0.00", CultureInfo.CurrentCulture)));
        if (c.Vibrance != ColorSettings.NeutralVibrance)
            parts.Add(Loc.Format("Summary_Vibrance", c.Vibrance));
        return parts.Count == 0 ? Loc.Get("Summary_Original") : string.Join(" · ", parts);
    }
}

public sealed record DisplayChoice(string? Key, string Name)
{
    public override string ToString() => Name;
}

public sealed partial class ProfileEditorViewModel(AppController app, IUpgradeService upgrade) : ObservableObject
{
    private static readonly DisplayChoice AllDisplays = new(null, Loc.Get("Profiles_AllDisplays"));
    private Guid _profileId;
    private bool _loading;

    public System.Collections.ObjectModel.ObservableCollection<DisplayChoice> Displays { get; } = [AllDisplays];

    [ObservableProperty]
    public partial DisplayChoice SelectedDisplay { get; set; } = AllDisplays;

    [ObservableProperty]
    public partial bool ShowDisplayPicker { get; set; }

    [ObservableProperty]
    public partial bool HasOverride { get; set; }

    partial void OnSelectedDisplayChanged(DisplayChoice value)
    {
        if (_loading)
            return;
        if (value.Key is not null && !app.Features.PerMonitorProfiles)
        {
            upgrade.ShowUpgrade(Loc.Get("Upgrade_PerDisplay"));
            SelectedDisplay = AllDisplays;
            return;
        }
        Load(app.FindProfile(_profileId));
    }

    [RelayCommand]
    private void ClearOverride()
    {
        if (SelectedDisplay.Key is { } key)
        {
            app.ClearMonitorOverride(_profileId, key);
            Load(app.FindProfile(_profileId));
        }
    }

    [ObservableProperty]
    public partial bool HasProfile { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial int Brightness { get; set; }

    [ObservableProperty]
    public partial int Contrast { get; set; }

    [ObservableProperty]
    public partial double Gamma { get; set; } = 1;

    [ObservableProperty]
    public partial int Temperature { get; set; } = ColorSettings.NeutralTemperatureK;

    [ObservableProperty]
    public partial int Vibrance { get; set; } = ColorSettings.NeutralVibrance;

    [ObservableProperty]
    public partial string? Hotkey { get; set; }

    [ObservableProperty]
    public partial string? HotkeyError { get; set; }

    /// <summary>Looks this profile can show: none, as on the Effects page, or any built-in or saved look.</summary>
    public System.Collections.ObjectModel.ObservableCollection<EffectChoice> Effects { get; } = [];

    [ObservableProperty]
    public partial EffectChoice? Effect { get; set; }

    /// <summary>Screen effects are turned on in Settings.</summary>
    [ObservableProperty]
    public partial bool ShowEffects { get; set; }

    partial void OnEffectChanged(EffectChoice? value)
    {
        if (!_loading && HasProfile && value is not null)
            app.SetProfileEffect(_profileId, value.Id);
    }

    private void RefreshEffects(Profile profile)
    {
        Core.Effects.EffectsConfig effects = app.Config.Effects;
        Effects.Clear();
        Effects.Add(new EffectChoice(null, Loc.Get("Profiles_EffectKeep")));
        Effects.Add(new EffectChoice(Core.Effects.LookLibrary.None, Loc.Get("Profiles_EffectNone")));
        foreach (Core.Effects.LookPreset preset in Core.Effects.Looks.All)
            Effects.Add(new EffectChoice(preset.Id, Loc.Get($"Look_{preset.Id}")));
        foreach (Core.Effects.SavedLook saved in effects.SavedLooks)
            Effects.Add(new EffectChoice(saved.Key, saved.Name));
        Effect = Effects.FirstOrDefault(e => e.Id == profile.Effect) ?? Effects[0];
    }

    public bool VibranceSupported => app.Monitors.Any(m => m.SupportsVibrance);

    public string VibranceHelp => VibranceSupported
        ? Loc.Get("Profiles_VibranceHelp")
        : Loc.Get("Profiles_VibranceUnsupported");

    public void Load(Profile? profile)
    {
        _loading = true;
        try
        {
            HasProfile = profile is not null;
            if (profile is null)
                return;
            if (_profileId != profile.Id)
                ShareMessage = null;
            _profileId = profile.Id;
            Name = profile.Name;
            RefreshDisplays();
            string? key = SelectedDisplay.Key;
            HasOverride = key is not null && profile.MonitorOverrides.ContainsKey(key);
            ColorSettings color = key is not null && profile.MonitorOverrides.TryGetValue(key, out ColorSettings? perDisplay)
                ? perDisplay
                : profile.Color;
            Brightness = color.Brightness;
            Contrast = color.Contrast;
            Gamma = color.Gamma;
            Temperature = color.TemperatureK;
            Vibrance = color.Vibrance;
            Hotkey = profile.Hotkey;
            HotkeyError = Localize(app.HotkeyFailure(profile.Id));
            ShowEffects = app.Config.Ui.ShowEffects;
            RefreshEffects(profile);
            OnPropertyChanged(nameof(VibranceSupported));
            OnPropertyChanged(nameof(VibranceHelp));
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnNameChanged(string value)
    {
        if (!_loading && HasProfile)
            app.RenameProfile(_profileId, value);
    }

    [ObservableProperty]
    public partial string? ShareMessage { get; set; }

    [RelayCommand]
    private void Share()
    {
        if (!HasProfile || app.FindProfile(_profileId) is not { } profile)
            return;
        System.Windows.Clipboard.SetText(new Core.Sharing.SharedPreset(profile.Name, profile.Color).ToLink());
        ShareMessage = Loc.Get("Profiles_LinkCopied");
    }

    [RelayCommand]
    private void Reset()
    {
        if (!HasProfile)
            return;
        app.UpdateProfileColor(_profileId, ColorSettings.Neutral);
        Load(app.FindProfile(_profileId));
    }

    partial void OnBrightnessChanged(int value) => Push();

    partial void OnContrastChanged(int value) => Push();

    partial void OnGammaChanged(double value) => Push();

    partial void OnTemperatureChanged(int value) => Push();

    partial void OnVibranceChanged(int value) => Push();

    partial void OnHotkeyChanged(string? value)
    {
        if (_loading || !HasProfile)
            return;
        HotkeyGesture? gesture = HotkeyGesture.TryParse(value, out HotkeyGesture parsed) ? parsed : null;
        HotkeyError = Localize(app.SetProfileHotkey(_profileId, gesture));
    }

    private static string? Localize(string? reason) => reason is null ? null : Loc.Get(reason);

    private void Push()
    {
        if (_loading || !HasProfile)
            return;
        var color = new ColorSettings(Brightness, Contrast, Math.Round(Gamma, 2), Temperature, Vibrance);
        if (SelectedDisplay.Key is { } key)
        {
            app.UpdateProfileMonitorColor(_profileId, key, color);
            HasOverride = true;
        }
        else
        {
            app.UpdateProfileColor(_profileId, color);
        }
    }

    private void RefreshDisplays()
    {
        var monitors = app.Monitors.Select(m => new DisplayChoice(m.Monitor.Key, m.Monitor.Name)).ToList();
        ShowDisplayPicker = monitors.Count > 1;
        if (Displays.Skip(1).Select(d => d.Key).SequenceEqual(monitors.Select(m => m.Key)))
            return;
        string? selected = SelectedDisplay.Key;
        while (Displays.Count > 1)
            Displays.RemoveAt(1);
        foreach (DisplayChoice choice in monitors)
            Displays.Add(choice);
        SelectedDisplay = Displays.FirstOrDefault(d => d.Key == selected) ?? AllDisplays;
    }
}

public sealed record WarningViewModel(
    string Title,
    string Message,
    Wpf.Ui.Controls.InfoBarSeverity Severity,
    string? ActionText = null,
    System.Windows.Input.ICommand? Action = null)
{
    public bool HasAction => ActionText is not null;
}

public sealed record TemplateChoice(string Key, string Name, string Description);

/// <summary>A screen effect a profile can pick; <see cref="Id"/> is null for "as on the Effects page".</summary>
public sealed record EffectChoice(string? Id, string Name)
{
    public override string ToString() => Name;
}
