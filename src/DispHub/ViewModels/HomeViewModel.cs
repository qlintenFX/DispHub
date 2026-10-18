using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Controls;
using DispHub.Core.Color;
using DispHub.Core.Licensing;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Core.Schedule;
using DispHub.Localization;
using DispHub.Platform.Shell;
using DispHub.Platform.Store;
using DispHub.Services;
using Wpf.Ui.Controls;

namespace DispHub.ViewModels;

/// <summary>
/// The start page. It answers what is on the screen right now and why, switches profile in one click, shows where
/// each feature stands with a way into it, and, for free users, what Pro adds.
/// </summary>
public sealed partial class HomeViewModel : PageViewModel
{
    public const string ChangelogUrl = AboutViewModel.RepositoryUrl + "/blob/main/CHANGELOG.md";

    private readonly AppController _app;
    private readonly EffectsService _effects;
    private readonly ILicenseService _license;
    private readonly IUpgradeService _upgrade;
    private readonly TimeProvider _time;
    private bool _loading;

    public HomeViewModel(AppController app, EffectsService effects, ILicenseService license, IUpgradeService upgrade, TimeProvider time)
        : base(Loc.Get("Nav_Home"), SymbolRegular.Home24)
    {
        _app = app;
        _effects = effects;
        _license = license;
        _upgrade = upgrade;
        _time = time;
        _app.StateChanged += (_, _) => Refresh();
        _app.ConfigChanged += (_, _) => Refresh();
        _app.PowerChanged += (_, _) => Refresh();
        _app.ProfileEdited += (_, _) => Refresh();
        _app.MonitorsChanged += (_, _) => Refresh();
        _effects.Changed += (_, _) => Refresh();
        _license.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        Refresh();
    }

    /// <summary>Asks the main window to show the page with this view model type.</summary>
    public event EventHandler<Type>? NavigateRequested;

    /// <summary>Asks the main window for its guided tour.</summary>
    public event EventHandler? TourRequested;

    /// <summary>The active profile's colours on the preview picture (the plain picture when off).</summary>
    [ObservableProperty] public partial ImageSource? NowPicture { get; set; }

    [ObservableProperty] public partial string NowName { get; set; } = "";

    /// <summary>Why: picked by hand, the schedule (until when), the app in front, or DispHub is off.</summary>
    [ObservableProperty] public partial string NowReason { get; set; } = "";

    /// <summary>The look on screen, if any.</summary>
    [ObservableProperty] public partial string? NowEffect { get; set; }

    [ObservableProperty] public partial bool IsOn { get; set; }

    public ObservableCollection<HomeProfile> Profiles { get; } = [];

    /// <summary>The profile on now, if any; picking another one switches to it.</summary>
    [ObservableProperty] public partial HomeProfile? CurrentProfile { get; set; }

    public ObservableCollection<HomeFeature> Features { get; } = [];

    /// <summary>Pro can be bought here and isn't yet.</summary>
    [ObservableProperty] public partial bool ShowPro { get; set; }

    /// <summary>A Microsoft Store build, which can be rated in the Store.</summary>
    public bool CanRate => _license.CanPurchase;

    public static string Version => Loc.Format("Home_Version", AboutViewModel.Version);

    public override void OnShown() => Refresh();

    partial void OnCurrentProfileChanged(HomeProfile? value)
    {
        if (!_loading && value is not null && (value.Id != _app.Current?.ProfileId || _app.Config.IsOff || _app.Config.IsPaused))
            _app.ActivateProfile(value.Id, ChangeSource.Editor);
    }

    [RelayCommand]
    private void Open(HomeFeature feature) => NavigateRequested?.Invoke(this, feature.Page);

    [RelayCommand]
    private void ManageProfiles() => NavigateRequested?.Invoke(this, typeof(ProfilesViewModel));

    [RelayCommand]
    private void TurnOn() => _app.ToggleOff(ChangeSource.Editor);

    [RelayCommand]
    private void Tour() => TourRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private Task Upgrade() => _upgrade.PurchaseAsync();

    [RelayCommand]
    private Task Rate() => _upgrade.RateAsync();

