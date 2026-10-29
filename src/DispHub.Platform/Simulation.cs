// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Platform.Displays;
using DispHub.Platform.Gamma;
using DispHub.Platform.Vibrance;

namespace DispHub.Platform;

/// <summary>
/// A second, external monitor for trying DispHub without one (--simulate-monitor): it answers over DDC/CI, offers
/// 60, 100 and 144 Hz (120 with HDR on) and does HDR. Its state lives here; nothing reaches a real device for it.
/// </summary>
public sealed class SimulatedMonitor
{
    private readonly Lock _gate = new();
    private int _level = 70;
    private int _refreshHz = 144;
    private bool _hdr;
    private GammaRamp _ramp = GammaRamp.Identity();

    public const string DeviceName = @"\\.\SIMULATED1";

    public MonitorInfo Info
    {
        get
        {
            lock (_gate)
                return new MonitorInfo("simulated-monitor", DeviceName, "Simulated monitor", false, false, _hdr, "SIM0001");
        }
    }

    public static bool Is(string deviceName) => string.Equals(deviceName, DeviceName, StringComparison.OrdinalIgnoreCase);

    public int Level
    {
        get { lock (_gate) return _level; }
        set { lock (_gate) _level = Math.Clamp(value, 0, 100); }
    }

    public int RefreshHz
    {
        get { lock (_gate) return _refreshHz; }
        set { lock (_gate) _refreshHz = value; }
    }

    public bool Hdr
    {
        get { lock (_gate) return _hdr; }
        set
        {
            lock (_gate)
            {
                _hdr = value;
                // As on real HDR monitors, the top rate is lower with HDR on.
                if (_hdr && _refreshHz > 120)
                    _refreshHz = 120;
            }
        }
    }

    public IReadOnlyList<int> RefreshRates => Hdr ? [60, 100, 120] : [60, 100, 144];

    public GammaRamp Ramp
    {
        get { lock (_gate) return _ramp; }
        set { lock (_gate) _ramp = value; }
    }
}

/// <summary>The real displays plus the simulated one.</summary>
public sealed class SimulatedDisplayBackend(IDisplayBackend real, SimulatedMonitor monitor) : IDisplayBackend
{
    public IVibranceProvider? Vibrance => real.Vibrance;

    public void Initialize() => real.Initialize();

    public IReadOnlyList<MonitorInfo> GetMonitors() => [.. real.GetMonitors(), monitor.Info];

    public GammaRamp? GetRamp(string deviceName) => SimulatedMonitor.Is(deviceName) ? monitor.Ramp : real.GetRamp(deviceName);

    public GammaResult SetRamp(string deviceName, GammaRamp ramp)
    {
        if (!SimulatedMonitor.Is(deviceName))
            return real.SetRamp(deviceName, ramp);
        monitor.Ramp = ramp;
        return GammaResult.Applied;
    }

    public RampLimits GetRampLimits() => real.GetRampLimits();

    public void Dispose() => real.Dispose();
}

/// <summary>Real screens' brightness, plus the simulated monitor's.</summary>
public sealed class SimulatedBrightness(IHardwareBrightness real, SimulatedMonitor simulated) : IHardwareBrightness
{
    public HardwareLevel? ReadLevel(MonitorInfo monitor) =>
        SimulatedMonitor.Is(monitor.DeviceName) ? new HardwareLevel(simulated.Level, 100) : real.ReadLevel(monitor);

    public bool WriteLevel(MonitorInfo monitor, int percent)
    {
        if (!SimulatedMonitor.Is(monitor.DeviceName))
            return real.WriteLevel(monitor, percent);
        simulated.Level = percent;
        return true;
    }

    public IDisposable? WatchChanges(Action changed) => real.WatchChanges(changed);
}

/// <summary>Real refresh rates and HDR, plus the simulated monitor's.</summary>
public sealed class SimulatedModes(IDisplayModeBackend real, SimulatedMonitor simulated) : IDisplayModeBackend
{
    public IReadOnlyList<int> RefreshRates(MonitorInfo monitor) =>
        SimulatedMonitor.Is(monitor.DeviceName) ? simulated.RefreshRates : real.RefreshRates(monitor);

    public int? RefreshRate(MonitorInfo monitor) =>
        SimulatedMonitor.Is(monitor.DeviceName) ? simulated.RefreshHz : real.RefreshRate(monitor);

    public bool SetRefreshRate(MonitorInfo monitor, int hz)
    {
        if (!SimulatedMonitor.Is(monitor.DeviceName))
            return real.SetRefreshRate(monitor, hz);
        if (!simulated.RefreshRates.Contains(hz))
            return false;
        simulated.RefreshHz = hz;
        return true;
    }

    public bool? Hdr(MonitorInfo monitor) => SimulatedMonitor.Is(monitor.DeviceName) ? simulated.Hdr : real.Hdr(monitor);

    public bool SetHdr(MonitorInfo monitor, bool enable)
    {
        if (!SimulatedMonitor.Is(monitor.DeviceName))
            return real.SetHdr(monitor, enable);
        simulated.Hdr = enable;
        return true;
    }
}
