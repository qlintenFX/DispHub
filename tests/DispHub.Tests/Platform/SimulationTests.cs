// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Core.Model;
using DispHub.Platform;
using DispHub.Platform.Displays;
using DispHub.Platform.Gamma;
using DispHub.Platform.Vibrance;

namespace DispHub.Tests.Platform;

public sealed class SimulationTests
{
    private static readonly MonitorInfo Real = new("real", "\\\\.\\DISPLAY1", "Laptop", true, true, false, "LAP0001");

    private readonly SimulatedMonitor _simulated = new();

    [Fact]
    public void Displays_AreTheRealOnesPlusTheSimulatedOne()
    {
        var backend = new SimulatedDisplayBackend(new RealDisplays(), _simulated);

        Assert.Equal(["real", "simulated-monitor"], backend.GetMonitors().Select(m => m.Key));
        Assert.False(backend.GetMonitors()[1].IsInternal);
    }

    [Fact]
    public void Ramps_ForTheSimulatedMonitor_StayInDispHub()
    {
        var real = new RealDisplays();
        var backend = new SimulatedDisplayBackend(real, _simulated);
        GammaRamp warm = RampBuilder.Build(new ColorSettings(TemperatureK: 4500));

        Assert.Equal(GammaResult.Applied, backend.SetRamp(SimulatedMonitor.DeviceName, warm));
        Assert.Same(warm, backend.GetRamp(SimulatedMonitor.DeviceName));
        Assert.Equal(0, real.Calls);

        backend.SetRamp(Real.DeviceName, warm);
        Assert.Equal(1, real.Calls);
    }

    [Fact]
    public void Brightness_AnswersOverDdcCi()
    {
        var brightness = new SimulatedBrightness(new NoDdcCi(), _simulated);

        Assert.Equal(new HardwareLevel(70, 100), brightness.ReadLevel(SimulatedMonitor.DeviceName));
        Assert.True(brightness.WriteLevel(SimulatedMonitor.DeviceName, 30));
        Assert.Equal(30, brightness.ReadLevel(SimulatedMonitor.DeviceName)?.Current);
        Assert.Null(brightness.ReadLevel(Real.DeviceName));
    }

    [Fact]
    public void Modes_LowerTheTopRateWithHdr_AsRealMonitorsDo()
    {
        var modes = new SimulatedModes(new FixedModes(), _simulated);
        MonitorInfo monitor = _simulated.Info;

        Assert.Equal([60, 100, 144], modes.RefreshRates(monitor));
        Assert.False(modes.SetRefreshRate(monitor, 165));
        Assert.True(modes.SetHdr(monitor, true));
        Assert.Equal(true, modes.Hdr(monitor));
        Assert.Equal(120, modes.RefreshRate(monitor));
        Assert.True(_simulated.Info.IsHdr);
        Assert.Equal([60], modes.RefreshRates(Real));
    }

    private sealed class RealDisplays : IDisplayBackend
    {
        public int Calls { get; private set; }

        public IVibranceProvider? Vibrance => null;

        public void Initialize()
        {
        }

        public IReadOnlyList<MonitorInfo> GetMonitors() => [Real];

        public GammaRamp? GetRamp(string deviceName)
        {
            Calls++;
            return GammaRamp.Identity();
        }

        public GammaResult SetRamp(string deviceName, GammaRamp ramp)
        {
            Calls++;
            return GammaResult.Applied;
        }

        public RampLimits GetRampLimits() => RampLimits.Default;

        public void Dispose()
        {
        }
    }

    private sealed class NoDdcCi : IHardwareBrightness
    {
        public HardwareLevel? ReadLevel(string deviceName) => null;

        public bool WriteLevel(string deviceName, int percent) => false;
    }

    private sealed class FixedModes : IDisplayModeBackend
    {
        public IReadOnlyList<int> RefreshRates(MonitorInfo monitor) => [60];

        public int? RefreshRate(MonitorInfo monitor) => 60;

        public bool SetRefreshRate(MonitorInfo monitor, int hz) => hz == 60;

        public bool? Hdr(MonitorInfo monitor) => null;

        public bool SetHdr(MonitorInfo monitor, bool enable) => false;
    }
}
