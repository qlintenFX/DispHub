// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Platform.Displays;

namespace DispHub.Platform.Vibrance;

/// <summary>
/// Saturation on displays driven by an AMD GPU, through AMD's Display Library. Beta: built from AMD's documented API
/// but not yet confirmed on every AMD driver, so it only activates when the driver reports a saturation range.
/// </summary>
public sealed class AmdVibranceProvider : IVibranceProvider
{
    private readonly AmdAdl _adl;
    private Dictionary<string, AmdAdl.Display> _displays = new(StringComparer.OrdinalIgnoreCase);

    private AmdVibranceProvider(AmdAdl adl) => _adl = adl;

    public string Vendor => "AMD";

    public static AmdVibranceProvider? TryCreate()
    {
        AmdAdl? adl = AmdAdl.TryLoad();
        return adl is null ? null : new AmdVibranceProvider(adl);
    }

    public void Refresh(IReadOnlyList<MonitorInfo> monitors)
    {
        var displays = new Dictionary<string, AmdAdl.Display>(StringComparer.OrdinalIgnoreCase);
        foreach (AmdAdl.Display display in _adl.EnumerateDisplays())
            displays.TryAdd(display.DeviceName, display);
        _displays = displays;
    }

    public VibranceRange? GetRange(string deviceName) =>
        _displays.TryGetValue(deviceName, out AmdAdl.Display d) ? new VibranceRange(d.Min, d.Max, d.Default) : null;

    public int? GetLevel(string deviceName) =>
        _displays.TryGetValue(deviceName, out AmdAdl.Display d) ? _adl.GetSaturation(d) : null;

    public bool SetLevel(string deviceName, int level) =>
        _displays.TryGetValue(deviceName, out AmdAdl.Display d) && _adl.SetSaturation(d, Math.Clamp(level, d.Min, d.Max));

    public void Dispose() => _adl.Dispose();
}
