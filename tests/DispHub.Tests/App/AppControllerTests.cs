using DispHub.Core.Color;
using DispHub.Core.Config;
using DispHub.Core.Hotkeys;
using DispHub.Core.Model;
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
    public void PickingAProfile_TurnsDispHubBackOn()
    {
        _app.ToggleOff(ChangeSource.Editor);

        _app.ActivateProfile(Gaming.Id, ChangeSource.Tray);

        Assert.False(_app.Config.IsOff);
        Assert.Equal(Gaming.Color, _colors.Last!.Default);
    }

    [Fact]
    public void ActivatingWhilePaused_Resumes()
    {
        _app.TogglePause(ChangeSource.Editor);

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
    public void Shutdown_SavesChangesAndRestoresColours()
    {
        _app.RenameProfile(Gaming.Id, "Competitive");

        _app.Shutdown();

        Assert.True(_colors.Restored);
        AppConfig saved = new ConfigStore(_dir.Path).Load().Config;
        Assert.Equal("Competitive", saved.Profiles[1].Name);
    }

    private sealed class FakeColors : IColorApplier
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

    private sealed class FakeHotkeys : IHotkeyRegistrar
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

    private sealed class FakeForeground : IForegroundWatcher
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
