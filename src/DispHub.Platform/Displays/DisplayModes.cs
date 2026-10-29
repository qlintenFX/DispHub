// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace DispHub.Platform.Displays;

/// <summary>A display's current resolution and refresh rate.</summary>
public readonly record struct DisplayMode(int Width, int Height, int RefreshHz);

/// <summary>A display's refresh rate and Windows HDR. Exists so the logic around it can be tested.</summary>
public interface IDisplayModeBackend
{
    /// <summary>The refresh rates the display offers at its current resolution, lowest first.</summary>
    IReadOnlyList<int> RefreshRates(MonitorInfo monitor);

    int? RefreshRate(MonitorInfo monitor);

    bool SetRefreshRate(MonitorInfo monitor, int hz);

    /// <summary>Whether Windows HDR is on, or null when the display can't do HDR.</summary>
    bool? Hdr(MonitorInfo monitor);

    bool SetHdr(MonitorInfo monitor, bool enable);
}

public sealed class DisplayModes : IDisplayModeBackend
{
    /// <summary>Only the calls Windows 10 has, for trying DispHub as it runs there (--simulate-windows10).</summary>
    public static bool OlderWindows { get; set; }

    public static DisplayMode? Current(string deviceName)
    {
        DEVMODEW mode = NewMode();
        if (!PInvoke.EnumDisplaySettings(deviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode))
            return null;
        return new DisplayMode((int)mode.dmPelsWidth, (int)mode.dmPelsHeight, (int)mode.dmDisplayFrequency);
    }

    public IReadOnlyList<int> RefreshRates(MonitorInfo monitor)
    {
        DEVMODEW current = NewMode();
        if (!PInvoke.EnumDisplaySettings(monitor.DeviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref current))
            return [];
        var rates = new SortedSet<int>();
        DEVMODEW mode = NewMode();
        for (uint i = 0; PInvoke.EnumDisplaySettings(monitor.DeviceName, (ENUM_DISPLAY_SETTINGS_MODE)i, ref mode); i++)
        {
            // 0 and 1 mean "the hardware's default", not a rate.
            if (mode.dmPelsWidth == current.dmPelsWidth && mode.dmPelsHeight == current.dmPelsHeight &&
                mode.dmBitsPerPel == current.dmBitsPerPel && mode.dmDisplayFlags == current.dmDisplayFlags &&
                mode.dmDisplayFrequency > 1)
            {
                rates.Add((int)mode.dmDisplayFrequency);
            }
        }
        return [.. rates];
    }

    public int? RefreshRate(MonitorInfo monitor) => Current(monitor.DeviceName)?.RefreshHz;

    public unsafe bool SetRefreshRate(MonitorInfo monitor, int hz)
    {
        DEVMODEW mode = NewMode();
        if (!PInvoke.EnumDisplaySettings(monitor.DeviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode))
            return false;
        mode.dmDisplayFrequency = (uint)hz;
        mode.dmFields = DEVMODE_FIELD_FLAGS.DM_DISPLAYFREQUENCY;
        // Not saved to the registry: should DispHub stop without putting it back, the next sign-in has the usual rate.
        return PInvoke.ChangeDisplaySettingsEx(monitor.DeviceName, mode, 0, null) == DISP_CHANGE.DISP_CHANGE_SUCCESSFUL;
    }

    public bool? Hdr(MonitorInfo monitor)
    {
        // Windows 11 24H2 and later tell HDR apart from wide colour on SDR screens; older builds only know "advanced colour".
        var info2 = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
        {
            header = Header<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>(monitor, DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2),
        };
        if (!OlderWindows && PInvoke.DisplayConfigGetDeviceInfo(ref info2.header) == 0)
            return info2.highDynamicRangeSupported ? info2.highDynamicRangeUserEnabled : null;

        var info = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = Header<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(monitor, DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO),
        };
        if (PInvoke.DisplayConfigGetDeviceInfo(ref info.header) != 0 ||
            !info.advancedColorSupported || info.advancedColorForceDisabled || info.wideColorEnforced)
        {
            return null;
        }
        return info.advancedColorEnabled;
    }

    public bool SetHdr(MonitorInfo monitor, bool enable)
    {
        var hdr = new DISPLAYCONFIG_SET_HDR_STATE
        {
            header = Header<DISPLAYCONFIG_SET_HDR_STATE>(monitor, DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE),
            enableHdr = enable,
        };
        if (!OlderWindows && PInvoke.DisplayConfigSetDeviceInfo(in hdr.header) == 0)
            return true;

        var state = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = Header<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(monitor, DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE),
            enableAdvancedColor = enable,
        };
        return PInvoke.DisplayConfigSetDeviceInfo(in state.header) == 0;
    }

    private static DEVMODEW NewMode() => new() { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };

    private static DISPLAYCONFIG_DEVICE_INFO_HEADER Header<T>(MonitorInfo monitor, DISPLAYCONFIG_DEVICE_INFO_TYPE type)
        where T : struct => new()
        {
            type = type,
            size = (uint)Marshal.SizeOf<T>(),
            adapterId = new LUID { LowPart = (uint)(monitor.AdapterLuid & 0xFFFF_FFFF), HighPart = (int)(monitor.AdapterLuid >> 32) },
            id = monitor.TargetId,
        };
}
