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
/// The schedule, made of changes ("from sunset, Night"). A day bar shows the result; each change is one card with
/// four plain choices. A new schedule starts from one of two common setups. Free users can build one and see it;
/// running it is Pro.
/// </summary>
public sealed partial class ScheduleViewModel : PageViewModel
{
    private readonly AppController _app;
    private readonly IUpgradeService _upgrade;
    private readonly TimeProvider _time;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };
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

        DayOfWeek first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        for (int i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)first + i) % 7);
            string name = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day);
            Days.Add(new DayChoice(day, day == Today.DayOfWeek ? Loc.Format("Schedule_Today", name) : name));
        }
        SelectedDay = Days.First(d => d.Day == Today.DayOfWeek);
        Load();
    }

    /// <summary>Which day the bar shows; only offered when some change is limited to certain days.</summary>
    public ObservableCollection<DayChoice> Days { get; } = [];

    public ObservableCollection<ScheduleSlotItem> Slots { get; } = [];
    public ObservableCollection<ProfileChoice> ProfileChoices { get; } = [];
    public ObservableCollection<Place> LocationResults { get; } = [];

    public IReadOnlyList<WhenChoice> Whens { get; } =
    [
        new(ScheduleAnchor.Sunrise, Loc.Get("Schedule_Sunrise")),
        new(ScheduleAnchor.Sunset, Loc.Get("Schedule_Sunset")),
        new(ScheduleAnchor.Time, Loc.Get("Schedule_AtTime")),
    ];

    public IReadOnlyList<FadeChoice> Fades { get; } =
        [.. new[] { 0, 5, 10, 15, 30, 45, 60, 90, 120 }.Select(m => new FadeChoice(m, m == 0 ? Loc.Get("Schedule_Instant") : Duration(m)))];

    public IReadOnlyList<DaysChoice> DaysChoices { get; } =
    [
        new(ScheduleDays.Every, Loc.Get("Schedule_EveryDay")),
        new(ScheduleDays.Weekdays, Loc.Get("Schedule_Weekdays")),
        new(ScheduleDays.Weekend, Loc.Get("Schedule_Weekends")),
        new(ScheduleDays.None, Loc.Get("Schedule_SomeDays")),
    ];

    [ObservableProperty] public partial bool Enabled { get; set; }
    [ObservableProperty] public partial bool IsPro { get; set; }
    [ObservableProperty] public partial DayChoice? SelectedDay { get; set; }
    [ObservableProperty] public partial bool ShowDayPicker { get; set; }
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
    [ObservableProperty] public partial bool HasSlots { get; set; }

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().DateTime);

    public override void OnShown()
    {
        RefreshTimeline();
        _clock.Start();
    }

    public override void OnHidden() => _clock.Stop();

    /// <summary>A time of day in the user's format, rounded to the minute.</summary>
    internal static string TimeText(TimeOnly time) => DayTimeline.TimeLabel(time.ToTimeSpan().TotalMinutes);

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
            SetEnabledSilently(false);
            return;
        }
        _app.Config.Schedule.Enabled = value;
        Save();
        RefreshStatus();
    }

    partial void OnSelectedDayChanged(DayChoice? value) => RefreshTimeline();

    partial void OnSelectedSlotIdChanged(Guid? value)
    {
        foreach (ScheduleSlotItem slot in Slots)
            slot.IsSelected = slot.Id == value;
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

    /// <summary>Adds a change after the last one, to the profile that isn't on then.</summary>
    [RelayCommand]
    private void AddSlot()
    {
        List<ScheduleSlot> slots = _app.Config.Schedule.Slots;
        var slot = new ScheduleSlot
        {
            Start = slots.Count == 0 ? new TimeOnly(7, 0) : NextFreeTime(),
            ProfileId = slots.Count == 0 ? DayProfile().Id : NightProfile().Id,
            FadeMinutes = slots.Count == 0 ? 0 : 30,
        };
        slots.Add(slot);
        SelectedSlotId = slot.Id;
        Save(reload: true);
    }

    /// <summary>Starts a schedule from a common setup, built from the user's own profiles.</summary>
    [RelayCommand]
    private void ApplyTemplate(string? name)
    {
        Profile day = DayProfile(), night = NightProfile();
        List<ScheduleSlot> slots = name switch
        {
            "hours" =>
            [
                new() { Start = new TimeOnly(7, 0), ProfileId = day.Id, FadeMinutes = 15 },
                new() { Start = new TimeOnly(21, 0), ProfileId = night.Id, FadeMinutes = 30 },
            ],
            "blank" => [new() { Start = new TimeOnly(21, 0), ProfileId = night.Id, FadeMinutes = 30 }],
            _ =>
            [
                new() { Anchor = ScheduleAnchor.Sunrise, ProfileId = day.Id, FadeMinutes = 30 },
                new() { Anchor = ScheduleAnchor.Sunset, ProfileId = night.Id, FadeMinutes = 60 },
            ],
        };
        if (slots.Any(s => s.Anchor != ScheduleAnchor.Time))
            EnsureLocation();
        _app.Config.Schedule.Slots = slots;
        // Setting one up is a clear sign it should run.
        if (IsPro)
            _app.Config.Schedule.Enabled = true;
        Save(reload: true);
    }

    internal void SlotChanged(bool needsLocation)
    {
        if (needsLocation)
            EnsureLocation();
        Save();
    }

    internal void RemoveSlot(ScheduleSlot slot)
    {
        _app.Config.Schedule.Slots.Remove(slot);
        Save(reload: true);
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
        try
        {
            IsPro = _app.Features.Schedules;
            Enabled = _app.Config.Schedule.Enabled;
            ProfileChoices.Clear();
            foreach (Profile profile in _app.Config.Profiles)
                ProfileChoices.Add(ProfileChoice.From(profile));
            Slots.Clear();
            DateOnly today = Today;
            foreach (ScheduleSlot slot in _app.Config.Schedule.Slots.OrderBy(s => ScheduleTimeline.StartOn(s, today, _app.Config.Schedule.Location, _time.LocalTimeZone)))
                Slots.Add(new ScheduleSlotItem(this, slot, ProfileChoices, Days));
            HasSlots = Slots.Count > 0;
        }
        finally
        {
            _loading = false;
        }
        RefreshTimeline();
        OnSelectedSlotIdChanged(SelectedSlotId);
    }

    private void RefreshTimeline()
    {
        ScheduleConfig schedule = _app.Config.Schedule;
        ShowDayPicker = schedule.Slots.Any(s => s.Days != ScheduleDays.Every);
        if (!ShowDayPicker && SelectedDay?.Day != Today.DayOfWeek)
        {
            SelectedDay = Days.First(d => d.Day == Today.DayOfWeek);
            return;
        }
        if (SelectedDay is null)
            return;
        TimeZoneInfo zone = _time.LocalTimeZone;
        DateOnly day = DateOfSelectedDay();
        DateTime midnight = day.ToDateTime(TimeOnly.MinValue);
        bool Valid(Guid id) => RuleEngine.FindProfile(_app.Config, id) is not null;

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
        Segments = segments;

        UsesSun = schedule.Slots.Any(s => s.Anchor != ScheduleAnchor.Time);
        (DateTime sunrise, DateTime sunset) = ScheduleTimeline.SunOn(day, schedule.Location, zone);
        Sunrise = UsesSun ? (sunrise - midnight).TotalMinutes : null;
        Sunset = UsesSun ? (sunset - midnight).TotalMinutes : null;
        DateTime now = _time.GetLocalNow().DateTime;
        Now = day == DateOnly.FromDateTime(now) ? (now - midnight).TotalMinutes : null;

        foreach (ScheduleSlotItem item in Slots)
            item.RefreshStart(day, schedule.Location, zone, day == Today);
        RefreshSunText(day);
        RefreshStatus();

        TimelineSegment Segment(ScheduleSlot slot, Guid? id, double start, double end, double fade)
        {
            Profile profile = RuleEngine.FindProfile(_app.Config, slot.ProfileId)!;
            return new TimelineSegment(id, profile.Name, ProfileColour(profile.Color), start, end, fade);
        }

        Guid? ProfileOf(TimelineSegment segment) =>
            schedule.Slots.Find(s => s.Id == segment.SlotId)?.ProfileId ?? ScheduleTimeline.Locate(schedule, midnight, zone, Valid)?.Current.Slot.ProfileId;
    }

    private void RefreshStatus()
    {
        ScheduleConfig schedule = _app.Config.Schedule;
        if (schedule.Slots.Count == 0)
        {
            StatusText = Loc.Get("Schedule_StatusEmpty");
            return;
        }
        if (!schedule.Enabled || !IsPro)
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
        string when = position.Next.Start.Date == now.Date
            ? DayTimeline.TimeLabel(position.Next.Start.TimeOfDay.TotalMinutes)
            : Loc.Format("Schedule_DayAndTime", position.Next.Start.ToString("dddd", CultureInfo.CurrentCulture), DayTimeline.TimeLabel(position.Next.Start.TimeOfDay.TotalMinutes));
        StatusText = Loc.Format("Schedule_StatusRunning", current, next, when);
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

    private DateOnly DateOfSelectedDay()
    {
        DateOnly today = Today;
        int delta = ((int)SelectedDay!.Day - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(delta);
    }

    private void SetLocation(GeoLocation location)
    {
        _app.Config.Schedule.Location = location;
        LocationError = null;
        Save(reload: true);
    }

    /// <summary>Sunrise needs a place; start from the capital of the Windows region, which the user can change.</summary>
    private void EnsureLocation()
    {
        if (_app.Config.Schedule.Location is not null)
            return;
        if (Places.ForCountry(RegionInfo.CurrentRegion.TwoLetterISORegionName) is { } place)
            _app.Config.Schedule.Location = new GeoLocation { Latitude = place.Latitude, Longitude = place.Longitude, Name = place.Name };
    }

    private TimeOnly NextFreeTime()
    {
        TimeOnly latest = _app.Config.Schedule.Slots.Where(s => s.Anchor == ScheduleAnchor.Time).Select(s => s.Start).DefaultIfEmpty(new TimeOnly(19, 0)).Max();
        return latest.AddHours(2);
    }

    private Profile DayProfile() =>
        _app.Config.Profiles.FirstOrDefault(p => p.Color.HasNeutralRamp) ?? _app.Config.Profiles[0];

    private Profile NightProfile() =>
        _app.Config.Profiles.Where(p => p.Color.TemperatureK < 6000).MinBy(p => p.Color.TemperatureK) ?? _app.Config.Profiles[^1];

    private void SetEnabledSilently(bool value)
    {
        _loading = true;
        Enabled = value;
        _loading = false;
    }

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

public sealed record DayChoice(DayOfWeek Day, string Name)
{
    public override string ToString() => Name;
}

public sealed record WhenChoice(ScheduleAnchor Anchor, string Name)
{
    public override string ToString() => Name;
}

public sealed record FadeChoice(int Minutes, string Name)
{
    public override string ToString() => Name;
}

/// <param name="Days">Every day, weekdays or weekends; <see cref="ScheduleDays.None"/> for "some days" (pick below).</param>
public sealed record DaysChoice(ScheduleDays Days, string Name)
{
    public override string ToString() => Name;
}

public sealed record TimeChoice(TimeOnly Time, string Name)
{
    public override string ToString() => Name;
}

public sealed record OffsetChoice(int Minutes, string Name)
{
    public override string ToString() => Name;
}

/// <summary>One change: from when, which profile, how it fades in, on which days.</summary>
public sealed partial class ScheduleSlotItem : ObservableObject
{
    private static readonly int[] OffsetSteps = [-120, -90, -60, -45, -30, -15, 0, 15, 30, 45, 60, 90, 120];
    private readonly ScheduleViewModel _owner;
    private readonly ScheduleSlot _slot;
    private readonly bool _ready;

    public ScheduleSlotItem(ScheduleViewModel owner, ScheduleSlot slot, IReadOnlyList<ProfileChoice> profiles, IEnumerable<DayChoice> days)
    {
        _owner = owner;
        _slot = slot;
        Profiles = profiles;
        Profile = profiles.FirstOrDefault(p => p.Id == slot.ProfileId);

        // Every quarter of an hour, plus the slot's own time if it's in between.
        var times = Enumerable.Range(0, 96).Select(i => new TimeOnly(i / 4, i % 4 * 15)).ToList();
        if (!times.Contains(slot.Start))
            times.Add(slot.Start);
        Times = [.. times.Order().Select(t => new TimeChoice(t, ScheduleViewModel.TimeText(t)))];

        var offsets = OffsetSteps.ToList();
        if (!offsets.Contains(slot.OffsetMinutes))
            offsets.Add(slot.OffsetMinutes);
        Offsets = [.. offsets.Order().Select(m => new OffsetChoice(m, OffsetText(m)))];

        When = owner.Whens.First(w => w.Anchor == slot.Anchor);
        Time = Times.First(t => t.Time == slot.Start);
        Offset = Offsets.First(o => o.Minutes == slot.OffsetMinutes);
        Fade = owner.Fades.MinBy(f => Math.Abs(f.Minutes - slot.FadeMinutes));
        DaysChoice = owner.DaysChoices.FirstOrDefault(d => d.Days == slot.Days) ?? owner.DaysChoices[^1];
        foreach (DayChoice day in days)
        {
            string name = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day.Day);
            Days.Add(new DayToggle(this, ScheduleTimeline.DayFlag(day.Day), name[..Math.Min(2, name.Length)], (slot.Days & ScheduleTimeline.DayFlag(day.Day)) != 0));
        }
        _ready = true;
    }

    public Guid Id => _slot.Id;
    public IReadOnlyList<ProfileChoice> Profiles { get; }
    public IReadOnlyList<WhenChoice> Whens => _owner.Whens;
    public IReadOnlyList<TimeChoice> Times { get; }
    public IReadOnlyList<OffsetChoice> Offsets { get; }
    public IReadOnlyList<FadeChoice> Fades => _owner.Fades;
    public IReadOnlyList<DaysChoice> DaysChoices => _owner.DaysChoices;
    public ObservableCollection<DayToggle> Days { get; } = [];

    [ObservableProperty] public partial ProfileChoice? Profile { get; set; }
    [ObservableProperty] public partial WhenChoice When { get; set; }
    [ObservableProperty] public partial TimeChoice Time { get; set; }
    [ObservableProperty] public partial OffsetChoice Offset { get; set; }
    [ObservableProperty] public partial FadeChoice? Fade { get; set; }
    [ObservableProperty] public partial DaysChoice DaysChoice { get; set; }
    [ObservableProperty] public partial string StartsText { get; set; } = "";
    [ObservableProperty] public partial bool IsSelected { get; set; }

    public bool IsTimeAnchor => When.Anchor == ScheduleAnchor.Time;
    public bool IsSunAnchor => !IsTimeAnchor;
    public bool IsSomeDays => DaysChoice.Days == ScheduleDays.None;

    partial void OnProfileChanged(ProfileChoice? value)
    {
        if (!_ready || value is null || value.Id == _slot.ProfileId)
            return;
        _slot.ProfileId = value.Id;
        _owner.SlotChanged(false);
    }

    partial void OnWhenChanged(WhenChoice value)
    {
        OnPropertyChanged(nameof(IsTimeAnchor));
        OnPropertyChanged(nameof(IsSunAnchor));
        if (!_ready || value.Anchor == _slot.Anchor)
            return;
        _slot.Anchor = value.Anchor;
        _owner.SlotChanged(value.Anchor != ScheduleAnchor.Time);
    }

    partial void OnTimeChanged(TimeChoice value)
    {
        if (!_ready || value.Time == _slot.Start)
            return;
        _slot.Start = value.Time;
        _owner.SlotChanged(false);
    }

    partial void OnOffsetChanged(OffsetChoice value)
    {
        if (!_ready || value.Minutes == _slot.OffsetMinutes)
            return;
        _slot.OffsetMinutes = value.Minutes;
        _owner.SlotChanged(false);
    }

    partial void OnFadeChanged(FadeChoice? value)
    {
        if (!_ready || value is null || value.Minutes == _slot.FadeMinutes)
            return;
        _slot.FadeMinutes = value.Minutes;
        _owner.SlotChanged(false);
    }

    partial void OnDaysChoiceChanged(DaysChoice value)
    {
        OnPropertyChanged(nameof(IsSomeDays));
        if (!_ready)
            return;
        // "Some days" keeps the current days, so the chips start from something sensible.
        ScheduleDays days = value.Days == ScheduleDays.None ? _slot.Days : value.Days;
        foreach (DayToggle toggle in Days)
            toggle.SetSilently((days & toggle.Day) != 0);
        if (days == _slot.Days)
            return;
        _slot.Days = days;
        _owner.SlotChanged(false);
    }

    internal void SetDay(ScheduleDays day, bool on)
    {
        ScheduleDays days = on ? _slot.Days | day : _slot.Days & ~day;
        if (days == ScheduleDays.None)
        {
            // A change needs at least one day; turn the last one back on.
            Days.First(d => d.Day == day).SetSilently(true);
            return;
        }
        _slot.Days = days;
        _owner.SlotChanged(false);
    }

    internal void RefreshStart(DateOnly day, GeoLocation? location, TimeZoneInfo zone, bool isToday)
    {
        DateTime start = ScheduleTimeline.StartOn(_slot, day, location, zone);
        string time = DayTimeline.TimeLabel(start.TimeOfDay.TotalMinutes);
        StartsText = IsSunAnchor ? Loc.Format(isToday ? "Schedule_StartsToday" : "Schedule_StartsThatDay", time) : "";
    }

    [RelayCommand]
    private void Remove() => _owner.RemoveSlot(_slot);

    [RelayCommand]
    private void Select() => _owner.SelectedSlotId = Id;

    private static string OffsetText(int minutes) => minutes == 0
        ? Loc.Get("Schedule_Exactly")
        : Loc.Format(minutes < 0 ? "Schedule_Before" : "Schedule_After", ScheduleViewModel.Duration(minutes));
}

public sealed partial class DayToggle : ObservableObject
{
    private readonly ScheduleSlotItem _slot;
    private bool _silent;

    public DayToggle(ScheduleSlotItem slot, ScheduleDays day, string label, bool isOn)
    {
        _slot = slot;
        Day = day;
        Label = label;
        _silent = true;
        IsOn = isOn;
        _silent = false;
    }

    public ScheduleDays Day { get; }
    public string Label { get; }

    [ObservableProperty] public partial bool IsOn { get; set; }

    partial void OnIsOnChanged(bool value)
    {
        if (!_silent)
            _slot.SetDay(Day, value);
    }

    internal void SetSilently(bool value)
    {
        _silent = true;
        IsOn = value;
        _silent = false;
    }
}
