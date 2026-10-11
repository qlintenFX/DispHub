using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;

namespace DispHub.Platform.Displays;

/// <summary>An active monitor.</summary>
/// <param name="Key">Stable ID: the monitor device path, lowercased. Survives reboots and port renumbering.</param>
/// <param name="DeviceName">GDI device name such as \\.\DISPLAY1, used for gamma ramps. Can change between boots.</param>
/// <param name="Name">Friendly name from the monitor's EDID, or "Built-in display".</param>
public sealed record MonitorInfo(
    string Key,
    string DeviceName,
    string Name,
    bool IsPrimary,
    bool IsInternal,
    bool IsHdr,
    string EdidId,
    long AdapterLuid = 0,
    uint TargetId = 0);

/// <summary>Lists active monitors through the Windows display configuration API.</summary>
public static class DisplayEnumerator
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var result = new List<MonitorInfo>();
        if (!TryQueryPaths(out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes))
            return result;

        foreach (DISPLAYCONFIG_PATH_INFO path in paths)
        {
            string? deviceName = GetSourceName(path);
            if (deviceName is null)
                continue;

            DISPLAYCONFIG_TARGET_DEVICE_NAME target = GetTargetName(path);
            string devicePath = target.monitorDevicePath.ToString();
            string key = string.IsNullOrEmpty(devicePath)
                ? $"{deviceName}#{path.targetInfo.id}".ToLowerInvariant()
                : devicePath.ToLowerInvariant();

            bool isInternal = target.outputTechnology is
                DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL or
                DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED or
                DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED;

            string name = target.monitorFriendlyDeviceName.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = isInternal ? "Built-in display" : deviceName.TrimStart('\\', '.');

            result.Add(new MonitorInfo(
                key,
                deviceName,
                name,
                IsPrimary(path, modes),
                isInternal,
                IsAdvancedColorEnabled(path),
                EdidId(target.edidManufactureId, target.edidProductCodeId),
                ((long)path.targetInfo.adapterId.HighPart << 32) | path.targetInfo.adapterId.LowPart,
                path.targetInfo.id));
        }

        // Clone mode reports one path per target; keep a stable, primary-first order.
        return result
            .GroupBy(m => m.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Turns the packed EDID manufacturer ID into its three-letter code plus product code, e.g. DEL41A8.</summary>
    internal static string EdidId(ushort manufacturer, ushort product)
    {
        if (manufacturer == 0)
            return "";
        // The manufacturer ID is big-endian in the EDID but reported byte-swapped here.
        int id = (ushort)((manufacturer << 8) | (manufacturer >> 8));
        char c1 = (char)('A' - 1 + ((id >> 10) & 0x1F));
        char c2 = (char)('A' - 1 + ((id >> 5) & 0x1F));
        char c3 = (char)('A' - 1 + (id & 0x1F));
        return $"{c1}{c2}{c3}{product:X4}";
    }

    private static bool TryQueryPaths(out DISPLAYCONFIG_PATH_INFO[] paths, out DISPLAYCONFIG_MODE_INFO[] modes)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            WIN32_ERROR sizeResult = PInvoke.GetDisplayConfigBufferSizes(
                QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
            if (sizeResult != WIN32_ERROR.ERROR_SUCCESS)
                break;

            paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            WIN32_ERROR result = PInvoke.QueryDisplayConfig(
                QUERY_DISPLAY_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes);
            if (result == WIN32_ERROR.ERROR_SUCCESS)
            {
                paths = paths[..(int)pathCount];
                modes = modes[..(int)modeCount];
                return true;
            }

            // The topology changed between the two calls; try again.
            if (result != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
                break;
        }

        paths = [];
        modes = [];
        return false;
    }

    private static string? GetSourceName(DISPLAYCONFIG_PATH_INFO path)
    {
        var request = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
        request.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
        request.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
        request.header.adapterId = path.sourceInfo.adapterId;
        request.header.id = path.sourceInfo.id;
        if (PInvoke.DisplayConfigGetDeviceInfo(ref request.header) != 0)
            return null;
        string name = request.viewGdiDeviceName.ToString();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private static DISPLAYCONFIG_TARGET_DEVICE_NAME GetTargetName(DISPLAYCONFIG_PATH_INFO path)
    {
        var request = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
        request.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
        request.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        request.header.adapterId = path.targetInfo.adapterId;
        request.header.id = path.targetInfo.id;
        return PInvoke.DisplayConfigGetDeviceInfo(ref request.header) == 0 ? request : default;
    }

    private static bool IsAdvancedColorEnabled(DISPLAYCONFIG_PATH_INFO path)
    {
        var request = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
        request.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
        request.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>();
        request.header.adapterId = path.targetInfo.adapterId;
        request.header.id = path.targetInfo.id;
        return PInvoke.DisplayConfigGetDeviceInfo(ref request.header) == 0
            && request.advancedColorEnabled
            && !request.wideColorEnforced;
    }

    private static bool IsPrimary(DISPLAYCONFIG_PATH_INFO path, DISPLAYCONFIG_MODE_INFO[] modes)
    {
        uint index = path.sourceInfo.modeInfoIdx;
        if (index >= modes.Length || modes[index].infoType != DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
            return false;
        var position = modes[index].sourceMode.position;
        return position.x == 0 && position.y == 0;
    }
}
