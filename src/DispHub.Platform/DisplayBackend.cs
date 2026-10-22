// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Platform.Displays;
using DispHub.Platform.Gamma;
using DispHub.Platform.Vibrance;

namespace DispHub.Platform;

/// <summary>The OS calls <see cref="ColorApplier"/> needs. Exists so its logic can be tested without real monitors.</summary>
public interface IDisplayBackend : IDisposable
{
    /// <summary>Called once on the colour thread before anything else.</summary>
    void Initialize();

    IReadOnlyList<MonitorInfo> GetMonitors();

    GammaRamp? GetRamp(string deviceName);

    GammaResult SetRamp(string deviceName, GammaRamp ramp);

    /// <summary>How far Windows lets a ramp deviate from identity right now.</summary>
    RampLimits GetRampLimits();

    /// <summary>Null when no supported GPU vendor library is present.</summary>
    IVibranceProvider? Vibrance { get; }
}

public sealed class WindowsDisplayBackend : IDisplayBackend
{
    public IVibranceProvider? Vibrance { get; private set; }

    public void Initialize()
    {
        var providers = new List<IVibranceProvider>();
        if (NvidiaVibranceProvider.TryCreate() is { } nvidia)
            providers.Add(nvidia);
        if (IntelVibranceProvider.TryCreate() is { } intel)
            providers.Add(intel);
        if (AmdVibranceProvider.TryCreate() is { } amd)
            providers.Add(amd);
        Vibrance = providers.Count == 0 ? null : new CompositeVibranceProvider(providers);
    }

    public IReadOnlyList<MonitorInfo> GetMonitors() => DisplayEnumerator.GetMonitors();

    public GammaRamp? GetRamp(string deviceName) => GdiGamma.TryGet(deviceName);

    public GammaResult SetRamp(string deviceName, GammaRamp ramp) => GdiGamma.Set(deviceName, ramp);

    public RampLimits GetRampLimits() => RampLimits.ForGammaRange(GammaRange.Read());

    public void Dispose() => Vibrance?.Dispose();
}
