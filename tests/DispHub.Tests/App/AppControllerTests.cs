// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Core.Config;
using DispHub.Core.Hotkeys;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Platform;
using DispHub.Platform.Displays;
using DispHub.Platform.Shell;
using DispHub.Platform.Store;
using DispHub.Services;
using DispHub.Tests.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispHub.Tests.App;

public sealed class AppControllerTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly FakeColors _colors = new();
    private readonly FakeHotkeys _hotkeys = new();
    private readonly FakeForeground _foreground = new();
    private readonly FakeLicenseService _license = new(startAsPro: false);
    private readonly AppController _app;

    public AppControllerTests()
    {
        _app = new AppController(new ConfigStore(_dir.Path), _colors, _license, _foreground, NullLogger<AppController>.Instance);
        _app.Start(_hotkeys);
    }

    private Profile Standard => _app.Config.Profiles[0];
    private Profile Gaming => _app.Config.Profiles[1];
    private Profile Night => _app.Config.Profiles[2];

    public void Dispose()
    {
        _app.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public void Start_AppliesActiveProfileAndRegistersHotkeys()
    {
        Assert.Equal(Standard.Color, _colors.Last!.Default);
        Assert.Equal(4, _hotkeys.Registered.Count); // three profiles + pause
    }

    [Fact]
    public void ProfileHotkey_SwitchesProfile()
    {
        _hotkeys.Press($"profile:{Night.Id}");

        Assert.Equal(Night.Id, _app.Current!.ProfileId);
        Assert.Equal(Night.Color, _colors.Last!.Default);
    }

    [Fact]
    public void ProfileHotkey_StillWorksAfterEditingTheProfile()
    {
        // Regression: v1 replaced the Profile object on save, so its hotkey silently stopped working.
        _app.UpdateProfileColor(Night.Id, new ColorSettings(TemperatureK: 3000));
        _app.RenameProfile(Night.Id, "Late night");
        _app.ActivateProfile(Standard.Id, ChangeSource.Tray);

        _hotkeys.Press($"profile:{Night.Id}");

        Assert.Equal(Night.Id, _app.Current!.ProfileId);
        Assert.Equal(3000, _colors.Last!.Default!.TemperatureK);
    }

    [Fact]
    public void OnOffHotkey_TurnsDispHubOffAndOn()
    {
        _hotkeys.Press(AppController.PauseAction);
        Assert.Null(_colors.Last!.Default);
        Assert.True(_app.Config.IsOff);
        Assert.False(_app.Config.IsPaused); // the Profiles switch is separate

        _hotkeys.Press(AppController.PauseAction);
        Assert.Equal(Standard.Color, _colors.Last!.Default);
    }

    [Fact]
    public void PickingAProfile_TurnsDispHubBackOn_AndSaysSo()
    {
        _app.ToggleOff(ChangeSource.Editor);
        int powerChanges = 0;
        _app.PowerChanged += (_, _) => powerChanges++;

        _app.ActivateProfile(Gaming.Id, ChangeSource.Tray);

        Assert.False(_app.Config.IsOff);
        Assert.True(_app.ProfilesOn);
        Assert.Equal(Gaming.Color, _colors.Last!.Default);
        // Everything that shows the power state (title bar, quick panel, tray) hears about it.
        Assert.Equal(1, powerChanges);
    }

    [Fact]
    public void ProfilesSwitch_TurnedOnWhileOff_TurnsDispHubOn()
    {
        _app.SetOn(false, ChangeSource.Editor);
        Assert.False(_app.ProfilesOn);

        _app.SetProfilesOn(true, ChangeSource.Editor);

        Assert.False(_app.Config.IsOff);
        Assert.False(_app.Config.IsPaused);
        Assert.Equal(Standard.Color, _colors.Last!.Default);
    }

    [Fact]
    public void ProfilesSwitch_TurnedOff_LeavesDispHubOn_AndPowerRemembersIt()
    {
        _app.SetProfilesOn(false, ChangeSource.Editor);
        Assert.False(_app.Config.IsOff);
        Assert.Null(_colors.Last!.Default);

        _app.ToggleOff(ChangeSource.Editor);
        _app.ToggleOff(ChangeSource.Editor);

        // Back on, profiles are still off: the power button restores what was there.
        Assert.False(_app.Config.IsOff);
        Assert.True(_app.Config.IsPaused);
        Assert.Null(_colors.Last!.Default);
    }

    [Fact]
    public void SwitchingToWhatIsAlreadySo_ChangesNothing()
    {
        int powerChanges = 0;
        _app.PowerChanged += (_, _) => powerChanges++;

        _app.SetOn(true, ChangeSource.Editor);
        _app.SetProfilesOn(true, ChangeSource.Editor);

        Assert.Equal(0, powerChanges);
    }

    [Fact]
    public void ManyQuickPresses_EndWhereTheCountSays()
    {
        for (int i = 0; i < 11; i++)
            _hotkeys.Press(AppController.PauseAction);

        Assert.True(_app.Config.IsOff);
        Assert.Null(_colors.Last!.Default);
    }

    [Fact]
    public void ActivatingWhilePaused_Resumes()
    {
        _app.SetProfilesOn(false, ChangeSource.Editor);

        _app.ActivateProfile(Gaming.Id, ChangeSource.Tray);

        Assert.False(_app.Config.IsPaused);
        Assert.Equal(Gaming.Color, _colors.Last!.Default);
    }

    [Fact]
    public void DeleteActiveProfile_FallsBackAndUnregistersItsHotkey()
    {
        Guid night = Night.Id;
        _app.ActivateProfile(night, ChangeSource.Tray);

        Assert.True(_app.DeleteProfile(night));

        Assert.Equal(Standard.Id, _app.Current!.ProfileId);
        Assert.DoesNotContain(_hotkeys.Registered.Keys, k => k.Contains(night.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void DeleteLastProfile_IsRefused()
    {
        _app.DeleteProfile(Night.Id);
        _app.DeleteProfile(Gaming.Id);

        Assert.False(_app.DeleteProfile(Standard.Id));
        Assert.Single(_app.Config.Profiles);
    }

    [Fact]
    public void Trial_HasEveryProFeature()
    {
        _app.SetTrial(DateTimeOffset.UtcNow.AddDays(3));

        Assert.True(_app.InTrial);
        Assert.True(_app.Features.HasPro);
        Assert.NotNull(_app.AddProfile());
        Assert.Equal(4, _app.Config.Profiles.Count);
    }

    [Fact]
    public void TrialEnding_KeepsExtraProfilesButOffScreen()
    {
        _app.SetTrial(DateTimeOffset.UtcNow.AddDays(3));
        Profile extra = _app.AddProfile(new ColorSettings(Brightness: 12), "Trial look")!;
        _app.SetProfileHotkey(extra.Id, HotkeyGesture.TryParse("Ctrl+Alt+4", out HotkeyGesture gesture) ? gesture : null);
        _app.ActivateProfile(extra.Id, ChangeSource.Editor);
        Assert.Equal(extra.Id, _app.Current!.ProfileId);
        Assert.Equal(5, _hotkeys.Registered.Count);

        _app.SetTrial(DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.False(_app.InTrial);
        Assert.Contains(_app.Config.Profiles, p => p.Id == extra.Id);
        Assert.Equal(Standard.Id, _app.Current!.ProfileId);
        Assert.Equal(4, _hotkeys.Registered.Count); // its shortcut no longer takes the keys
        _app.ActivateProfile(extra.Id, ChangeSource.Hotkey);
        Assert.Equal(Standard.Id, _app.Current!.ProfileId);
    }

    [Fact]
    public void FreeTier_StopsAtProfileLimit()
    {
        while (_app.AddProfile() is not null)
        {
        }

        Assert.Equal(DispHub.Core.Licensing.FeatureGate.FreeProfileLimit, _app.Config.Profiles.Count);
    }

    [Fact]
    public async Task Pro_RemovesProfileLimit()
    {
        await _license.PurchaseProAsync(0);

        for (int i = 0; i < 10; i++)
            Assert.NotNull(_app.AddProfile());
    }

    [Fact]
    public void AppRule_ByNameOnly_TakesTheProgramsPathWhenItIsInFront()
    {
        var rule = new AppRule { Executable = "cs2.exe", ProfileId = Gaming.Id };
        _app.Config.AppRules.Add(rule);
        int changes = 0;
        _app.ConfigChanged += (_, _) => changes++;

        _foreground.Switch(@"C:\Games\cs2.exe");
        _app.OnForegroundChanged();

        Assert.Equal(@"C:\Games\cs2.exe", rule.Path);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void AppRule_KeepsAPathThatStillExists()
    {
        string program = Path.Combine(_dir.Path, "cs2.exe");
        File.WriteAllText(program, "");
        var rule = new AppRule { Executable = "cs2.exe", Path = program, ProfileId = Gaming.Id };
        _app.Config.AppRules.Add(rule);

        _foreground.Switch(@"D:\Other\cs2.exe");
        _app.OnForegroundChanged();

        Assert.Equal(program, rule.Path);
    }

    [Fact]
    public void AppRule_WithAPathThatIsGone_TakesTheNewOne()
    {
        var rule = new AppRule { Executable = "cs2.exe", Path = Path.Combine(_dir.Path, "moved", "cs2.exe"), ProfileId = Gaming.Id };
        _app.Config.AppRules.Add(rule);

        _foreground.Switch(@"D:\Games\cs2.exe");
        _app.OnForegroundChanged();

        Assert.Equal(@"D:\Games\cs2.exe", rule.Path);
    }

    [Fact]
    public async Task AppRule_AppliesWhileAppIsInFrontAndRevertsAfter()
    {
        await _license.PurchaseProAsync(0);
        _app.Config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = Gaming.Id });
        _app.SettingsChanged();

        _foreground.Switch(@"C:\Games\cs2.exe");
        _app.OnForegroundChanged();
        Assert.Equal(Gaming.Id, _app.Current!.ProfileId);

        _foreground.Switch(@"C:\Windows\explorer.exe");
        _app.OnForegroundChanged();
        Assert.Equal(Standard.Id, _app.Current!.ProfileId);
    }

    [Fact]
    public async Task ManualPickDuringAppRule_LastsUntilTheAppLosesFocus()
    {
        await _license.PurchaseProAsync(0);
        _app.Config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = Gaming.Id });
        _foreground.Switch(@"C:\Games\cs2.exe");
        _app.SettingsChanged();

        _hotkeys.Press($"profile:{Night.Id}");
        Assert.Equal(Night.Id, _app.Current!.ProfileId);
        Assert.Equal(Standard.Id, _app.Config.ActiveProfileId); // the pick didn't change the everyday profile

        _foreground.Switch(@"C:\Windows\explorer.exe");
        _app.OnForegroundChanged();
        Assert.Equal(Standard.Id, _app.Current!.ProfileId);
    }

    [Fact]
    public async Task PickingAProfileWhileOff_DuringAnAppRule_ShowsThePick()
    {
        // Regression: while off every rule looked like the everyday profile, so the pick lost to the rule.
        await _license.PurchaseProAsync(0);
        _app.Config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = Gaming.Id });
        _foreground.Switch(@"C:\Games\cs2.exe");
        _app.SettingsChanged();
        _app.SetOn(false, ChangeSource.Editor);

        _app.ActivateProfile(Night.Id, ChangeSource.Editor);

        Assert.Equal(Night.Id, _app.Current!.ProfileId);
        Assert.Equal(Night.Color, _colors.Last!.Default);
        Assert.Equal(Standard.Id, _app.Config.ActiveProfileId);
    }

    [Fact]
    public async Task APickDuringAnAppRule_SurvivesTurningOffAndOn()
    {
        await _license.PurchaseProAsync(0);
        _app.Config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = Gaming.Id });
        _foreground.Switch(@"C:\Games\cs2.exe");
        _app.SettingsChanged();
        _app.ActivateProfile(Night.Id, ChangeSource.Editor);

        _app.ToggleOff(ChangeSource.Editor);
        _app.ToggleOff(ChangeSource.Editor);
        Assert.Equal(Night.Id, _app.Current!.ProfileId);

        _app.SetProfilesOn(false, ChangeSource.Editor);
        _app.SetProfilesOn(true, ChangeSource.Editor);
        Assert.Equal(Night.Id, _app.Current!.ProfileId);
    }

    [Fact]
    public async Task APickDuringTheSchedule_ComesBackAfterAnAppWithItsOwnRule()
    {
        await _license.PurchaseProAsync(0);
        _app.Config.Schedule.Enabled = true;
        _app.Config.Schedule.Slots.Add(new ScheduleSlot { Start = new TimeOnly(0, 0), ProfileId = Night.Id });
        _app.Config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = Gaming.Id });
        _app.SettingsChanged();
        Assert.Equal(Night.Id, _app.Current!.ProfileId);

        _app.ActivateProfile(Standard.Id, ChangeSource.Editor);
        _foreground.Switch(@"C:\Games\cs2.exe");
        _app.OnForegroundChanged();
        Assert.Equal(Gaming.Id, _app.Current!.ProfileId);

        _foreground.Switch(@"C:\Windows\explorer.exe");
        _app.OnForegroundChanged();
        Assert.Equal(Standard.Id, _app.Current!.ProfileId);
        Assert.Equal(TargetReason.Manual, _app.Current.Reason);
    }

    [Fact]
    public void ConflictingHotkey_IsReported()
    {
        _hotkeys.Taken.Add("Ctrl+Alt+F9");

        string? error = _app.SetProfileHotkey(Gaming.Id, HotkeyGesture.TryParse("Ctrl+Alt+F9", out var g) ? g : null);

        Assert.Equal("Used by another app", error);
    }

    [Fact]
    public void MonitorOverride_RequiresPro()
    {
        _app.UpdateProfileMonitorColor(Standard.Id, "m1", new ColorSettings(TemperatureK: 4000));

        Assert.Empty(Standard.MonitorOverrides);
    }

    [Fact]
    public async Task MonitorOverride_AppliesToThatMonitorOnly()
    {
        await _license.PurchaseProAsync(0);

        _app.UpdateProfileMonitorColor(Standard.Id, "m1", new ColorSettings(TemperatureK: 4000));

        Assert.Equal(4000, _colors.Last!.PerMonitor["m1"]!.TemperatureK);
        Assert.Equal(Standard.Color, _colors.Last.Default);

        _app.ClearMonitorOverride(Standard.Id, "m1");
        Assert.Equal(Standard.Color, _colors.Last!.PerMonitor["m1"]);
    }

    [Fact]
    public async Task ProfileMonitorBrightness_IsProAndPutBackOnExit()
    {
        using var dir = new TempDirectory();
        var brightness = new FakeBrightness();
        using var app = new AppController(new ConfigStore(dir.Path), new FakeColors(), _license, _foreground, NullLogger<AppController>.Instance, brightness: brightness);
        app.Start(new FakeHotkeys());
        Profile standard = app.Config.Profiles[0];

        app.SetProfileMonitorBrightness(standard.Id, 40);
        Assert.Null(standard.MonitorBrightness);
        Assert.Null(brightness.Last!["m1"]);

        await _license.PurchaseProAsync(0);
        app.SetProfileMonitorBrightness(standard.Id, 40);
        Assert.Equal(40, brightness.Last!["m1"]);

        app.SetProfilesOn(false, ChangeSource.Editor);
        Assert.Null(brightness.Last!["m1"]);

        // On exit m1 is put back; m2 isn't connected, so its level waits for next time.
        brightness.Report(new Dictionary<string, int> { ["m1"] = 70, ["m2"] = 50 });
        brightness.LeftAfterRestore = new Dictionary<string, int> { ["m2"] = 50 };
        app.Shutdown();
        Assert.True(brightness.Restored);
        AppConfig saved = new ConfigStore(dir.Path).Load().Config;
        Assert.Null(saved.Monitors.GetValueOrDefault("m1")?.BrightnessBeforeProfile);
        Assert.Equal(50, saved.Monitors["m2"].BrightnessBeforeProfile);
    }

    [Fact]
    public async Task ProfileDisplayModes_ArePro_AndBatteryLowersTheRateOtherwise()
    {
        using var dir = new TempDirectory();
        var modes = new FakeModes();
        using var app = new AppController(new ConfigStore(dir.Path), new FakeColors(), _license, _foreground, NullLogger<AppController>.Instance, modes: modes);
        app.Start(new FakeHotkeys());
        Profile standard = app.Config.Profiles[0];

        app.SetProfileDisplayMode(standard.Id, RefreshChoice.Highest, hdr: true);
        Assert.Equal(RefreshChoice.Unchanged, standard.RefreshRate);
        Assert.Empty(modes.Last!);

        await _license.PurchaseProAsync(0);
        app.SetProfileDisplayMode(standard.Id, RefreshChoice.Highest, hdr: true);
        Assert.Equal(new ModeTarget(RefreshChoice.Highest, true), modes.Last!["m1"]);
        Assert.True(modes.LastByHand);

        app.SetProfileDisplayMode(standard.Id, RefreshChoice.Unchanged, hdr: null);
        app.SetLowRefreshOnBattery("m1", on: true);
        Assert.Equal(default, modes.Last!["m1"]);
        app.OnPowerSourceChanged(onBattery: true);
        Assert.Equal(RefreshChoice.Lowest, modes.Last!["m1"].Refresh);
        Assert.False(modes.LastByHand); // waits a moment, like any automatic change

        app.SetOn(false, ChangeSource.Tray);
        Assert.Empty(modes.Last!);

        app.Shutdown();
        Assert.True(modes.Restored);
    }

    [Fact]
    public async Task Duplicate_KeepsEverythingButNameAndHotkey()
    {
        await _license.PurchaseProAsync(0);
        _app.UpdateProfileMonitorColor(Gaming.Id, "m1", new ColorSettings(TemperatureK: 4000));
        _app.SetProfileMonitorBrightness(Gaming.Id, 80);

        Profile copy = _app.DuplicateProfile(Gaming.Id)!;

        Assert.Equal(Gaming.Color, copy.Color);
        Assert.Equal(4000, copy.MonitorOverrides["m1"].TemperatureK);
        Assert.Equal(80, copy.MonitorBrightness);
        Assert.NotEqual(Gaming.Name, copy.Name);
        Assert.Null(copy.Hotkey);
    }

    [Fact]
    public void Shutdown_SavesChangesAndRestoresColours()
    {
        _app.RenameProfile(Gaming.Id, "Competitive");

        _app.Shutdown();

        Assert.True(_colors.Restored);
        AppConfig saved = new ConfigStore(_dir.Path).Load().Config;
        Assert.Equal("Competitive", saved.Profiles[1].Name);
    }

    internal sealed class FakeColors : IColorApplier
    {
        public MonitorTargets? Last { get; private set; }
        public bool Restored { get; private set; }

        public IReadOnlyList<MonitorStatus> Monitors { get; } =
            [new MonitorStatus(new MonitorInfo("m1", "\\\\.\\DISPLAY1", "Test", true, false, false, ""), MonitorColorState.Original, false)];

        public event EventHandler? StatusChanged
        {
            add { }
            remove { }
        }

        public event EventHandler? ExternalResetDetected
        {
            add { }
            remove { }
        }

        public void Start()
        {
        }

        public void Apply(MonitorTargets targets) => Last = targets;

        public void RefreshDisplays()
        {
        }

        public void Reapply()
        {
        }

        public bool RestoreAll(TimeSpan timeout) => Restored = true;

        public void Dispose()
        {
        }
    }

    internal sealed class FakeBrightness : IMonitorBrightness
    {
        public IReadOnlyDictionary<string, int?>? Last { get; private set; }
        public bool Restored { get; private set; }

        /// <summary>What <see cref="RestoreAll"/> reports is still to put back, like a monitor that isn't connected.</summary>
        public IReadOnlyDictionary<string, int> LeftAfterRestore { get; set; } = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> Levels { get; } = new Dictionary<string, int> { ["m1"] = 60 };

        public event EventHandler<IReadOnlyDictionary<string, int>>? OriginalsChanged;

        /// <summary>Says which levels are waiting to be put back, as the worker does after a change.</summary>
        public void Report(IReadOnlyDictionary<string, int> originals) => OriginalsChanged?.Invoke(this, originals);

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public void Start(IReadOnlyDictionary<string, int> originals)
        {
        }

        public void Refresh(IReadOnlyList<MonitorInfo> monitors)
        {
        }

        public void Apply(IReadOnlyDictionary<string, int?> targets) => Last = targets;

        public void SetLevel(string monitorKey, int percent)
        {
        }

        public void Reread()
        {
        }

        public bool RestoreAll(TimeSpan timeout)
        {
            Report(LeftAfterRestore);
            return Restored = true;
        }

        public void Dispose()
        {
        }
    }

    internal sealed class FakeModes : IMonitorModes
    {
        public IReadOnlyDictionary<string, ModeTarget>? Last { get; private set; }
        public bool LastByHand { get; private set; }
        public bool Restored { get; private set; }

        public IReadOnlyDictionary<string, ModeAbilities> Abilities { get; } =
            new Dictionary<string, ModeAbilities> { ["m1"] = new([60, 120], true) };

        public event EventHandler<IReadOnlyDictionary<string, SavedMode>>? OriginalsChanged
        {
            add { }
            remove { }
        }

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public void Start(IReadOnlyDictionary<string, SavedMode> originals)
        {
        }

        public void Refresh(IReadOnlyList<MonitorInfo> monitors)
        {
        }

        public void Apply(IReadOnlyDictionary<string, ModeTarget> targets, bool byHand) => (Last, LastByHand) = (targets, byHand);

        public bool RestoreAll(TimeSpan timeout) => Restored = true;

        public void Dispose()
        {
        }
    }

    internal sealed class FakeHotkeys : IHotkeyRegistrar
    {
        public Dictionary<string, HotkeyGesture> Registered { get; } = [];
        public HashSet<string> Taken { get; } = [];

        public event EventHandler<string>? Pressed;

        public IReadOnlyDictionary<string, string> Sync(IReadOnlyDictionary<string, HotkeyGesture> desired)
        {
            Registered.Clear();
            var failures = new Dictionary<string, string>();
            foreach ((string action, HotkeyGesture gesture) in desired)
            {
                if (Taken.Contains(gesture.ToString()))
                    failures[action] = "Used by another app";
                else
                    Registered[action] = gesture;
            }
            return failures;
        }

        public void SuspendAll()
        {
        }

        public void ResumeAll()
        {
        }

        public void Press(string action) => Pressed?.Invoke(this, action);
    }

    internal sealed class FakeForeground : IForegroundWatcher
    {
        public string? Current { get; private set; }

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public void Switch(string path) => Current = path;

        public void Start()
        {
        }

        public void Dispose()
        {
        }
    }
}
