// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Model;
using DispHub.Platform;
using DispHub.Platform.Displays;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispHub.Tests.Platform;

public sealed class MonitorModesTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PutBack = TimeSpan.FromMilliseconds(250);
    private static readonly MonitorInfo Screen = new("tv", "\\\\.\\DISPLAY1", "TV", true, false, false, "TV00001");
    private static readonly Dictionary<string, ModeTarget> Game = new() { [Screen.Key] = new(RefreshChoice.Highest, true) };

    private readonly FakeBackend _backend = new();
    private readonly MonitorModes _modes;
    private volatile IReadOnlyDictionary<string, SavedMode>? _originals;

    public MonitorModesTests()
    {
        _modes = new MonitorModes(_backend, NullLogger<MonitorModes>.Instance, settle: TimeSpan.FromMilliseconds(20), putBack: PutBack);
        _modes.OriginalsChanged += (_, originals) => _originals = originals;
    }

    public void Dispose() => _modes.Dispose();

    private void Start(Dictionary<string, SavedMode>? originals = null)
    {
        _modes.Start(originals ?? []);
        _modes.Refresh([Screen]);
        WaitFor(() => _modes.Abilities.Count > 0);
    }

    [Fact]
    public void Refresh_FindsRatesAndHdr()
    {
        Start();

        ModeAbilities abilities = _modes.Abilities[Screen.Key];
        Assert.Equal([60, 120, 165], abilities.RefreshRates);
        Assert.True(abilities.Hdr);
    }

    [Fact]
    public void Apply_SwitchesAndPutsBackWhatWasThere()
    {
        Start();

        _modes.Apply(Game, byHand: true);
        // With HDR on this display tops out lower: the rate is picked after HDR switched.
        // What was there is reported just after the switch, so it can land a moment after the backend changes.
        WaitFor(() => _backend.HdrOn && _backend.Rate == 144 && _originals is { Count: > 0 });
        Assert.Equal(new SavedMode(120, false), _originals![Screen.Key]);

        _modes.Apply(new Dictionary<string, ModeTarget>(), byHand: true);
        WaitFor(() => !_backend.HdrOn && _backend.Rate == 120);
        WaitFor(() => _originals is { Count: 0 });
    }

    [Fact]
    public void HdrThatLowersTheRate_GetsTheRateBackToo()
    {
        _backend.Rate = 165;
        Start();

        _modes.Apply(new Dictionary<string, ModeTarget> { [Screen.Key] = new(RefreshChoice.Unchanged, true) }, byHand: true);
        WaitFor(() => _backend.HdrOn && _backend.Rate == 144);
        _modes.Apply(new Dictionary<string, ModeTarget>(), byHand: true);

        WaitFor(() => !_backend.HdrOn && _backend.Rate == 165);
    }

    [Fact]
    public void AQuickLookAtAnotherApp_DoesNotSwitchBack()
    {
        Start();
        _modes.Apply(Game, byHand: false);
        WaitFor(() => _backend.HdrOn && _backend.Rate == 144);
        int switches = _backend.Switches;

        _modes.Apply(new Dictionary<string, ModeTarget>(), byHand: false);
        Thread.Sleep(PutBack / 5);
        _modes.Apply(Game, byHand: false);
        Thread.Sleep(PutBack * 2);

        Assert.Equal(switches, _backend.Switches);
    }

    [Fact]
    public void LeavingForLonger_PutsTheModeBack()
    {
        Start();
        _modes.Apply(Game, byHand: true);
        WaitFor(() => _backend.HdrOn);

        _modes.Apply(new Dictionary<string, ModeTarget>(), byHand: false);

        WaitFor(() => !_backend.HdrOn && _backend.Rate == 120);
    }

    [Fact]
    public void RestoreAll_PutsBackWhatAProfileChanged()
    {
        Start();
        _modes.Apply(Game, byHand: true);
        WaitFor(() => _backend.HdrOn && _backend.Rate == 144);

        Assert.True(_modes.RestoreAll(Wait));

        Assert.False(_backend.HdrOn);
        Assert.Equal(120, _backend.Rate);
        Assert.Empty(_originals!);
    }

    [Fact]
    public void Start_PutsBackAModeThatACrashLeftChanged()
    {
        _backend.HdrOn = true;
        _backend.Rate = 60;

        Start(new Dictionary<string, SavedMode> { [Screen.Key] = new(165, false) });

        WaitFor(() => !_backend.HdrOn && _backend.Rate == 165);
        WaitFor(() => _originals is { Count: 0 });
    }

    [Fact]
    public void Requests_AfterDispose_AreIgnored()
    {
        Start();
        _modes.Dispose();

        _modes.Refresh([Screen]);
        _modes.Apply(Game, byHand: true);
        Assert.True(_modes.RestoreAll(Wait));
        Assert.False(_backend.HdrOn);
    }

    private static void WaitFor(Func<bool> condition) =>
        Assert.True(SpinWait.SpinUntil(condition, Wait), "timed out waiting for the display mode thread");

    /// <summary>A 165 Hz HDR display that only reaches 144 Hz with HDR on, starting at 120 Hz in SDR.</summary>
    private sealed class FakeBackend : IDisplayModeBackend
    {
        private volatile int _rate = 120;
        private volatile bool _hdr;
        private volatile int _switches;

        public int Rate { get => _rate; set => _rate = value; }
        public bool HdrOn { get => _hdr; set => _hdr = value; }
        public int Switches => _switches;

        public IReadOnlyList<int> RefreshRates(MonitorInfo monitor) => _hdr ? [60, 120, 144] : [60, 120, 165];

        public int? RefreshRate(MonitorInfo monitor) => _rate;

        public bool SetRefreshRate(MonitorInfo monitor, int hz)
        {
            if (!RefreshRates(monitor).Contains(hz))
                return false;
            _rate = hz;
            _switches++;
            return true;
        }

        public bool? Hdr(MonitorInfo monitor) => _hdr;

        public bool SetHdr(MonitorInfo monitor, bool enable)
        {
            _hdr = enable;
            if (enable && _rate > 144)
                _rate = 144;
            _switches++;
            return true;
        }
    }
}
