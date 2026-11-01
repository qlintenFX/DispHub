// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Core.Config;
using DispHub.Platform;
using DispHub.Platform.Displays;
using DispHub.Platform.Gamma;
using DispHub.Platform.Vibrance;
using DispHub.Tests.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispHub.Tests.Platform;

public sealed class ColorApplierTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private static readonly ColorSettings Warm = new(TemperatureK: 4000);

    private readonly TempDirectory _dir = new();
    private readonly FakeBackend _backend = new();
    private readonly ColorApplier _applier;

    public ColorApplierTests()
    {
        _applier = new ColorApplier(_backend, JournalPath, NullLogger<ColorApplier>.Instance, idleTime: () => TimeSpan.Zero);
    }

    private string JournalPath => Path.Combine(_dir.Path, "restore.json");

    public void Dispose()
    {
        _applier.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public void Apply_FirstChange_JournalsOriginalBeforeSetting()
    {
        _backend.Ramps["\\\\.\\DISPLAY1"] = Calibrated();
        _applier.Start();

        Apply(Warm);
        WaitFor(() => _backend.SetCalls > 0);

        RestoreJournalData? journal = JsonFiles.ReadJournal(JournalPath);
        Assert.NotNull(journal);
        GammaRamp? original = GammaRamp.FromBase64(journal.Monitors[FakeBackend.MonitorKey].Ramp);
        Assert.True(Calibrated().ApproximatelyEquals(original!, 0));
    }

    [Fact]
    public void RestoreAll_PutsBackOriginalAndDeletesJournal()
    {
        _backend.Ramps["\\\\.\\DISPLAY1"] = Calibrated();
        _applier.Start();
        Apply(Warm);
        WaitFor(() => _backend.SetCalls > 0);

        Assert.True(_applier.RestoreAll(Wait));

        Assert.True(Calibrated().ApproximatelyEquals(_backend.Ramps["\\\\.\\DISPLAY1"], 0));
        Assert.False(File.Exists(JournalPath));
    }

    [Fact]
    public void Start_WithJournalFromCrashedSession_RestoresThoseOriginals()
    {
        var journal = new RestoreJournalData();
        journal.Monitors[FakeBackend.MonitorKey] = new RestoreEntry { DeviceName = "\\\\.\\DISPLAY1", Ramp = Calibrated().ToBase64() };
        JsonFiles.WriteJournal(JournalPath, journal);
        _backend.Ramps["\\\\.\\DISPLAY1"] = RampBuilder.Build(new ColorSettings(TemperatureK: 3000)); // tint left behind
        _applier.Start();

        Apply(Warm);
        WaitFor(() => _backend.SetCalls > 0);
        _applier.RestoreAll(Wait);

        Assert.True(Calibrated().ApproximatelyEquals(_backend.Ramps["\\\\.\\DISPLAY1"], 0));
    }

    [Fact]
    public void Apply_SameSettingsTwice_SetsRampOnce()
    {
        _applier.Start();
        Apply(Warm);
        WaitFor(() => _backend.SetCalls == 1);

        Apply(Warm);
        _applier.RestoreAll(Wait); // barrier: the colour thread has processed everything before this

        Assert.Equal(2, _backend.SetCalls); // the warm ramp once, then the restore
    }

    [Fact]
    public void Apply_RampOutsideWindowsLimits_IsFittedAndReportedAsLimited()
    {
        _applier.Start();

        Apply(new ColorSettings(TemperatureK: 2000));
        WaitFor(() => _applier.Monitors.Count == 1 && _applier.Monitors[0].State != MonitorColorState.Original);

        Assert.Equal(MonitorColorState.Limited, _applier.Monitors[0].State);
        Assert.True(RampLimits.Default.Accepts(_backend.Ramps["\\\\.\\DISPLAY1"]));
    }

    [Fact]
    public void Apply_HdrMonitor_LeavesGammaAlone()
    {
        _backend.Hdr = true;
        _applier.Start();

        Apply(Warm);
        WaitFor(() => _applier.Monitors.Count == 1 && _applier.Monitors[0].State == MonitorColorState.Hdr);

        Assert.Equal(0, _backend.SetCalls);
    }

    [Fact]
    public void Vibrance_OriginalLevelIsRestored()
    {
        _backend.Vibrance = new FakeVibrance { Level = 12 };
        _applier.Start();

        Apply(new ColorSettings(Vibrance: 100));
        WaitFor(() => _backend.Vibrance.Level == 63);
        _applier.RestoreAll(Wait);

        Assert.Equal(12, _backend.Vibrance.Level);
    }

    [Fact]
    public void Apply_RampResetJustAfter_IsPutBackQuickly()
    {
        _applier.Start();
        Apply(Warm);
        WaitFor(() => _backend.SetCalls == 1);

        // A game starting up puts the plain ramp back.
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _backend.SetFromOutside(GammaRamp.Identity());
        WaitFor(() => _backend.SetCalls == 2);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"took {clock.Elapsed}");
        Assert.True(RampBuilder.Build(Warm).ApproximatelyEquals(_backend.Ramps["\\\\.\\DISPLAY1"], 0));
    }

    [Fact]
    public void IdleDimming_IsLeftAlone_AndTheProfileComesBackAtOnceOnInput()
    {
        long idleMs = 0;
        using var applier = new ColorApplier(
            _backend, Path.Combine(_dir.Path, "idle.json"), NullLogger<ColorApplier>.Instance, idleTime: () => TimeSpan.FromMilliseconds(Interlocked.Read(ref idleMs)));
        applier.Start();
        applier.Apply(new MonitorTargets(new Dictionary<string, ColorSettings?>(), Warm));
        WaitFor(() => _backend.SetCalls == 1);

        // Minutes without input: the laptop dims by writing a plain ramp at 60 % (and again every second).
        Interlocked.Exchange(ref idleMs, 200_000);
        _backend.SetFromOutside(GammaRamp.Identity().Scaled(0.6));
        Thread.Sleep(1500);
        Assert.Equal(1, _backend.SetCalls); // no fight, no flicker

        // The mouse moves: the profile is back straight away, and again right after the laptop's own undim.
        Interlocked.Exchange(ref idleMs, 0);
        WaitFor(() => _backend.SetCalls == 2);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _backend.SetFromOutside(GammaRamp.Identity());
        WaitFor(() => _backend.SetCalls == 3);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(300), $"took {clock.Elapsed}");
        Assert.True(RampBuilder.Build(Warm).ApproximatelyEquals(_backend.Ramps["\\\\.\\DISPLAY1"], 0));
    }

    [Fact]
    public void DimFactor_RecognisesAPlainDimmedRamp()
    {
        Assert.Equal(0.6, GammaRamp.Identity().Scaled(0.6).DimFactor()!.Value, 2);
        Assert.Null(GammaRamp.Identity().DimFactor()); // not dimmed
        Assert.Null(RampBuilder.Build(Warm).Scaled(0.6).DimFactor()); // coloured: not a plain dimmer
    }

    [Fact]
    public void Requests_AfterDispose_AreIgnored()
    {
        _applier.Start();
        _applier.Dispose();

        // A window closing during shut-down still asks for an update.
        Apply(Warm);
        _applier.RefreshDisplays();
        _applier.Reapply();
        Assert.True(_applier.RestoreAll(Wait));
    }

    private void Apply(ColorSettings settings) =>
        _applier.Apply(new MonitorTargets(new Dictionary<string, ColorSettings?>(), settings));

    private static void WaitFor(Func<bool> condition) =>
        Assert.True(SpinWait.SpinUntil(condition, Wait), "timed out waiting for the colour thread");

    private static GammaRamp Calibrated() => RampBuilder.Build(new ColorSettings(Gamma: 1.05));

    private sealed class FakeBackend : IDisplayBackend
    {
        public const string MonitorKey = "\\\\?\\display#test";
        private int _setCalls;

        public Dictionary<string, GammaRamp> Ramps { get; } = new() { ["\\\\.\\DISPLAY1"] = GammaRamp.Identity() };
        public bool Hdr { get; set; }
        public int SetCalls => Volatile.Read(ref _setCalls);
        public FakeVibrance? Vibrance { get; set; }
        IVibranceProvider? IDisplayBackend.Vibrance => Vibrance;

        public void Initialize()
        {
        }

        public IReadOnlyList<MonitorInfo> GetMonitors() =>
            [new MonitorInfo(MonitorKey, "\\\\.\\DISPLAY1", "Test", true, false, Hdr, "TST0001")];

        public GammaRamp? GetRamp(string deviceName)
        {
            lock (Ramps)
                return Ramps.TryGetValue(deviceName, out GammaRamp? ramp) ? ramp.Clone() : null;
        }

        public GammaResult SetRamp(string deviceName, GammaRamp ramp)
        {
            lock (Ramps)
                Ramps[deviceName] = ramp.Clone();
            Interlocked.Increment(ref _setCalls);
            return RampLimits.Default.Accepts(ramp) ? GammaResult.Applied : GammaResult.Failed;
        }

        /// <summary>Another program changes the ramp.</summary>
        public void SetFromOutside(GammaRamp ramp)
        {
            lock (Ramps)
                Ramps["\\\\.\\DISPLAY1"] = ramp;
        }

        public RampLimits GetRampLimits() => RampLimits.Default;

        public void Dispose()
        {
        }
    }

    private sealed class FakeVibrance : IVibranceProvider
    {
        private int _level;

        public int Level
        {
            get => Volatile.Read(ref _level);
            set => Volatile.Write(ref _level, value);
        }

        public string Vendor => "Test";

        public void Refresh(IReadOnlyList<MonitorInfo> monitors)
        {
        }

        public VibranceRange? GetRange(string deviceName) => new VibranceRange(0, 63, 0);

        public int? GetLevel(string deviceName) => Level;

        public bool SetLevel(string deviceName, int level)
        {
            Level = level;
            return true;
        }

        public void Dispose()
        {
        }
    }
}
