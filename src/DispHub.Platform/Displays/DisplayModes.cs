using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;

namespace DispHub.Platform.Displays;

/// <summary>A display's current resolution and refresh rate.</summary>
public readonly record struct DisplayMode(int Width, int Height, int RefreshHz);

public static class DisplayModes
{
    public static DisplayMode? Current(string deviceName)
    {
        var mode = new DEVMODEW { dmSize = (ushort)Marshal.SizeOf<DEVMODEW>() };
        if (!PInvoke.EnumDisplaySettings(deviceName, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, ref mode))
            return null;
        return new DisplayMode((int)mode.dmPelsWidth, (int)mode.dmPelsHeight, (int)mode.dmDisplayFrequency);
    }
}
