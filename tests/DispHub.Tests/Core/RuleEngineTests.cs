using DispHub.Core.Color;
using DispHub.Core.Model;
using DispHub.Core.Rules;

namespace DispHub.Tests.Core;

public sealed class RuleEngineTests
{
    private const string MonitorA = "monitor-a";
    private const string MonitorB = "monitor-b";
    private static readonly DateTime Noon = new(2026, 10, 10, 12, 0, 0);

    private readonly Profile _standard = new() { Name = "Standard" };
    private readonly Profile _gaming = new() { Name = "Gaming", Color = new ColorSettings(Gamma: 1.3, Vibrance: 80) };
    private readonly Profile _night = new() { Name = "Night", Color = new ColorSettings(TemperatureK: 3000) };
    private readonly AppConfig _config;

    public RuleEngineTests()
    {
        _config = new AppConfig { Profiles = [_standard, _gaming, _night], ActiveProfileId = _standard.Id };
    }

    private RuleOutput Resolve(string? foreground = null, ManualOverride? manual = null, bool isPro = true, DateTime? now = null) =>
        RuleEngine.Resolve(new RuleInput(_config, [MonitorA, MonitorB], now ?? Noon, foreground, manual, isPro));

    [Fact]
    public void Resolve_NoRules_AppliesActiveProfileEverywhere()
    {
        RuleOutput output = Resolve();

        Assert.Equal(_standard.Id, output.ProfileId);
        Assert.All(output.Monitors.Values, t => Assert.Equal(_standard.Color, t.Settings));
        Assert.Equal(RuleEngine.BaseContext, output.ContextKey);
    }

    [Fact]
    public void Resolve_Paused_RestoresAllMonitors()
    {
        _config.IsPaused = true;

        RuleOutput output = Resolve();

        Assert.All(output.Monitors.Values, t => Assert.Null(t.Settings));
        Assert.Equal(TargetReason.Paused, output.Reason);
    }

    [Fact]
    public void Resolve_ExcludedMonitor_IsLeftAlone()
    {
        _config.Monitors[MonitorB] = new MonitorConfig { Excluded = true };

        RuleOutput output = Resolve();

        Assert.Equal(_standard.Color, output.Monitors[MonitorA].Settings);
        Assert.Null(output.Monitors[MonitorB].Settings);
    }

    [Fact]
    public void Resolve_ForegroundAppRule_AppliesItsProfile()
    {
        _config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = _gaming.Id });

        RuleOutput output = Resolve(@"C:\Games\Counter-Strike\cs2.exe");

        Assert.Equal(_gaming.Id, output.ProfileId);
        Assert.Equal(TargetReason.AppRule, output.Reason);
        Assert.StartsWith("app:", output.ContextKey, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_AppRuleWithoutPro_IsIgnored()
    {
        _config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = _gaming.Id });

        RuleOutput output = Resolve(@"C:\Games\cs2.exe", isPro: false);

        Assert.Equal(_standard.Id, output.ProfileId);
    }

    [Fact]
    public void Resolve_DisabledAppRule_IsIgnored()
    {
        _config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = _gaming.Id, Enabled = false });

        Assert.Equal(_standard.Id, Resolve(@"C:\Games\cs2.exe").ProfileId);
    }

    [Fact]
    public void Resolve_ManualPickInSameContext_WinsOverAppRule()
    {
        _config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = _gaming.Id });
        string context = Resolve(@"C:\cs2.exe").ContextKey;

        RuleOutput output = Resolve(@"C:\cs2.exe", new ManualOverride(_night.Id, context));

        Assert.Equal(_night.Id, output.ProfileId);
        Assert.Equal(TargetReason.Manual, output.Reason);
    }

    [Fact]
    public void Resolve_ManualPickFromOtherContext_IsIgnored()
    {
        _config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = _gaming.Id });

        RuleOutput output = Resolve(@"C:\cs2.exe", new ManualOverride(_night.Id, RuleEngine.BaseContext));

        Assert.Equal(_gaming.Id, output.ProfileId);
    }

    [Fact]
    public void Resolve_MonitorOverride_OnlyWithPro()
    {
        var special = new ColorSettings(Gamma: 1.8);
        _standard.MonitorOverrides[MonitorB] = special;

        Assert.Equal(special, Resolve().Monitors[MonitorB].Settings);
        Assert.Equal(_standard.Color, Resolve(isPro: false).Monitors[MonitorB].Settings);
    }

    [Fact]
    public void Resolve_ScheduleSlot_AppliesProfileAndSchedulesNextSlot()
    {
        EnableSchedule(fadeMinutes: 0);

        RuleOutput evening = Resolve(now: new DateTime(2026, 10, 10, 22, 0, 0));
        RuleOutput earlyMorning = Resolve(now: new DateTime(2026, 10, 11, 3, 0, 0));
        RuleOutput day = Resolve(now: Noon);

        Assert.Equal(_night.Id, evening.ProfileId);
        Assert.Equal(new DateTime(2026, 10, 11, 7, 0, 0), evening.NextEvaluation);
        Assert.Equal(_night.Id, earlyMorning.ProfileId);
        Assert.Equal(_standard.Id, day.ProfileId);
        Assert.Equal(new DateTime(2026, 10, 10, 21, 0, 0), day.NextEvaluation);
    }

    [Fact]
    public void Resolve_ScheduleFade_BlendsFromPreviousSlot()
    {
        EnableSchedule(fadeMinutes: 30);

        RuleOutput halfway = Resolve(now: new DateTime(2026, 10, 10, 21, 15, 0));

        int kelvin = halfway.Monitors[MonitorA].Settings!.TemperatureK;
        Assert.InRange(kelvin, 3100, 6400);
        Assert.Equal(new DateTime(2026, 10, 10, 21, 15, 2), halfway.NextEvaluation);
    }

    [Fact]
    public void Resolve_AppRuleBeatsSchedule()
    {
        EnableSchedule(fadeMinutes: 0);
        _config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = _gaming.Id });

        RuleOutput output = Resolve(@"D:\cs2.exe", now: new DateTime(2026, 10, 10, 22, 0, 0));

        Assert.Equal(_gaming.Id, output.ProfileId);
    }

    [Theory]
    [InlineData("cs2.exe", @"C:\Games\CS2.EXE", true)]
    [InlineData("cs2.exe", @"C:\Games\cs2_launcher.exe", false)]
    [InlineData(@"C:\Games\cs2.exe", @"c:\games\cs2.exe", true)]
    [InlineData(@"C:\Games\cs2.exe", @"D:\Games\cs2.exe", false)]
    public void Matches_ByFileNameOrFullPath(string rule, string path, bool expected) =>
        Assert.Equal(expected, RuleEngine.Matches(new AppRule { Executable = rule }, path));

    private void EnableSchedule(int fadeMinutes)
    {
        _config.Schedule.Enabled = true;
        _config.Schedule.Slots.Add(new ScheduleSlot { Start = new TimeOnly(7, 0), ProfileId = _standard.Id, FadeMinutes = fadeMinutes });
        _config.Schedule.Slots.Add(new ScheduleSlot { Start = new TimeOnly(21, 0), ProfileId = _night.Id, FadeMinutes = fadeMinutes });
    }
}
