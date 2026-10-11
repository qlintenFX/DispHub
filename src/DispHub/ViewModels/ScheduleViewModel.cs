using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Controls;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Core.Schedule;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>
/// The schedule: a day view of which profile runs when, the slots that make it up, and the location used for
/// sunrise and sunset. Free users can build a schedule and see it; running it is Pro.
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
            Days.Add(new DayChoice(day, CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedDayName(day)));
        }
        SelectedDay = Days.First(d => d.Day == Today.DayOfWeek);
        Load();
    }

    public ObservableCollection<DayChoice> Days { get; } = [];
    public ObservableCollection<ScheduleSlotItem> Slots { get; } = [];
    public ObservableCollection<ProfileChoice> ProfileChoices { get; } = [];
    public ObservableCollection<Place> LocationResults { get; } = [];

    public IReadOnlyList<AnchorChoice> Anchors { get; } =
    [
        new(ScheduleAnchor.Time, Loc.Get("Schedule_AtTime")),
        new(ScheduleAnchor.Sunrise, Loc.Get("Schedule_Sunrise")),
        new(ScheduleAnchor.Sunset, Loc.Get("Schedule_Sunset")),
    ];

    public IReadOnlyList<FadeChoice> Fades { get; } =
        [.. new[] { 0, 5, 10, 15, 30, 45, 60, 90, 120 }.Select(m => new FadeChoice(m, m == 0 ? Loc.Get("Schedule_Instant") : Loc.Format("Schedule_Minutes", m)))];

    [ObservableProperty] public partial bool Enabled { get; set; }
    [ObservableProperty] public partial bool IsPro { get; set; }
    [ObservableProperty] public partial DayChoice? SelectedDay { get; set; }
    [ObservableProperty] public partial IReadOnlyList<TimelineSegment> Segments { get; set; } = [];
    [ObservableProperty] public partial double? Sunrise { get; set; }
    [ObservableProperty] public partial double? Sunset { get; set; }
    [ObservableProperty] public partial double? Now { get; set; }
    [ObservableProperty] public partial Guid? SelectedSlotId { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial string LocationText { get; set; } = "";
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
    private void ChoosePlace(Place? place)
    {
        if (place is null)
            return;
        SetLocation(new GeoLocation { Latitude = place.Latitude, Longitude = place.Longitude, Name = place.Name });
        LocationQuery = "";
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
        }
        finally
        {
            IsLocating = false;
        }
    }

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

    /// <summary>Replaces the schedule with a ready-made one built from the user's own profiles.</summary>
    [RelayCommand]
    private void ApplyTemplate(string? name)
    {
        Profile day = DayProfile(), night = NightProfile(), dim = DimProfile();
        List<ScheduleSlot> slots = name switch
        {
            "workweek" =>
            [
                new() { Start = new TimeOnly(8, 0), Days = ScheduleDays.Weekdays, ProfileId = day.Id, FadeMinutes = 15 },
                new() { Start = new TimeOnly(10, 0), Days = ScheduleDays.Weekend, ProfileId = day.Id, FadeMinutes = 15 },
                new() { Start = new TimeOnly(22, 0), ProfileId = night.Id, FadeMinutes = 45 },
            ],
            "latenight" =>
            [
                new() { Anchor = ScheduleAnchor.Sunrise, ProfileId = day.Id, FadeMinutes = 15 },
                new() { Anchor = ScheduleAnchor.Sunset, ProfileId = night.Id, FadeMinutes = 60 },
                new() { Start = new TimeOnly(23, 30), ProfileId = dim.Id, FadeMinutes = 30 },
            ],
            _ =>
            [
                new() { Anchor = ScheduleAnchor.Sunrise, ProfileId = day.Id, FadeMinutes = 30 },
                new() { Anchor = ScheduleAnchor.Sunset, OffsetMinutes = 30, ProfileId = night.Id, FadeMinutes = 60 },
            ],
        };
        if (slots.Any(s => s.Anchor != ScheduleAnchor.Time))
            EnsureLocation();
        _app.Config.Schedule.Slots = slots;
        Save(reload: true);
    }

    /// <summary>A handle on the day view was dragged: move that slot's start, keeping its anchor.</summary>
    public void MoveSlot(Guid slotId, double minutes)
    {
        if (_app.Config.Schedule.Slots.Find(s => s.Id == slotId) is not { } slot || SelectedDay is null)
            return;
        DateOnly day = DateOfSelectedDay();
        if (slot.Anchor == ScheduleAnchor.Time)
        {
            slot.Start = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes));
        }
        else
        {
            (DateTime sunrise, DateTime sunset) = ScheduleTimeline.SunOn(day, _app.Config.Schedule.Location, _time.LocalTimeZone);
            double sun = ((slot.Anchor == ScheduleAnchor.Sunrise ? sunrise : sunset) - day.ToDateTime(TimeOnly.MinValue)).TotalMinutes;
            slot.OffsetMinutes = (int)Math.Clamp(Math.Round((minutes - sun) / 5) * 5, -ScheduleTimeline.MaxSunOffsetMinutes, ScheduleTimeline.MaxSunOffsetMinutes);
        }
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
        if (SelectedDay is null)
            return;
        ScheduleConfig schedule = _app.Config.Schedule;
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

        if (schedule.Location is not null && SunCalculator.SunTimes(day, schedule.Location.Latitude, schedule.Location.Longitude, zone) is { } sun)
        {
            Sunrise = (sun.Sunrise - midnight).TotalMinutes;
            Sunset = (sun.Sunset - midnight).TotalMinutes;
        }
        else
        {
            Sunrise = Sunset = null;
        }
        DateTime now = _time.GetLocalNow().DateTime;
        Now = day == DateOnly.FromDateTime(now) ? (now - midnight).TotalMinutes : null;

        foreach (ScheduleSlotItem item in Slots)
            item.RefreshStart(day, schedule.Location, zone);
        RefreshLocationText(day);
        RefreshStatus();

        TimelineSegment Segment(ScheduleSlot slot, Guid? id, double start, double end, double fade)
        {
            Profile profile = RuleEngine.FindProfile(_app.Config, slot.ProfileId)!;
            return new TimelineSegment(id, profile.Name, ColorPreview.Render(profile.Color), start, end, fade);
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

    private void RefreshLocationText(DateOnly day)
    {
        GeoLocation? location = _app.Config.Schedule.Location;
        if (location is null)
        {
            LocationText = Loc.Format("Schedule_NoLocation",
                DayTimeline.TimeLabel(ScheduleTimeline.FallbackSunrise.ToTimeSpan().TotalMinutes),
                DayTimeline.TimeLabel(ScheduleTimeline.FallbackSunset.ToTimeSpan().TotalMinutes));
            return;
        }
        string name = location.Name ?? string.Create(CultureInfo.CurrentCulture, $"{location.Latitude:0.00}, {location.Longitude:0.00}");
        LocationText = SunCalculator.SunTimes(day, location.Latitude, location.Longitude, _time.LocalTimeZone) is { } sun
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

    private Profile DimProfile() =>
        _app.Config.Profiles.Where(p => p.Color.Brightness < 0).MinBy(p => p.Color.Brightness) ?? NightProfile();

    private void SetEnabledSilently(bool value)
    {
        _loading = true;
        Enabled = value;
        _loading = false;
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

public sealed record AnchorChoice(ScheduleAnchor Anchor, string Name)
{
    public override string ToString() => Name;
}

public sealed record FadeChoice(int Minutes, string Name)
{
    public override string ToString() => Name;
}

/// <summary>One slot in the list under the day view.</summary>
public sealed partial class ScheduleSlotItem : ObservableObject
{
    private readonly ScheduleViewModel _owner;
    private readonly ScheduleSlot _slot;
    private readonly bool _ready;

    public ScheduleSlotItem(ScheduleViewModel owner, ScheduleSlot slot, IReadOnlyList<ProfileChoice> profiles, IEnumerable<DayChoice> days)
    {
        _owner = owner;
        _slot = slot;
        Profiles = profiles;
        Profile = profiles.FirstOrDefault(p => p.Id == slot.ProfileId);
        Anchor = owner.Anchors.First(a => a.Anchor == slot.Anchor);
        Time = slot.Start.ToString("t", CultureInfo.CurrentCulture);
        Offset = slot.OffsetMinutes;
        Fade = owner.Fades.MinBy(f => Math.Abs(f.Minutes - slot.FadeMinutes));
        foreach (DayChoice day in days)
            Days.Add(new DayToggle(this, ScheduleTimeline.DayFlag(day.Day), day.Name[..Math.Min(2, day.Name.Length)], (slot.Days & ScheduleTimeline.DayFlag(day.Day)) != 0));
        _ready = true;
    }

    public Guid Id => _slot.Id;
    public IReadOnlyList<ProfileChoice> Profiles { get; }
    public IReadOnlyList<AnchorChoice> Anchors => _owner.Anchors;
    public IReadOnlyList<FadeChoice> Fades => _owner.Fades;
    public ObservableCollection<DayToggle> Days { get; } = [];

    [ObservableProperty] public partial ProfileChoice? Profile { get; set; }
    [ObservableProperty] public partial AnchorChoice Anchor { get; set; }
    [ObservableProperty] public partial string Time { get; set; }
    [ObservableProperty] public partial bool TimeIsInvalid { get; set; }
    [ObservableProperty] public partial double Offset { get; set; }
    [ObservableProperty] public partial string OffsetText { get; set; } = "";
    [ObservableProperty] public partial string StartsText { get; set; } = "";
    [ObservableProperty] public partial FadeChoice? Fade { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    public bool IsTimeAnchor => Anchor.Anchor == ScheduleAnchor.Time;
    public bool IsSunAnchor => !IsTimeAnchor;

    partial void OnProfileChanged(ProfileChoice? value)
    {
        if (!_ready || value is null || value.Id == _slot.ProfileId)
            return;
        _slot.ProfileId = value.Id;
        _owner.SlotChanged(false);
    }

    partial void OnAnchorChanged(AnchorChoice value)
    {
        OnPropertyChanged(nameof(IsTimeAnchor));
        OnPropertyChanged(nameof(IsSunAnchor));
        if (!_ready || value.Anchor == _slot.Anchor)
            return;
        _slot.Anchor = value.Anchor;
        _owner.SlotChanged(value.Anchor != ScheduleAnchor.Time);
    }

    partial void OnTimeChanged(string value)
    {
        if (!_ready)
            return;
        if (!TimeOnly.TryParse(value, CultureInfo.CurrentCulture, out TimeOnly time) &&
            !TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out time))
        {
            TimeIsInvalid = true;
            return;
        }
        TimeIsInvalid = false;
        if (time == _slot.Start)
            return;
        _slot.Start = time;
        _owner.SlotChanged(false);
    }

    partial void OnOffsetChanged(double value)
    {
        int minutes = (int)Math.Round(value / 5) * 5;
        OffsetText = minutes == 0 ? Loc.Get(_slot.Anchor == ScheduleAnchor.Sunrise ? "Schedule_AtSunrise" : "Schedule_AtSunset")
            : Loc.Format(minutes < 0 ? "Schedule_Before" : "Schedule_After", Math.Abs(minutes));
        if (!_ready || minutes == _slot.OffsetMinutes)
            return;
        _slot.OffsetMinutes = minutes;
        _owner.SlotChanged(false);
    }

    partial void OnFadeChanged(FadeChoice? value)
    {
        if (!_ready || value is null || value.Minutes == _slot.FadeMinutes)
            return;
        _slot.FadeMinutes = value.Minutes;
        _owner.SlotChanged(false);
    }

    internal void SetDay(ScheduleDays day, bool on)
    {
        ScheduleDays days = on ? _slot.Days | day : _slot.Days & ~day;
        if (days == ScheduleDays.None)
        {
            // A slot needs at least one day; turn the last one back on.
            Days.First(d => d.Day == day).SetSilently(true);
            return;
        }
        _slot.Days = days;
        _owner.SlotChanged(false);
    }

    internal void RefreshStart(DateOnly day, GeoLocation? location, TimeZoneInfo zone)
    {
        DateTime start = ScheduleTimeline.StartOn(_slot, day, location, zone);
        StartsText = Loc.Format("Schedule_StartsAt", DayTimeline.TimeLabel(start.TimeOfDay.TotalMinutes));
        OnOffsetChanged(Offset);
    }

    [RelayCommand]
    private void Remove() => _owner.RemoveSlot(_slot);

    [RelayCommand]
    private void Select() => _owner.SelectedSlotId = Id;
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
