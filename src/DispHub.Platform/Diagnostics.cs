using System.Globalization;
using System.Text;
using DispHub.Core.Color;
using DispHub.Platform.Displays;
using DispHub.Platform.Gamma;
using DispHub.Platform.Vibrance;

namespace DispHub.Platform;

/// <summary>Builds the text behind "Copy diagnostics" and the --diagnose command line switch.</summary>
public static class Diagnostics
{
    public static string Describe(IReadOnlyList<MonitorStatus> monitors, string appVersion)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"DispHub {appVersion}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Windows {Environment.OSVersion.Version} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})");
        text.AppendLine(CultureInfo.InvariantCulture, $"Packaged: {PackageInfo.IsPackaged}");
        foreach (MonitorStatus status in monitors)
        {
            MonitorInfo m = status.Monitor;
            text.AppendLine(CultureInfo.InvariantCulture,
                $"- {m.Name} [{m.EdidId}] {m.DeviceName} primary={m.IsPrimary} internal={m.IsInternal} hdr={m.IsHdr} state={status.State} vibrance={status.SupportsVibrance}");
        }
        return text.ToString();
    }

    /// <summary>
    /// Developer probe: tries a series of ramps on every monitor, reports whether Windows kept each one, then
    /// restores the original. The screen flickers while it runs.
    /// </summary>
    public static string ProbeHardware()
    {
        var text = new StringBuilder();
        var cases = new (string Name, ColorSettings Settings)[]
        {
            ("neutral", ColorSettings.Neutral),
            ("3400K", new ColorSettings(TemperatureK: 3400)),
            ("2700K", new ColorSettings(TemperatureK: 2700)),
            ("2300K", new ColorSettings(TemperatureK: 2300)),
            ("2000K", new ColorSettings(TemperatureK: 2000)),
            ("10000K", new ColorSettings(TemperatureK: 10000)),
            ("gamma 0.3", new ColorSettings(Gamma: 0.3)),
            ("gamma 1.6", new ColorSettings(Gamma: 1.6)),
            ("gamma 2.2", new ColorSettings(Gamma: 2.2)),
            ("gamma 2.8", new ColorSettings(Gamma: 2.8)),
            ("brightness -50", new ColorSettings(Brightness: -50)),
            ("brightness +50", new ColorSettings(Brightness: 50)),
            ("contrast -50", new ColorSettings(Contrast: -50)),
            ("contrast +50", new ColorSettings(Contrast: 50)),
            ("night combo", new ColorSettings(Brightness: -30, TemperatureK: 2700)),
            ("gaming combo", new ColorSettings(Brightness: 20, Contrast: 15, Gamma: 1.8)),
        };

        foreach (MonitorInfo monitor in DisplayEnumerator.GetMonitors())
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"{monitor.Name} [{monitor.EdidId}] {monitor.DeviceName} hdr={monitor.IsHdr} key={monitor.Key}");
            GammaRamp? original = GdiGamma.TryGet(monitor.DeviceName);
            if (original is null)
            {
                text.AppendLine("  GetDeviceGammaRamp failed");
                continue;
            }

            text.AppendLine(CultureInfo.InvariantCulture,
                $"  original is identity: {original.ApproximatelyEquals(GammaRamp.Identity(), 256)}");
            try
            {
                foreach ((string name, ColorSettings settings) in cases)
                {
                    GammaRamp ramp = RampBuilder.Build(settings);
                    GammaResult result = GdiGamma.Set(monitor.DeviceName, ramp);
                    text.AppendLine(CultureInfo.InvariantCulture,
                        $"  {name,-16} {result,-9} ourLimits={RampLimits.Default.Accepts(ramp)}");
                    Thread.Sleep(150);
                }
            }
            finally
            {
                GdiGamma.Set(monitor.DeviceName, original);
            }
        }

        using NvidiaVibranceProvider? nvidia = NvidiaVibranceProvider.TryCreate();
        text.AppendLine(nvidia is null ? "NVAPI: not available" : "NVAPI: loaded");
        if (nvidia is not null)
        {
            foreach (MonitorInfo monitor in DisplayEnumerator.GetMonitors())
            {
                VibranceRange? range = nvidia.GetRange(monitor.DeviceName);
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"  {monitor.DeviceName}: {(range is { } r ? $"DVC {r.Min}..{r.Max} default {r.Default} current {nvidia.GetLevel(monitor.DeviceName)}" : "not driven by NVIDIA")}");
            }
        }

        text.Append(ProbeIntel());
        return text.ToString();
    }

    /// <summary>Developer probe for Intel saturation: briefly boosts saturation on each Intel display, then restores it.</summary>
    public static string ProbeIntel()
    {
        var text = new StringBuilder();
        using IntelControlLib? intel = IntelControlLib.TryLoad();
        if (intel is null)
            return "Intel Graphics Control Library: not available" + Environment.NewLine;

        text.AppendLine("Intel Graphics Control Library: loaded");
        IReadOnlyList<MonitorInfo> monitors = DisplayEnumerator.GetMonitors();
        foreach (IntelControlLib.Display display in intel.EnumerateDisplays())
        {
            MonitorInfo? monitor = monitors.FirstOrDefault(m => m.TargetId == display.TargetId);
            text.AppendLine(CultureInfo.InvariantCulture,
                $"  target {display.TargetId} luid {display.AdapterLuid:X} matrix block {display.MatrixBlockId} (type {display.MatrixBlockType}) -> {monitor?.DeviceName ?? "no match"} (monitor luid {monitor?.AdapterLuid:X})");
            bool set = intel.SetSaturation(display, 1.4);
            Thread.Sleep(1500);
            bool restored = intel.RestoreDefault(display);
            text.AppendLine(CultureInfo.InvariantCulture, $"  set 1.4: {set}, restore: {restored}");
        }
        return text.ToString();
    }
}
