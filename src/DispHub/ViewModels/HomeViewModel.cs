// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

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
/// The start page. It answers what is on the screen right now and why, shows the day ahead on the schedule, where
/// each feature stands with a way into it, and, for free users, what Pro adds.
/// </summary>
public sealed partial class HomeViewModel : PageViewModel
{
    public const string ChangelogUrl = AboutViewModel.RepositoryUrl + "/blob/main/CHANGELOG.md";

    private readonly AppController _app;
    private readonly ILicenseService _license;
    private readonly IUpgradeService _upgrade;
    private readonly TimeProvider _time;
    private readonly System.Windows.Threading.DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };

    public HomeViewModel(AppController app, ILicenseService license, IUpgradeService upgrade, TimeProvider time)
        : base(Loc.Get("Nav_Home"), SymbolRegular.Home24)
    {
        _app = app;
        _license = license;
        _upgrade = upgrade;
        _time = time;
        _app.StateChanged += (_, _) => Refresh();
        _app.ConfigChanged += (_, _) => Refresh();
        _app.PowerChanged += (_, _) => Refresh();
        _app.ProfileEdited += (_, _) => Refresh();
        _app.MonitorsChanged += (_, _) => Refresh();
        _license.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        _clock.Tick += (_, _) => TodayNow = MinutesSinceMidnight();
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

    [ObservableProperty] public partial bool IsOn { get; set; }

    /// <summary>DispHub is on but its profiles are off: the card offers to turn them back on.</summary>
    [ObservableProperty] public partial bool ProfilesOff { get; set; }

    /// <summary>A schedule runs: Home shows today on it.</summary>
    [ObservableProperty] public partial bool ShowToday { get; set; }

    [ObservableProperty] public partial IReadOnlyList<TimelineSegment> TodaySegments { get; set; } = [];

    [ObservableProperty] public partial double? TodayNow { get; set; }

    [ObservableProperty] public partial string TodayText { get; set; } = "";

    public ObservableCollection<HomeFeature> Features { get; } = [];

    /// <summary>Pro can be bought here and isn't yet.</summary>
    [ObservableProperty] public partial bool ShowPro { get; set; }

    /// <summary>A Microsoft Store build, which can be rated in the Store.</summary>
    public bool CanRate => _license.CanPurchase;

    public static string Version => Loc.Format("Home_Version", AboutViewModel.Version);

    public override void OnShown()
    {
        Refresh();
        _clock.Start();
    }

    public override void OnHidden() => _clock.Stop();

    [RelayCommand]
    private void Open(HomeFeature feature) => NavigateRequested?.Invoke(this, feature.Page);

    [RelayCommand]
    private void OpenSchedule() => NavigateRequested?.Invoke(this, typeof(ScheduleViewModel));

    [RelayCommand]
    private void TurnOn() => _app.SetOn(true, ChangeSource.Editor);

    [RelayCommand]
    private void TurnProfilesOn() => _app.SetProfilesOn(true, ChangeSource.Editor);

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
        AppConfig config = _app.Config;
        FeatureGate features = _app.Features;
        RuleOutput? current = _app.Current;
        Profile? active = current?.ProfileId is { } id && !config.IsOff && !config.IsPaused ? _app.FindProfile(id) : null;
        IsOn = !config.IsOff;
        ProfilesOff = !config.IsOff && config.IsPaused;
        ShowPro = _license.CanPurchase && !_license.IsPro;

        NowPicture = ColorPreview.Home(active?.Color ?? ColorSettings.Neutral);
        NowName = active?.Name ?? Loc.Get(config.IsOff ? "Home_Off" : "Home_Original");
        NowReason = Reason(config, active, current);

        ShowToday = features.Schedules && config.Schedule.Enabled && config.Schedule.Slots.Count > 0;
        if (ShowToday)
        {
            TodaySegments = ScheduleViewModel.SegmentsFor(config, DateOnly.FromDateTime(_time.GetLocalNow().DateTime), _time.LocalTimeZone);
            TodayNow = MinutesSinceMidnight();
            TodayText = ScheduleStatus(config);
        }

        Features.Clear();
        Features.Add(new HomeFeature(typeof(ProfilesViewModel), SymbolRegular.Color24, Loc.Get("Nav_Profiles"),
            Loc.Format(config.Profiles.Count == 1 ? "Home_OneProfile" : "Home_Profiles", config.Profiles.Count), true, false));
        int rules = config.AppRules.Count(r => r.Enabled);
        Features.Add(new HomeFeature(typeof(AppsViewModel), SymbolRegular.AppsList24, Loc.Get("Nav_Apps"),
            rules == 0 ? Loc.Get("Home_NoApps") : Loc.Format(rules == 1 ? "Home_OneApp" : "Home_Apps", rules), rules > 0, false));
        int displays = _app.Monitors.Count;
        Features.Add(new HomeFeature(typeof(DisplaysViewModel), SymbolRegular.Desktop24, Loc.Get("Nav_Displays"),
            Loc.Format(displays == 1 ? "Home_OneDisplay" : "Home_Displays", displays), true, false));
        Features.Add(new HomeFeature(typeof(SettingsViewModel), SymbolRegular.Settings24, Loc.Get("Nav_Settings"),
            Loc.Get("Home_SettingsStatus"), true, false));
    }

    private double MinutesSinceMidnight() => _time.GetLocalNow().DateTime.TimeOfDay.TotalMinutes;

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
            // Chosen during the schedule: it lasts until the schedule's next change.
            TargetReason.Manual when NextScheduleChange(config) is { } next => Loc.Format("Home_ReasonManualUntil", DayTimeline.TimeLabel(next.TimeOfDay.TotalMinutes)),
            _ => Loc.Get("Home_ReasonSelected"),
        };
    }

    private DateTime? NextScheduleChange(AppConfig config) =>
        _app.Features.Schedules && config.Schedule.Enabled &&
        ScheduleTimeline.Locate(config.Schedule, _time.GetLocalNow().DateTime, _time.LocalTimeZone, id => _app.FindProfile(id) is not null) is { } position
            ? position.Next.Start
            : null;

    private string ScheduleStatus(AppConfig config)
    {
        ScheduleConfig schedule = config.Schedule;
        if (!_app.Features.Schedules || !schedule.Enabled || schedule.Slots.Count == 0)
            return Loc.Get("Common_Off");
        DateTime now = _time.GetLocalNow().DateTime;
        if (ScheduleTimeline.Locate(schedule, now, _time.LocalTimeZone, id => _app.FindProfile(id) is not null) is not { } position)
            return Loc.Get("Common_On");
        string next = _app.FindProfile(position.Next.Slot.ProfileId)?.Name ?? "";
        return Loc.Format("Home_ScheduleNext", next, DayTimeline.WhenLabel(position.Next.Start, now));
    }
}

/// <summary>A feature card on the Home page: where it stands, and the page that sets it up.</summary>
/// <param name="IsActive">Whether it is doing something now (its status reads brighter).</param>
/// <param name="NeedsPro">Part of Pro, which this user doesn't have: the card says so.</param>
public sealed record HomeFeature(Type Page, SymbolRegular Icon, string Title, string Status, bool IsActive, bool NeedsPro);
