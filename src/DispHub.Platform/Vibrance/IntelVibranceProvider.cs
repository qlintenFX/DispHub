using DispHub.Core.Color;
using DispHub.Platform.Displays;

namespace DispHub.Platform.Vibrance;

/// <summary>
/// Saturation on displays driven by an Intel GPU, through the Intel Graphics Control Library. This covers the
/// built-in screen of most laptops, including NVIDIA Optimus laptops where NVIDIA's vibrance can't reach it.
/// Levels are saturation in percent: 100 is unchanged, 0 is greyscale, 200 doubles it.
/// </summary>
public sealed class IntelVibranceProvider : IVibranceProvider
{
    private static readonly VibranceRange Range = new(0, 200, 100);
    private readonly IntelControlLib _lib;
    private readonly Dictionary<string, int> _levels = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, IntelControlLib.Display> _displays = new(StringComparer.OrdinalIgnoreCase);

    private IntelVibranceProvider(IntelControlLib lib) => _lib = lib;

    public string Vendor => "Intel";

    public static IntelVibranceProvider? TryCreate()
    {
        IntelControlLib? lib = IntelControlLib.TryLoad();
        return lib is null ? null : new IntelVibranceProvider(lib);
    }

    public void Refresh(IReadOnlyList<MonitorInfo> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var displays = new Dictionary<string, IntelControlLib.Display>(StringComparer.OrdinalIgnoreCase);
        foreach (IntelControlLib.Display display in _lib.EnumerateDisplays())
        {
            // Match on adapter + target ID; fall back to target ID alone when the driver doesn't report the adapter.
            MonitorInfo? monitor = monitors.FirstOrDefault(m => m.TargetId == display.TargetId && m.AdapterLuid == display.AdapterLuid)
                ?? (display.AdapterLuid == 0 ? monitors.FirstOrDefault(m => m.TargetId == display.TargetId) : null);
            if (monitor is not null)
                displays.TryAdd(monitor.DeviceName, display);
        }
        _displays = displays;
    }

    public VibranceRange? GetRange(string deviceName) => _displays.ContainsKey(deviceName) ? Range : null;

    /// <summary>The driver doesn't report the current matrix; DispHub restores Intel's default, which is 100.</summary>
    public int? GetLevel(string deviceName) =>
        _displays.ContainsKey(deviceName) ? _levels.GetValueOrDefault(deviceName, Range.Default) : null;

    public bool SetLevel(string deviceName, int level)
    {
        if (!_displays.TryGetValue(deviceName, out IntelControlLib.Display display))
            return false;
        int clamped = Math.Clamp(level, Range.Min, Range.Max);
        if (!_lib.SetSaturation(display, clamped / 100.0))
            return false;
        _levels[deviceName] = clamped;
        return true;
    }

    public void Dispose()
    {
        foreach (IntelControlLib.Display display in _displays.Values)
            _lib.RestoreDefault(display);
        _lib.Dispose();
    }
}
