// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Platform.Displays;

namespace DispHub.Platform.Vibrance;

/// <summary>Routes each display to whichever GPU vendor drives it.</summary>
public sealed class CompositeVibranceProvider(IReadOnlyList<IVibranceProvider> providers) : IVibranceProvider
{
    public string Vendor => string.Join(" + ", providers.Select(p => p.Vendor));

    public void Refresh(IReadOnlyList<MonitorInfo> monitors)
    {
        foreach (IVibranceProvider provider in providers)
            provider.Refresh(monitors);
    }

    public VibranceRange? GetRange(string deviceName) => For(deviceName)?.GetRange(deviceName);

    public int? GetLevel(string deviceName) => For(deviceName)?.GetLevel(deviceName);

    public bool SetLevel(string deviceName, int level) => For(deviceName)?.SetLevel(deviceName, level) ?? false;

    public void Dispose()
    {
        foreach (IVibranceProvider provider in providers)
            provider.Dispose();
    }

    private IVibranceProvider? For(string deviceName) => providers.FirstOrDefault(p => p.GetRange(deviceName) is not null);
}
