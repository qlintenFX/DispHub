// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace DispHub.Platform.Gamma;

public enum GammaResult
{
    Applied,
    /// <summary>Windows reported success but kept a different ramp (its undocumented safety limits).</summary>
    Rejected,
    /// <summary>The device has no loadable gamma ramp or the call failed.</summary>
    Failed,
}

/// <summary>Reads and writes a monitor's hardware gamma ramp through GDI.</summary>
public static class GdiGamma
{
    /// <summary>Readback tolerance: drivers round ramps to their LUT precision (often 10 bits).</summary>
    private const int ReadbackTolerance = 512;

    public static unsafe GammaRamp? TryGet(string deviceName)
    {
        HDC dc = PInvoke.CreateDCW(deviceName, null, null, null);
        if (dc.IsNull)
            return null;
        try
        {
            var ramp = new GammaRamp();
            fixed (ushort* values = ramp)
            {
                return PInvoke.GetDeviceGammaRamp(dc, values) ? ramp : null;
            }
        }
        finally
        {
            PInvoke.DeleteDC(dc);
        }
    }

    /// <summary>Sets the ramp, then reads it back because SetDeviceGammaRamp returns TRUE even when it ignored us.</summary>
    public static unsafe GammaResult Set(string deviceName, GammaRamp ramp)
    {
        ArgumentNullException.ThrowIfNull(ramp);
        HDC dc = PInvoke.CreateDCW(deviceName, null, null, null);
        if (dc.IsNull)
            return GammaResult.Failed;
        try
        {
            fixed (ushort* values = ramp)
            {
                if (!PInvoke.SetDeviceGammaRamp(dc, values))
                    return GammaResult.Failed;
            }

            var readBack = new GammaRamp();
            fixed (ushort* values = readBack)
            {
                if (!PInvoke.GetDeviceGammaRamp(dc, values))
                    return GammaResult.Applied;
            }
            return readBack.ApproximatelyEquals(ramp, ReadbackTolerance) ? GammaResult.Applied : GammaResult.Rejected;
        }
        finally
        {
            PInvoke.DeleteDC(dc);
        }
    }

    public static bool Matches(string deviceName, GammaRamp expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        GammaRamp? current = TryGet(deviceName);
        return current is not null && current.ApproximatelyEquals(expected, ReadbackTolerance);
    }
}
