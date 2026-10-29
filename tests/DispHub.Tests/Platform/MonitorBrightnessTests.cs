// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using DispHub.Platform;
using DispHub.Platform.Displays;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispHub.Tests.Platform;

public sealed class MonitorBrightnessTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private static readonly MonitorInfo External = new("ext", "\\\\.\\DISPLAY2", "External", false, false, false, "EXT0001");
    private static readonly MonitorInfo Laptop = new("lap", "\\\\.\\DISPLAY1", "Laptop", true, true, false, "LAP0001");

    private readonly FakeHardware _hardware = new();
    private readonly MonitorBrightness _brightness;
    private volatile IReadOnlyDictionary<string, int>? _originals;

    public MonitorBrightnessTests()
    {
        _hardware.Levels[External.DeviceName] = 60;
        _hardware.Levels[Laptop.DeviceName] = 80;
        _brightness = new MonitorBrightness(_hardware, NullLogger<MonitorBrightness>.Instance);
        _brightness.OriginalsChanged += (_, originals) => _originals = originals;
    }

    public void Dispose() => _brightness.Dispose();

    private int ExternalLevel => _hardware.Levels[External.DeviceName];

    private void Start(Dictionary<string, int>? originals = null)
    {
        _brightness.Start(originals ?? []);
        _brightness.Refresh([External, Laptop]);
        WaitFor(() => _brightness.Levels.Count > 0);
    }

    [Fact]
    public void Refresh_FindsEveryScreenThatAnswers_LaptopScreensToo()
    {
        Start();

        Assert.Equal(60, _brightness.Levels[External.Key]);
        Assert.Equal(80, _brightness.Levels[Laptop.Key]);
    }

    [Fact]
    public void Refresh_LeavesOutScreensThatDoNotAnswer()
    {
        _hardware.Levels.TryRemove(Laptop.DeviceName, out _);

        Start();

        Assert.False(_brightness.Levels.ContainsKey(Laptop.Key));
    }

    [Fact]
    public void Apply_KeepsTheLevelFromBeforeAndPutsItBack()
    {
        Start();

        _brightness.Apply(new Dictionary<string, int?> { [External.Key] = 30 });
        WaitFor(() => ExternalLevel == 30);
        _brightness.Apply(new Dictionary<string, int?> { [External.Key] = 40 });
        WaitFor(() => ExternalLevel == 40);
        Assert.Equal(60, _originals![External.Key]);

        _brightness.Apply(new Dictionary<string, int?> { [External.Key] = null });
        WaitFor(() => ExternalLevel == 60);
        WaitFor(() => _originals is { Count: 0 });
        Assert.Equal(80, _hardware.Levels[Laptop.DeviceName]);
    }

    [Fact]
    public void RestoreAll_PutsBackWhatAProfileChanged()
    {
        Start();
        _brightness.Apply(new Dictionary<string, int?> { [External.Key] = 30 });
        WaitFor(() => ExternalLevel == 30);

        Assert.True(_brightness.RestoreAll(Wait));

        Assert.Equal(60, ExternalLevel);
        Assert.Empty(_originals!);
    }

    [Fact]
    public void Start_PutsBackALevelThatACrashLeftChanged()
    {
        _hardware.Levels[External.DeviceName] = 30;

        _brightness.Start(new Dictionary<string, int> { [External.Key] = 75 });
        _brightness.Refresh([External]);

        WaitFor(() => ExternalLevel == 75);
        WaitFor(() => _originals is { Count: 0 });
    }

    [Fact]
    public void SetLevel_ChangesTheMonitorLikeItsButtons()
    {
        Start();

        _brightness.SetLevel(External.Key, 45);

        WaitFor(() => ExternalLevel == 45 && _brightness.Levels[External.Key] == 45);
        Assert.Null(_originals); // nothing to put back: the change was by hand
    }

    private static void WaitFor(Func<bool> condition) =>
        Assert.True(SpinWait.SpinUntil(condition, Wait), "timed out waiting for the brightness thread");

    private sealed class FakeHardware : IHardwareBrightness
    {
        public ConcurrentDictionary<string, int> Levels { get; } = new();

        public HardwareLevel? ReadLevel(MonitorInfo monitor) =>
            Levels.TryGetValue(monitor.DeviceName, out int level) ? new HardwareLevel(level, 100) : null;

        public bool WriteLevel(MonitorInfo monitor, int percent)
        {
            if (!Levels.ContainsKey(monitor.DeviceName))
                return false;
            Levels[monitor.DeviceName] = percent;
            return true;
        }
    }
}
