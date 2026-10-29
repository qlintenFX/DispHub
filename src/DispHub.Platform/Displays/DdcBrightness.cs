// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace DispHub.Platform.Displays;

/// <summary>A monitor's own brightness setting: the current value and the highest it takes.</summary>
public readonly record struct HardwareLevel(int Current, int Maximum)
{
    public int Percent => Maximum <= 0 ? 0 : (int)Math.Round(Current * 100.0 / Maximum);
}

/// <summary>The monitor's own brightness (what its buttons change). Exists so the logic around it can be tested.</summary>
public interface IHardwareBrightness
{
    /// <summary>Null when the monitor doesn't answer: DDC/CI off in its menu, a laptop screen, some docks and adapters.</summary>
    HardwareLevel? ReadLevel(string deviceName);

    bool WriteLevel(string deviceName, int percent);
}

/// <summary>
/// Monitor brightness over DDC/CI (VCP code 0x10, luminance), as Twinkle Tray and Monitorian do it. Each call can
/// take 50 to 200 ms, so it runs on its own thread, never on the UI or colour thread.
/// </summary>
public sealed class DdcBrightness : IHardwareBrightness
{
    private const byte Luminance = 0x10;

    public HardwareLevel? ReadLevel(string deviceName) => WithMonitor<HardwareLevel?>(deviceName, null, Read);

    public bool WriteLevel(string deviceName, int percent) => WithMonitor(deviceName, false, handle =>
    {
        if (Read(handle) is not { } level)
            return false;
        uint value = (uint)Math.Round(Math.Clamp(percent, 0, 100) * level.Maximum / 100.0);
        return PInvoke.SetVCPFeature(handle, Luminance, value) != 0;
    });

    private static unsafe HardwareLevel? Read(HANDLE handle)
    {
        uint current = 0, maximum = 0;
        if (PInvoke.GetVCPFeatureAndVCPFeatureReply(handle, Luminance, null, &current, &maximum) == 0 || maximum == 0)
            return null;
        return new HardwareLevel((int)current, (int)maximum);
    }

    /// <summary>Runs <paramref name="action"/> on the first physical monitor behind a GDI device such as \.\DISPLAY2.</summary>
    private static T WithMonitor<T>(string deviceName, T fallback, Func<HANDLE, T> action)
    {
        if (FindMonitor(deviceName) is not { } monitor ||
            !PInvoke.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out uint count) || count == 0)
        {
            return fallback;
        }
        var physical = new PHYSICAL_MONITOR[count];
        if (!PInvoke.GetPhysicalMonitorsFromHMONITOR(monitor, physical))
            return fallback;
        try
        {
            return action(physical[0].hPhysicalMonitor);
        }
        finally
        {
            PInvoke.DestroyPhysicalMonitors(physical);
        }
    }

    private static unsafe HMONITOR? FindMonitor(string deviceName)
    {
        var monitors = new List<HMONITOR>();
        GCHandle list = GCHandle.Alloc(monitors);
        try
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, null, &Collect, (LPARAM)GCHandle.ToIntPtr(list));
        }
        finally
        {
            list.Free();
        }

        foreach (HMONITOR monitor in monitors)
        {
            var info = new MONITORINFOEXW();
            info.monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>();
            if (PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info) &&
                string.Equals(info.szDevice.ToString(), deviceName, StringComparison.OrdinalIgnoreCase))
            {
                return monitor;
            }
        }
        return null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL Collect(HMONITOR monitor, HDC dc, RECT* clip, LPARAM data)
    {
        ((List<HMONITOR>)GCHandle.FromIntPtr(data).Target!).Add(monitor);
        return true;
    }
}
