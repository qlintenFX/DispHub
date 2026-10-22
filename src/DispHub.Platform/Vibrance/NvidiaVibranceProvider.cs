// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Platform.Displays;

namespace DispHub.Platform.Vibrance;

/// <summary>
/// NVIDIA digital vibrance for every display wired to an NVIDIA GPU. On Optimus laptops the built-in panel is
/// usually driven by the integrated GPU, so only external ports wired to the NVIDIA GPU show up here.
/// </summary>
public sealed class NvidiaVibranceProvider : IVibranceProvider
{
    private readonly NvApi _api;
    private Dictionary<string, (nint Handle, VibranceRange Range, bool Extended)> _displays = new(StringComparer.OrdinalIgnoreCase);

    private NvidiaVibranceProvider(NvApi api)
    {
        _api = api;
        Refresh([]);
    }

    public string Vendor => "NVIDIA";

    public static NvidiaVibranceProvider? TryCreate()
    {
        NvApi? api = NvApi.TryLoad();
        return api is null ? null : new NvidiaVibranceProvider(api);
    }

    public void Refresh(IReadOnlyList<MonitorInfo> monitors)
    {
        var displays = new Dictionary<string, (nint, VibranceRange, bool)>(StringComparer.OrdinalIgnoreCase);
        foreach ((nint handle, string deviceName) in _api.EnumerateDisplays())
        {
            if (_api.TryGetDvc(handle, out _, out int min, out int max, out int defaultLevel, out bool extended))
            {
                var range = new VibranceRange(min, max, Math.Clamp(defaultLevel, min, max));
                displays.TryAdd(deviceName, (handle, range, extended));
            }
        }
        _displays = displays;
    }

    public VibranceRange? GetRange(string deviceName) =>
        _displays.TryGetValue(deviceName, out var display) ? display.Range : null;

    public int? GetLevel(string deviceName) =>
        _displays.TryGetValue(deviceName, out var display) && _api.TryGetDvc(display.Handle, out int current, out _, out _, out _, out _)
            ? current
            : null;

    public bool SetLevel(string deviceName, int level) =>
        _displays.TryGetValue(deviceName, out var display) &&
        _api.SetDvc(display.Handle, Math.Clamp(level, display.Range.Min, display.Range.Max), display.Extended);

    public void Dispose() => _api.Dispose();
}
