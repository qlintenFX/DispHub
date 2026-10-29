// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

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
        _app.PowerChanged += (_, _) => Refresh();
        _app.ProfileEdited += (_, id) =>
        {
            if (Profiles.FirstOrDefault(p => p.Id == id) is { } item && _app.FindProfile(id) is { } profile)
                item.Update(profile, item.IsActive, _app.HotkeyFailure(id), item.IsLocked);
        };
        _app.MonitorsChanged += (_, _) => RefreshWarnings();
        _app.MonitorLevelsChanged += (_, _) => Editor.RefreshHardware();
        _app.ModeAbilitiesChanged += (_, _) => Editor.RefreshHardware();
        _app.ExternalResetDetected += (_, _) =>
        {
            _externalReset = true;
            RefreshWarnings();
        };
        Rebuild();
    }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    /// <summary>What the profile grid shows: the profiles, then the tile that adds one.</summary>
    public ObservableCollection<object> Tiles { get; } = [];

    public AddTileViewModel AddTile { get; } = new();

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

    /// <summary>The free profiles are used: offer Pro.</summary>
    public void ShowProfileUpgrade() => _upgrade.ShowUpgrade(Loc.Get("Upgrade_MoreProfiles"));

    partial void OnSelectedProfileChanged(ProfileItemViewModel? value)
    {
        if (value is null || _mirroringSelection)
            return;
        Editor.Load(_app.FindProfile(value.Id));
        if (_app.Current?.ProfileId != value.Id || _app.Config.IsPaused)
            _app.ActivateProfile(value.Id, ChangeSource.Editor);
    }

    /// <summary>A profile was clicked: it goes on screen if it isn't already (DispHub or profiles off, or another on).</summary>
    public void Pick(ProfileItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsLocked)
        {
            ShowProfileUpgrade();
            return;
        }
        if (!_app.ProfilesOn || _app.Current?.ProfileId != item.Id)
            _app.ActivateProfile(item.Id, ChangeSource.Editor);
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (value != _app.ProfilesOn)
            _app.SetProfilesOn(value, ChangeSource.Editor);
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
        Tiles.Clear();
        foreach (Profile profile in _app.Config.Profiles)
        {
            var tile = new ProfileItemViewModel(profile);
            Profiles.Add(tile);
            Tiles.Add(tile);
        }
        CanAddProfile = _app.Features.CanAddProfile(_app.Config.Profiles.Count);
        AddTile.Update(locked: !CanAddProfile && !_app.Features.HasPro, full: !CanAddProfile && _app.Features.HasPro);
        Tiles.Add(AddTile);
        Refresh();
        if (selected is { } id && Profiles.FirstOrDefault(p => p.Id == id) is { } item)
            SetSelectedSilently(item);
        else if (Profiles.FirstOrDefault(p => p.IsActive) is { } active)
            SetSelectedSilently(active);
    }

    private void Refresh()
    {
        RuleOutput? current = _app.Current;
        // Off while DispHub is off, so the switch shows what's on screen; turning it on turns DispHub on.
        IsActive = _app.ProfilesOn;
        foreach (ProfileItemViewModel item in Profiles)
        {
            if (_app.FindProfile(item.Id) is { } profile)
            {
                item.Update(profile, isActive: !_app.Config.IsPaused && current?.ProfileId == profile.Id, _app.HotkeyFailure(profile.Id),
                    isLocked: !_app.CanUseProfile(profile.Id));
            }
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
        if (_app.Config.IsOff)
            return Loc.Get("Profiles_StatusOff");
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

/// <summary>The grid's last tile: adds a profile, or offers Pro once the free profiles are used.</summary>
public sealed partial class AddTileViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    /// <summary>Pro's ceiling is reached: nothing more to add.</summary>
    [ObservableProperty]
    public partial bool IsFull { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = Loc.Get("Profiles_New");

    [ObservableProperty]
    public partial string Hint { get; set; } = Loc.Get("Profiles_NewHint");

    public void Update(bool locked, bool full = false)
    {
        IsLocked = locked;
        IsFull = full;
        (Title, Hint) = (locked, full) switch
        {
            (true, _) => (Loc.Get("Profiles_MoreTitle"), Loc.Format("Profiles_MoreHint", Core.Licensing.FeatureGate.FreeProfileLimit)),
            (_, true) => (Loc.Format("Profiles_FullTitle", Core.Licensing.FeatureGate.ProfileCeiling), Loc.Get("Profiles_FullHint")),
            _ => (Loc.Get("Profiles_New"), Loc.Get("Profiles_NewHint")),
        };
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

    /// <summary>Past the free profiles without Pro: kept, but it can't go on screen.</summary>
    [ObservableProperty]
    public partial bool IsLocked { get; set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = Describe(profile.Color);

    [ObservableProperty]
    public partial ImageSource Preview { get; set; } = Controls.ColorPreview.Render(profile.Color);

    public void Update(Profile source, bool isActive, string? hotkeyFailure, bool isLocked)
    {
        Name = source.Name;
        Hotkey = source.Hotkey;
        HasHotkeyProblem = hotkeyFailure is not null;
        IsActive = isActive;
        IsLocked = isLocked;
        Summary = isLocked ? Loc.Get("Profiles_NeedsPro") : Describe(source.Color);
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
        if (c.ShadowBoost != 0)
            parts.Add(Loc.Format("Summary_Shadows", c.ShadowBoost));
        if (c.Vibrance != ColorSettings.NeutralVibrance)
            parts.Add(Loc.Format("Summary_Vibrance", c.Vibrance));
        return parts.Count == 0 ? Loc.Get("Summary_Original") : string.Join(" · ", parts);
    }
}

public sealed record DisplayChoice(string? Key, string Name)
{
    public override string ToString() => Name;
}

public sealed record RefreshOption(RefreshChoice Choice, string Name)
{
    public override string ToString() => Name;
}

public sealed record HdrOption(bool? On, string Name)
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
    public partial int ShadowBoost { get; set; }

    /// <summary>Shadow boost is Pro: without it the row shows a lock that offers the upgrade.</summary>
    [ObservableProperty]
    public partial bool CanUseShadowBoost { get; set; }

    [RelayCommand]
    private void UnlockShadowBoost() => upgrade.ShowUpgrade(Loc.Get("Upgrade_ShadowBoost"));

    /// <summary>An external monitor answers over DDC/CI, so profiles can set its own brightness.</summary>
    [ObservableProperty]
    public partial bool HasMonitorLevel { get; set; }

    [ObservableProperty]
    public partial bool CanUseMonitorBrightness { get; set; }

    /// <summary>This profile sets the monitors' own brightness.</summary>
    [ObservableProperty]
    public partial bool SetsMonitorBrightness { get; set; }

    [ObservableProperty]
    public partial int MonitorBrightness { get; set; } = 70;

    [RelayCommand]
    private void UnlockMonitorBrightness() => upgrade.ShowUpgrade(Loc.Get("Upgrade_MonitorBrightness"));

    partial void OnSetsMonitorBrightnessChanged(bool value) => PushMonitorBrightness();

    partial void OnMonitorBrightnessChanged(int value) => PushMonitorBrightness();

    private void PushMonitorBrightness()
    {
        if (!_loading && HasProfile)
            app.SetProfileMonitorBrightness(_profileId, SetsMonitorBrightness ? MonitorBrightness : null);
    }

    /// <summary>Some display has more than one refresh rate.</summary>
    [ObservableProperty]
    public partial bool HasRefreshChoice { get; set; }

    /// <summary>Some display can do HDR.</summary>
    [ObservableProperty]
    public partial bool HasHdrChoice { get; set; }

    /// <summary>The section about the monitor itself (its brightness, refresh rate and HDR) has something to show.</summary>
    [ObservableProperty]
    public partial bool HasMonitorSection { get; set; }

    [ObservableProperty]
    public partial bool CanUseDisplayModes { get; set; }

    public ObservableCollection<RefreshOption> RefreshOptions { get; } = [];

    public IReadOnlyList<HdrOption> HdrOptions { get; } =
    [
        new(null, Loc.Get("Profiles_Unchanged")),
        new(true, Loc.Get("Profiles_HdrOn")),
        new(false, Loc.Get("Profiles_HdrOff")),
    ];

    [ObservableProperty]
    public partial RefreshOption? SelectedRefresh { get; set; }

    [ObservableProperty]
    public partial HdrOption? SelectedHdr { get; set; }

    [RelayCommand]
    private void UnlockDisplayModes() => upgrade.ShowUpgrade(Loc.Get("Upgrade_DisplayModes"));

    partial void OnSelectedRefreshChanged(RefreshOption? value) => PushDisplayMode();

    partial void OnSelectedHdrChanged(HdrOption? value) => PushDisplayMode();

    private void PushDisplayMode()
    {
        if (!_loading && HasProfile)
            app.SetProfileDisplayMode(_profileId, SelectedRefresh?.Choice ?? RefreshChoice.Unchanged, SelectedHdr?.On);
    }

    /// <summary>Updates the section about the monitor itself once displays have been looked at, or changed.</summary>
    public void RefreshHardware()
    {
        if (!HasProfile || app.FindProfile(_profileId) is not { } profile)
            return;
        _loading = true;
        try
        {
            LoadHardware(profile);
        }
        finally
        {
            _loading = false;
        }
    }

    private void LoadHardware(Profile profile)
    {
        HasMonitorLevel = app.MonitorLevels.Count > 0;
        CanUseMonitorBrightness = app.Features.MonitorBrightnessInProfiles;
        SetsMonitorBrightness = profile.MonitorBrightness is not null;
        // Turned on, the slider starts from where the monitor is now.
        MonitorBrightness = profile.MonitorBrightness ?? app.MonitorLevels.Values.DefaultIfEmpty(70).First();

        var rates = app.ModeAbilities.Values.Select(a => a.RefreshRates).Where(r => r.Count > 1).ToList();
        HasRefreshChoice = rates.Count > 0;
        HasHdrChoice = app.ModeAbilities.Values.Any(a => a.Hdr);
        HasMonitorSection = HasMonitorLevel || HasRefreshChoice || HasHdrChoice;
        CanUseDisplayModes = app.Features.DisplayModes;
        RefreshOption[] options =
        [
            new(RefreshChoice.Unchanged, Loc.Get("Profiles_Unchanged")),
            Option(RefreshChoice.Highest, "Profiles_RefreshHighest", rates),
            Option(RefreshChoice.Lowest, "Profiles_RefreshLowest", rates),
        ];
        if (!RefreshOptions.SequenceEqual(options))
        {
            RefreshOptions.Clear();
            foreach (RefreshOption option in options)
                RefreshOptions.Add(option);
        }
        SelectedRefresh = RefreshOptions.First(o => o.Choice == profile.RefreshRate);
        SelectedHdr = HdrOptions.First(o => o.On == profile.Hdr);
    }

    /// <summary>"Highest (165 Hz)" when every display agrees on the rate, otherwise just "Highest".</summary>
    private static RefreshOption Option(RefreshChoice choice, string key, List<IReadOnlyList<int>> rates)
    {
        var picks = rates.Select(r => RefreshRates.Pick(r, choice)).Distinct().ToList();
        string name = Loc.Get(key);
        return new RefreshOption(choice, picks is [int hz] ? Loc.Format("Profiles_RefreshRateHz", name, hz) : name);
    }

    [ObservableProperty]
    public partial string? Hotkey { get; set; }

    [ObservableProperty]
    public partial string? HotkeyError { get; set; }

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
                LinkCopied = false;
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
            ShadowBoost = color.ShadowBoost;
            CanUseShadowBoost = app.Features.ShadowBoost;
            LoadHardware(profile);
            Hotkey = profile.Hotkey;
            HotkeyError = Localize(app.HotkeyFailure(profile.Id));
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

    /// <summary>The share link was just copied: the button says so for a moment.</summary>
    [ObservableProperty]
    public partial bool LinkCopied { get; set; }

    [RelayCommand]
    private void Share()
    {
        if (!HasProfile || app.FindProfile(_profileId) is not { } profile)
            return;
        System.Windows.Clipboard.SetText(new Core.Sharing.SharedPreset(profile.Name, profile.Color).ToLink());
        _ = FlashLinkCopiedAsync();
    }

    private async Task FlashLinkCopiedAsync()
    {
        LinkCopied = true;
        await Task.Delay(TimeSpan.FromSeconds(2));
        LinkCopied = false;
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

    partial void OnShadowBoostChanged(int value) => Push();

    partial void OnHotkeyChanged(string? value)
    {
        if (_loading || !HasProfile)
            return;
        HotkeyGesture? gesture = HotkeyGesture.TryParse(value, out HotkeyGesture parsed) ? parsed : null;
        HotkeyError = Localize(app.SetProfileHotkey(_profileId, gesture));
    }

    private static string? Localize(string? reason) => reason is null ? null : AppController.DescribeHotkeyFailure(reason);

    private void Push()
    {
        if (_loading || !HasProfile)
            return;
        var color = new ColorSettings(Brightness, Contrast, Math.Round(Gamma, 2), Temperature, Vibrance, ShadowBoost);
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