    [RelayCommand]
    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private void Refresh()
    {
        _loading = true;
        try
        {
            AppConfig config = _app.Config;
            FeatureGate features = _app.Features;
            RuleOutput? current = _app.Current;
            Profile? active = current?.ProfileId is { } id && !config.IsOff && !config.IsPaused ? _app.FindProfile(id) : null;
            IsOn = !config.IsOff;
            ShowPro = _license.CanPurchase && !_license.IsPro;

            NowPicture = ColorPreview.Large(active?.Color ?? ColorSettings.Neutral);
            NowName = active?.Name ?? Loc.Get(config.IsOff ? "Home_Off" : "Home_Original");
            NowReason = Reason(config, active, current);
            NowEffect = _effects.Current is not null && _effects.State != EffectsState.Off ? Loc.Format("Home_Effect", _effects.NameOf(_effects.CurrentId)) : null;

            Profiles.Clear();
            foreach (Profile profile in config.Profiles)
                Profiles.Add(new HomeProfile(profile.Id, profile.Name, ColorPreview.Render(profile.Color), profile.Hotkey));
            CurrentProfile = Profiles.FirstOrDefault(p => p.Id == active?.Id);

            Features.Clear();
            Features.Add(new HomeFeature(typeof(ProfilesViewModel), SymbolRegular.Color24, Loc.Get("Nav_Profiles"),
                Loc.Format(config.Profiles.Count == 1 ? "Home_OneProfile" : "Home_Profiles", config.Profiles.Count), true, false));
            Features.Add(new HomeFeature(typeof(ScheduleViewModel), SymbolRegular.Clock24, Loc.Get("Nav_Schedule"),
                ScheduleStatus(config), config.Schedule.Enabled && features.Schedules, !features.Schedules));
            int rules = config.AppRules.Count(r => r.Enabled);
            Features.Add(new HomeFeature(typeof(AppsViewModel), SymbolRegular.AppsList24, Loc.Get("Nav_Apps"),
                rules == 0 ? Loc.Get("Home_NoApps") : Loc.Format(rules == 1 ? "Home_OneApp" : "Home_Apps", rules), rules > 0 && features.AppRules, !features.AppRules));
            if (config.Ui.ShowEffects)
            {
                bool effectsOn = _effects.State is not EffectsState.Off and not EffectsState.NeedsPro;
                Features.Add(new HomeFeature(typeof(EffectsViewModel), SymbolRegular.Sparkle24, Loc.Get("Nav_Effects"),
                    effectsOn ? Loc.Format("Home_EffectOn", _effects.NameOf(_effects.CurrentId)) : Loc.Get("Common_Off"), effectsOn, !features.ScreenEffects));
            }
            int displays = _app.Monitors.Count;
            Features.Add(new HomeFeature(typeof(DisplaysViewModel), SymbolRegular.Desktop24, Loc.Get("Nav_Displays"),
                Loc.Format(displays == 1 ? "Home_OneDisplay" : "Home_Displays", displays), true, false));
            Features.Add(new HomeFeature(typeof(SettingsViewModel), SymbolRegular.Settings24, Loc.Get("Nav_Settings"),
                Loc.Get("Home_SettingsStatus"), true, false));
        }
        finally
        {
            _loading = false;
        }
    }

    private string Reason(AppConfig config, Profile? active, RuleOutput? current)
    {
        if (config.IsOff)
            return Loc.Get("Home_ReasonOff");
        if (config.IsPaused || active is null || current is null)
            return Loc.Get("Home_ReasonProfilesOff");
        return current.Reason switch
        {
            TargetReason.Schedule when current.NextEvaluation is { } until =>
                Loc.Format("Home_ReasonScheduleUntil", DayTimeline.TimeLabel(until.TimeOfDay.TotalMinutes)),
            TargetReason.Schedule => Loc.Get("Home_ReasonSchedule"),
            TargetReason.AppRule when _app.ForegroundExecutable is { } program =>
                Loc.Format("Home_ReasonApp", Path.IsPathRooted(program) ? ProgramInfo.DisplayName(program) : Path.GetFileNameWithoutExtension(program)),
            TargetReason.AppRule => Loc.Get("Home_ReasonAppUnknown"),
            _ => Loc.Get("Home_ReasonPicked"),
        };
    }

    private string ScheduleStatus(AppConfig config)
    {
        ScheduleConfig schedule = config.Schedule;
        if (!_app.Features.Schedules || !schedule.Enabled || schedule.Slots.Count == 0)
            return Loc.Get("Common_Off");
        DateTime now = _time.GetLocalNow().DateTime;
        if (ScheduleTimeline.Locate(schedule, now, _time.LocalTimeZone, id => _app.FindProfile(id) is not null) is not { } position)
            return Loc.Get("Common_On");
        string next = _app.FindProfile(position.Next.Slot.ProfileId)?.Name ?? "";
        string when = position.Next.Start.Date == now.Date
            ? DayTimeline.TimeLabel(position.Next.Start.TimeOfDay.TotalMinutes)
            : Loc.Format("Schedule_DayAndTime", position.Next.Start.ToString("dddd", CultureInfo.CurrentCulture), DayTimeline.TimeLabel(position.Next.Start.TimeOfDay.TotalMinutes));
        return Loc.Format("Home_ScheduleNext", next, when);
    }
}

/// <summary>A profile on the Home page's quick switch.</summary>
public sealed record HomeProfile(Guid Id, string Name, ImageSource Preview, string? Hotkey);

/// <summary>A feature card on the Home page: where it stands, and the page that sets it up.</summary>
/// <param name="IsActive">Whether it is doing something now (its status reads brighter).</param>
/// <param name="NeedsPro">Part of Pro, which this user doesn't have: the card says so.</param>
public sealed record HomeFeature(Type Page, SymbolRegular Icon, string Title, string Status, bool IsActive, bool NeedsPro);
