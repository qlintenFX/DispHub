using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Controls;
using DispHub.Core.Color;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Core.Schedule;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>
/// The schedule, set up like Windows' Night light: a night profile from sunset to sunrise, or between two times.
/// Custom is there for more than two changes or different days. Underneath, every mode is a list of slots, so
/// the rule engine doesn't care which one the user picked. Free users can set one up and see it; running it is Pro.
/// </summary>
public sealed partial class ScheduleViewModel : PageViewModel
{
    private static readonly TimeOnly DefaultTurnOn = new(21, 0), DefaultTurnOff = new(7, 0);

    private readonly AppController _app;
    private readonly IUpgradeService _upgrade;
    private readonly TimeProvider _time;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };
    private List<ScheduleSlot>? _customSlots;
    private bool _loading;
    private bool _ownChange;

    public ScheduleViewModel(AppController app, IUpgradeService upgrade, TimeProvider time)
        : base(Loc.Get("Nav_Schedule"), Wpf.Ui.Controls.SymbolRegular.CalendarClock24)
    {
        _app = app;
        _upgrade = upgrade;
        _time = time;
        _clock.Tick += (_, _) => RefreshTimeline();
        _app.ConfigChanged += (_, _) =>
        {
            if (_ownChange)
                RefreshTimeline();
            else
                Load();
        };
        _app.StateChanged += (_, _) => RefreshStatus();
        _app.PowerChanged += (_, _) => RefreshStatus();
        Load();
    }

    public ObservableCollection<ProfileChoice> ProfileChoices { get; } = [];
    public ObservableCollection<ScheduleChangeItem> Changes { get; } = [];
    public ObservableCollection<Place> LocationResults { get; } = [];

    public IReadOnlyList<FadeChoice> Fades { get; } =
        [.. new[] { 0, 15, 30, 60 }.Select(m => new FadeChoice(m, m == 0 ? Loc.Get("Schedule_Instant") : Duration(m)))];

    public IReadOnlyList<WhenChoice> Whens { get; } =
    [
        new(ScheduleAnchor.Time, Loc.Get("Schedule_AtTime")),
        new(ScheduleAnchor.Sunset, Loc.Get("Schedule_Sunset")),
        new(ScheduleAnchor.Sunrise, Loc.Get("Schedule_Sunrise")),
    ];

    [ObservableProperty] public partial bool Enabled { get; set; }
    [ObservableProperty] public partial bool IsPro { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSunMode), nameof(IsHoursMode), nameof(IsCustomMode), nameof(IsSimpleMode))]
    public partial ScheduleMode Mode { get; set; }

    [ObservableProperty] public partial ProfileChoice? NightProfile { get; set; }
    [ObservableProperty] public partial ProfileChoice? DayProfile { get; set; }
    [ObservableProperty] public partial TimeOnly TurnOn { get; set; } = DefaultTurnOn;
    [ObservableProperty] public partial TimeOnly TurnOff { get; set; } = DefaultTurnOff;
    [ObservableProperty] public partial FadeChoice? Fade { get; set; }
    [ObservableProperty] public partial IReadOnlyList<TimelineSegment> Segments { get; set; } = [];
    [ObservableProperty] public partial double? Sunrise { get; set; }
    [ObservableProperty] public partial double? Sunset { get; set; }
    [ObservableProperty] public partial double? Now { get; set; }
    [ObservableProperty] public partial Guid? SelectedSlotId { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial bool UsesSun { get; set; }
    [ObservableProperty] public partial string SunText { get; set; } = "";
    [ObservableProperty] public partial bool IsEditingPlace { get; set; }
    [ObservableProperty] public partial string LocationQuery { get; set; } = "";
    [ObservableProperty] public partial string? LocationError { get; set; }
    [ObservableProperty] public partial bool IsLocating { get; set; }

    public bool IsSunMode
    {
        get => Mode == ScheduleMode.SunsetToSunrise;
        set => SetMode(value, ScheduleMode.SunsetToSunrise);
    }

    public bool IsHoursMode
    {
        get => Mode == ScheduleMode.SetHours;
        set => SetMode(value, ScheduleMode.SetHours);
    }

    public bool IsCustomMode
    {
        get => Mode == ScheduleMode.Custom;
        set => SetMode(value, ScheduleMode.Custom);
    }

    public bool IsSimpleMode => Mode != ScheduleMode.Custom;

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    public override void OnShown()
    {
        RefreshTimeline();
        _clock.Start();
    }

    public override void OnHidden() => _clock.Stop();

    /// <summary>"30 min", "1 h", "1 h 30 min".</summary>
    internal static string Duration(int minutes)
    {
        minutes = Math.Abs(minutes);
        if (minutes < 60)
            return Loc.Format("Schedule_Minutes", minutes);
        return minutes % 60 == 0 ? Loc.Format("Schedule_Hours", minutes / 60) : Loc.Format("Schedule_HoursMinutes", minutes / 60, minutes % 60);
    }

    partial void OnEnabledChanged(bool value)
    {
        if (_loading || value == _app.Config.Schedule.Enabled)
            return;
        if (value && !IsPro)
        {
            _upgrade.ShowUpgrade(Loc.Get("Upgrade_Schedules"));
            _loading = true;
            Enabled = false;
            _loading = false;
            return;
        }
        _app.Config.Schedule.Enabled = value;
        Save();
        RefreshStatus();
    }

    partial void OnNightProfileChanged(ProfileChoice? value) => WriteSimple();

    partial void OnDayProfileChanged(ProfileChoice? value) => WriteSimple();

    partial void OnTurnOnChanged(TimeOnly value) => WriteSimple();

    partial void OnTurnOffChanged(TimeOnly value) => WriteSimple();

    partial void OnFadeChanged(FadeChoice? value)
    {
        if (_loading || value is null)
            return;
        foreach (ScheduleSlot slot in _app.Config.Schedule.Slots)
            slot.FadeMinutes = value.Minutes;
        Save();
    }

    partial void OnSelectedSlotIdChanged(Guid? value)
    {
        foreach (ScheduleChangeItem change in Changes)
            change.IsSelected = change.Id == value;
    }

    partial void OnLocationQueryChanged(string value)
    {
        LocationResults.Clear();
        foreach (Place place in Places.Search(value))
            LocationResults.Add(place);
    }

    [RelayCommand]
    private void EditPlace() => IsEditingPlace = !IsEditingPlace;

    [RelayCommand]
    private void ChoosePlace(Place? place)
    {
        if (place is null)
            return;
        SetLocation(new GeoLocation { Latitude = place.Latitude, Longitude = place.Longitude, Name = place.Name });
        LocationQuery = "";
        IsEditingPlace = false;
    }

    [RelayCommand]
    private async Task UseMyLocation()
    {
        IsLocating = true;
        LocationError = null;
        try
        {
            (double latitude, double longitude, LocationService.Failure failure) = await LocationService.TryGetAsync();
            if (failure != LocationService.Failure.None)
            {
                LocationError = Loc.Get(failure == LocationService.Failure.Denied ? "Schedule_LocationDenied" : "Schedule_LocationUnavailable");
                return;
            }
            Place nearest = Places.All.MinBy(p => Distance(p.Latitude, p.Longitude, latitude, longitude))!;
            // About a kilometre: plenty for sunrise and sunset, without keeping an exact address in the settings.
            SetLocation(new GeoLocation { Latitude = Math.Round(latitude, 2), Longitude = Math.Round(longitude, 2), Name = Loc.Format("Schedule_Near", nearest.Name) });
            IsEditingPlace = false;
        }
        finally
        {
            IsLocating = false;
        }
    }

    /// <summary>Custom schedule: one more change, two hours after the last timed one.</summary>
    [RelayCommand]
    private void AddChange()
    {
        List<ScheduleSlot> slots = _app.Config.Schedule.Slots;
        TimeOnly latest = slots.Where(s => s.Anchor == ScheduleAnchor.Time).Select(s => s.Start).DefaultIfEmpty(new TimeOnly(19, 0)).Max();
        var slot = new ScheduleSlot
        {
            Start = latest.AddHours(2),
            ProfileId = slots.Count == 0 ? DefaultDayProfile().Id : DefaultNightProfile().Id,
            FadeMinutes = Fade?.Minutes ?? 30,
        };
        slots.Add(slot);
        SelectedSlotId = slot.Id;
        Save(reload: true);
    }

    internal void ChangeEdited(bool needsLocation)
    {
        if (needsLocation)
            EnsureLocation();
        _customSlots = null;
        Save();
    }

    internal void RemoveChange(ScheduleSlot slot)
    {
        _app.Config.Schedule.Slots.Remove(slot);
        Save(reload: true);
    }

    private void SetMode(bool selected, ScheduleMode mode)
    {
        if (!selected || _loading || Mode == mode)
            return;
        ScheduleConfig schedule = _app.Config.Schedule;
        if (Mode == ScheduleMode.Custom)
            _customSlots = schedule.Slots; // Back to custom in this session brings these back.
        Mode = mode;
        schedule.Mode = mode;
        if (mode == ScheduleMode.Custom)
        {
            if (_customSlots is { Count: > 0 } custom)
                schedule.Slots = custom;
            Save(reload: true);
            return;
        }
        WriteSimple(force: true);
        Load();
    }

    /// <summary>The simple modes are two slots, night first, rebuilt from the choices on the page.</summary>
    private void WriteSimple(bool force = false)
    {
        if ((_loading && !force) || Mode == ScheduleMode.Custom)
            return;
        ScheduleConfig schedule = _app.Config.Schedule;
        Guid night = NightProfile?.Id ?? DefaultNightProfile().Id, day = DayProfile?.Id ?? DefaultDayProfile().Id;
        int fade = Fade?.Minutes ?? 30;
        ScheduleSlot nightSlot = schedule.Slots.ElementAtOrDefault(0) ?? new ScheduleSlot();
        ScheduleSlot daySlot = schedule.Slots.ElementAtOrDefault(1) ?? new ScheduleSlot();
        bool sun = Mode == ScheduleMode.SunsetToSunrise;
        foreach ((ScheduleSlot slot, Guid profile, ScheduleAnchor anchor, TimeOnly start) in new[]
                 {
                     (nightSlot, night, ScheduleAnchor.Sunset, TurnOn),
                     (daySlot, day, ScheduleAnchor.Sunrise, TurnOff),
                 })
        {
            slot.ProfileId = profile;
            slot.Anchor = sun ? anchor : ScheduleAnchor.Time;
            slot.Start = sun ? default : start;
            slot.OffsetMinutes = 0;
            slot.Days = ScheduleDays.Every;
            slot.FadeMinutes = fade;
        }
        schedule.Mode = Mode;
        schedule.Slots = [nightSlot, daySlot];
        if (sun)
            EnsureLocation();
        Save();
    }

    private void Save(bool reload = false)
    {
        _ownChange = !reload;
        try
        {
            _app.SettingsChanged();
        }
        finally
        {
            _ownChange = false;
        }
    }

    private void Load()
    {
        _loading = true;
        ScheduleConfig schedule = _app.Config.Schedule;
        try
        {
            IsPro = _app.Features.Schedules;
            Enabled = schedule.Enabled;
            Mode = schedule.Mode;
            ProfileChoices.Clear();
            foreach (Profile profile in _app.Config.Profiles)
                ProfileChoices.Add(ProfileChoice.From(profile));

            ScheduleSlot? night = Mode == ScheduleMode.Custom ? null : schedule.Slots.ElementAtOrDefault(0);
            ScheduleSlot? day = Mode == ScheduleMode.Custom ? null : schedule.Slots.ElementAtOrDefault(1);
            NightProfile = ProfileChoices.FirstOrDefault(p => p.Id == (night?.ProfileId ?? DefaultNightProfile().Id));
            DayProfile = ProfileChoices.FirstOrDefault(p => p.Id == (day?.ProfileId ?? DefaultDayProfile().Id));
            if (Mode == ScheduleMode.SetHours && night is not null && day is not null)
                (TurnOn, TurnOff) = (night.Start, day.Start);
            int fade = schedule.Slots.Select(s => s.FadeMinutes).DefaultIfEmpty(30).Max();
            Fade = Fades.MinBy(f => Math.Abs(f.Minutes - fade));

            Changes.Clear();
            if (Mode == ScheduleMode.Custom)
            {
                DateOnly today = Today;
                foreach (ScheduleSlot slot in schedule.Slots.OrderBy(s => ScheduleTimeline.StartOn(s, today, schedule.Location, _time.LocalTimeZone)))
                    Changes.Add(new ScheduleChangeItem(this, slot, ProfileChoices));
            }
        }
        finally
        {
            _loading = false;
        }

        // A new schedule starts as the common one, so the page shows (and can run) something right away.
        if (Mode != ScheduleMode.Custom && schedule.Slots.Count != 2)
            WriteSimple(force: true);
        RefreshTimeline();
        OnSelectedSlotIdChanged(SelectedSlotId);
    }

    private void RefreshTimeline()
    {
        ScheduleConfig schedule = _app.Config.Schedule;
        TimeZoneInfo zone = _time.LocalTimeZone;
        DateOnly day = Today;
        DateTime midnight = day.ToDateTime(TimeOnly.MinValue);
        Segments = SegmentsFor(_app.Config, day, zone);

        UsesSun = schedule.Slots.Any(s => s.Anchor != ScheduleAnchor.Time);
        (DateTime sunrise, DateTime sunset) = ScheduleTimeline.SunOn(day, schedule.Location, zone);
        Sunrise = UsesSun ? (sunrise - midnight).TotalMinutes : null;
        Sunset = UsesSun ? (sunset - midnight).TotalMinutes : null;
        Now = (_time.GetLocalNow().DateTime - midnight).TotalMinutes;

        RefreshSunText(day);
        RefreshStatus();
    }

    /// <summary>The schedule's profiles across <paramref name="day"/>, for a <see cref="DayTimeline"/>.</summary>
    internal static IReadOnlyList<TimelineSegment> SegmentsFor(AppConfig config, DateOnly day, TimeZoneInfo zone)
    {
        ScheduleConfig schedule = config.Schedule;
        DateTime midnight = day.ToDateTime(TimeOnly.MinValue);
        bool Valid(Guid id) => RuleEngine.FindProfile(config, id) is not null;

        var segments = new List<TimelineSegment>();
        IReadOnlyList<ScheduleOccurrence> occurrences = ScheduleTimeline.ForDay(schedule, day, zone, Valid);
        if (ScheduleTimeline.Locate(schedule, midnight, zone, Valid) is { } atMidnight && (occurrences.Count == 0 || occurrences[0].Start > midnight))
        {
            double end = occurrences.Count > 0 ? (occurrences[0].Start - midnight).TotalMinutes : 24 * 60;
            segments.Add(Segment(atMidnight.Current.Slot, null, 0, end, 0));
        }
        for (int i = 0; i < occurrences.Count; i++)
        {
            double start = (occurrences[i].Start - midnight).TotalMinutes;
            double end = i + 1 < occurrences.Count ? (occurrences[i + 1].Start - midnight).TotalMinutes : 24 * 60;
            Guid? previous = segments.Count > 0 ? ProfileOf(segments[^1]) : null;
            ScheduleSlot slot = occurrences[i].Slot;
            double fade = previous is { } p && p != slot.ProfileId ? slot.FadeMinutes : 0;
            segments.Add(Segment(slot, slot.Id, start, end, fade));
        }
        return segments;

        TimelineSegment Segment(ScheduleSlot slot, Guid? id, double start, double end, double fade)
        {
            Profile profile = RuleEngine.FindProfile(config, slot.ProfileId)!;
            return new TimelineSegment(id, profile.Name, ProfileColour(profile.Color), start, end, fade, ColorPreview.Render(profile.Color));
        }

        Guid? ProfileOf(TimelineSegment segment) =>
            schedule.Slots.Find(s => s.Id == segment.SlotId)?.ProfileId ?? ScheduleTimeline.Locate(schedule, midnight, zone, Valid)?.Current.Slot.ProfileId;
    }

    private void RefreshStatus()
    {
        ScheduleConfig schedule = _app.Config.Schedule;
        if (_app.Config.IsOff)
        {
            StatusText = Loc.Get("Schedule_StatusDispHubOff");
            return;
        }
        if (!schedule.Enabled || !IsPro || schedule.Slots.Count == 0)
        {
            StatusText = Loc.Get(IsPro ? "Schedule_StatusOff" : "Schedule_StatusPro");
            return;
        }
        DateTime now = _time.GetLocalNow().DateTime;
        if (ScheduleTimeline.Locate(schedule, now, _time.LocalTimeZone, id => RuleEngine.FindProfile(_app.Config, id) is not null) is not { } position)
        {
            StatusText = "";
            return;
        }
        string current = RuleEngine.FindProfile(_app.Config, position.Current.Slot.ProfileId)?.Name ?? "";
        string next = RuleEngine.FindProfile(_app.Config, position.Next.Slot.ProfileId)?.Name ?? "";
        StatusText = Loc.Format("Schedule_StatusRunning", current, next, DayTimeline.WhenLabel(position.Next.Start, now));
    }

    private void RefreshSunText(DateOnly day)
    {
        GeoLocation? location = _app.Config.Schedule.Location;
        if (location is null)
        {
            SunText = Loc.Format("Schedule_NoLocation",
                DayTimeline.TimeLabel(ScheduleTimeline.FallbackSunrise.ToTimeSpan().TotalMinutes),
                DayTimeline.TimeLabel(ScheduleTimeline.FallbackSunset.ToTimeSpan().TotalMinutes));
            return;
        }
        string name = location.Name ?? string.Create(CultureInfo.CurrentCulture, $"{location.Latitude:0.00}, {location.Longitude:0.00}");
        SunText = SunCalculator.SunTimes(day, location.Latitude, location.Longitude, _time.LocalTimeZone) is { } sun
            ? Loc.Format("Schedule_LocationSun", name, DayTimeline.TimeLabel(sun.Sunrise.TimeOfDay.TotalMinutes), DayTimeline.TimeLabel(sun.Sunset.TimeOfDay.TotalMinutes))
            : Loc.Format("Schedule_LocationPolar", name);
    }

    private void SetLocation(GeoLocation location)
    {
        _app.Config.Schedule.Location = location;
        LocationError = null;
        Save(reload: true);
    }

    /// <summary>Sunrise needs a place: start from the capital of the user's home country in Windows.</summary>
    private void EnsureLocation()
    {
        if (_app.Config.Schedule.Location is not null)
            return;
        if (Places.ForCountry(HomeCountry()) is { } place)
            _app.Config.Schedule.Location = new GeoLocation { Latitude = place.Latitude, Longitude = place.Longitude, Name = place.Name };
    }

    /// <summary>
    /// The country set as home location in Windows (Settings, Time and language, Region). The formatting culture
    /// is often en-US on PCs elsewhere, so it is only the fallback.
    /// </summary>
    private static string HomeCountry()
    {
        try
        {
            string region = global::Windows.System.UserProfile.GlobalizationPreferences.HomeGeographicRegion;
            if (region.Length == 2)
                return region;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
        }
        return RegionInfo.CurrentRegion.TwoLetterISORegionName;
    }

    private Profile DefaultDayProfile() =>
        _app.Config.Profiles.FirstOrDefault(p => p.Color.HasNeutralRamp) ?? _app.Config.Profiles[0];

    private Profile DefaultNightProfile() =>
        _app.Config.Profiles.Where(p => p.Color.TemperatureK < 6000).MinBy(p => p.Color.TemperatureK) ?? _app.Config.Profiles[^1];

    /// <summary>
    /// A colour that says what a profile does: neutral grey for original colours, amber as it gets warmer, darker
    /// as it gets dimmer.
    /// </summary>
    internal static Color ProfileColour(ColorSettings color)
    {
        (double r, double g, double b) = Blackbody.Multipliers(color.TemperatureK);
        double level = 128 * (1 + (color.Brightness / 100.0)) * Math.Pow(1 / Math.Max(color.Gamma, 0.3), 0.25);
        byte C(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
        return Color.FromRgb(C(level * r * 1.12), C(level * g * 1.04), C(level * b));
    }

    private static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
        double a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2)) +
                   (Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public sealed record WhenChoice(ScheduleAnchor Anchor, string Name)
{
    public override string ToString() => Name;
}

public sealed record FadeChoice(int Minutes, string Name)
{
    public override string ToString() => Name;
}

public sealed record DaysChoice(ScheduleDays Days, string Name)
{
    public override string ToString() => Name;
}

/// <summary>One change in a custom schedule: when, which profile, and on which days.</summary>
public sealed partial class ScheduleChangeItem : ObservableObject
{
    private readonly ScheduleViewModel _owner;
    private readonly ScheduleSlot _slot;
    private readonly bool _ready;

    public ScheduleChangeItem(ScheduleViewModel owner, ScheduleSlot slot, IReadOnlyList<ProfileChoice> profiles)
    {
        _owner = owner;
        _slot = slot;
        Profiles = profiles;
        DaysChoices = [.. StandardDays(), .. StandardDays().Any(d => d.Days == slot.Days) ? [] : new[] { new DaysChoice(slot.Days, DaysText(slot.Days)) }];
        Profile = profiles.FirstOrDefault(p => p.Id == slot.ProfileId);
        When = owner.Whens.First(w => w.Anchor == slot.Anchor);
        Time = slot.Start;
        DaysChoice = DaysChoices.First(d => d.Days == slot.Days);
        _ready = true;
    }

    public Guid Id => _slot.Id;
    public IReadOnlyList<ProfileChoice> Profiles { get; }
    public IReadOnlyList<WhenChoice> Whens => _owner.Whens;
    public IReadOnlyList<DaysChoice> DaysChoices { get; }

    [ObservableProperty] public partial ProfileChoice? Profile { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimeAnchor))]
    public partial WhenChoice When { get; set; }

    [ObservableProperty] public partial TimeOnly Time { get; set; }
    [ObservableProperty] public partial DaysChoice DaysChoice { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    public bool IsTimeAnchor => When.Anchor == ScheduleAnchor.Time;

    partial void OnProfileChanged(ProfileChoice? value)
    {
        if (!_ready || value is null || value.Id == _slot.ProfileId)
            return;
        _slot.ProfileId = value.Id;
        _owner.ChangeEdited(false);
    }

    partial void OnWhenChanged(WhenChoice value)
    {
        if (!_ready || value.Anchor == _slot.Anchor)
            return;
        _slot.Anchor = value.Anchor;
        _slot.OffsetMinutes = 0;
        _owner.ChangeEdited(value.Anchor != ScheduleAnchor.Time);
    }

    partial void OnTimeChanged(TimeOnly value)
    {
        if (!_ready || value == _slot.Start)
            return;
        _slot.Start = value;
        _owner.ChangeEdited(false);
    }

    partial void OnDaysChoiceChanged(DaysChoice value)
    {
        if (!_ready || value.Days == _slot.Days)
            return;
        _slot.Days = value.Days;
        _owner.ChangeEdited(false);
    }

    [RelayCommand]
    private void Remove() => _owner.RemoveChange(_slot);

    [RelayCommand]
    private void Select() => _owner.SelectedSlotId = Id;

    private static DaysChoice[] StandardDays() =>
    [
        new(ScheduleDays.Every, Loc.Get("Schedule_EveryDay")),
        new(ScheduleDays.Weekdays, Loc.Get("Schedule_Weekdays")),
        new(ScheduleDays.Weekend, Loc.Get("Schedule_Weekends")),
    ];

    /// <summary>Days from an older or hand-made schedule, such as "Mon, Wed, Fri".</summary>
    private static string DaysText(ScheduleDays days)
    {
        DateTimeFormatInfo format = CultureInfo.CurrentCulture.DateTimeFormat;
        IEnumerable<DayOfWeek> week = Enumerable.Range(0, 7).Select(i => (DayOfWeek)(((int)format.FirstDayOfWeek + i) % 7));
        return string.Join(", ", week.Where(d => (days & ScheduleTimeline.DayFlag(d)) != 0).Select(format.GetAbbreviatedDayName));
    }
}
