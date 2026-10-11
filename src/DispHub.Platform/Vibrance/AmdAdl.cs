using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DispHub.Platform.Vibrance;

/// <summary>
/// Minimal binding to AMD's Display Library (atiadlxx.dll, installed with AMD Software). Uses the display colour
/// "saturation" control, the same one AMD Software's custom colour panel uses. Layouts follow the MIT-licensed ADL
/// SDK headers (GPUOpen display-library); sizes are asserted in the unit tests.
/// </summary>
internal sealed unsafe class AmdAdl : IDisposable
{
    public const int ColorSaturation = 1 << 2;
    private const int Ok = 0;
    private const int MaxPath = 256;
    private const int DisplayConnected = 1 << 0;
    private const int DisplayMapped = 1 << 1;

    private readonly nint _library;
    private readonly nint _context;
    private readonly delegate* unmanaged[Cdecl]<nint, int> _destroy;
    private readonly delegate* unmanaged[Cdecl]<nint, int*, int> _adapterCount;
    private readonly delegate* unmanaged[Cdecl]<nint, AdapterInfo*, int, int> _adapterInfo;
    private readonly delegate* unmanaged[Cdecl]<nint, int, int*, DisplayInfo**, int, int> _displayInfo;
    private readonly delegate* unmanaged[Cdecl]<nint, int, int, int, int*, int*, int*, int*, int*, int> _colorGet;
    private readonly delegate* unmanaged[Cdecl]<nint, int, int, int, int, int> _colorSet;
    private bool _disposed;

    private AmdAdl(nint library, nint context)
    {
        _library = library;
        _context = context;
        _destroy = (delegate* unmanaged[Cdecl]<nint, int>)Export("ADL2_Main_Control_Destroy");
        _adapterCount = (delegate* unmanaged[Cdecl]<nint, int*, int>)Export("ADL2_Adapter_NumberOfAdapters_Get");
        _adapterInfo = (delegate* unmanaged[Cdecl]<nint, AdapterInfo*, int, int>)Export("ADL2_Adapter_AdapterInfo_Get");
        _displayInfo = (delegate* unmanaged[Cdecl]<nint, int, int*, DisplayInfo**, int, int>)Export("ADL2_Display_DisplayInfo_Get");
        _colorGet = (delegate* unmanaged[Cdecl]<nint, int, int, int, int*, int*, int*, int*, int*, int>)Export("ADL2_Display_Color_Get");
        _colorSet = (delegate* unmanaged[Cdecl]<nint, int, int, int, int, int>)Export("ADL2_Display_Color_Set");
    }

    /// <summary>A display on an AMD adapter, with its saturation range.</summary>
    public readonly record struct Display(string DeviceName, int AdapterIndex, int DisplayIndex, int Min, int Max, int Default);

    public static AmdAdl? TryLoad()
    {
        if (!Environment.Is64BitProcess || !NativeLibrary.TryLoad("atiadlxx.dll", out nint library))
            return null;

        if (!NativeLibrary.TryGetExport(library, "ADL2_Main_Control_Create", out nint createAddress))
        {
            NativeLibrary.Free(library);
            return null;
        }

        var create = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Stdcall]<int, nint>, int, nint*, int>)createAddress;
        nint context;
        if (create(&Allocate, 1, &context) != Ok || context == 0)
        {
            NativeLibrary.Free(library);
            return null;
        }
        return new AmdAdl(library, context);
    }

    public List<Display> EnumerateDisplays()
    {
        var result = new List<Display>();
        if (_adapterCount == null || _adapterInfo == null || _displayInfo == null || _colorGet == null)
            return result;

        int count = 0;
        if (_adapterCount(_context, &count) != Ok || count <= 0 || count > 64)
            return result;

        var adapters = new AdapterInfo[count];
        fixed (AdapterInfo* pAdapters = adapters)
        {
            for (int i = 0; i < count; i++)
                pAdapters[i].Size = sizeof(AdapterInfo);
            if (_adapterInfo(_context, pAdapters, count * sizeof(AdapterInfo)) != Ok)
                return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int a = 0; a < count; a++)
            {
                string deviceName = Text(pAdapters[a].DisplayName, MaxPath);
                if (pAdapters[a].Present == 0 || deviceName.Length == 0 || !seen.Add(deviceName))
                    continue;
                if (FindDisplay(pAdapters[a].AdapterIndex, deviceName) is { } display)
                    result.Add(display);
            }
        }
        return result;
    }

    public int? GetSaturation(Display display)
    {
        int current, defaultValue, min, max, step;
        return _colorGet(_context, display.AdapterIndex, display.DisplayIndex, ColorSaturation, &current, &defaultValue, &min, &max, &step) == Ok
            ? current
            : null;
    }

    public bool SetSaturation(Display display, int value) =>
        _colorSet != null && _colorSet(_context, display.AdapterIndex, display.DisplayIndex, ColorSaturation, value) == Ok;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_destroy != null)
            _destroy(_context);
        NativeLibrary.Free(_library);
    }

    private Display? FindDisplay(int adapterIndex, string deviceName)
    {
        int displayCount = 0;
        DisplayInfo* displays = null;
        if (_displayInfo(_context, adapterIndex, &displayCount, &displays, 0) != Ok || displays == null)
            return null;
        try
        {
            for (int i = 0; i < displayCount; i++)
            {
                DisplayInfo info = displays[i];
                if ((info.InfoValue & (DisplayConnected | DisplayMapped)) != (DisplayConnected | DisplayMapped) ||
                    info.LogicalAdapterIndex != adapterIndex)
                {
                    continue;
                }

                int current, defaultValue, min, max, step;
                if (_colorGet(_context, adapterIndex, info.LogicalIndex, ColorSaturation, &current, &defaultValue, &min, &max, &step) == Ok &&
                    max > min && defaultValue >= min && defaultValue <= max)
                {
                    return new Display(deviceName, adapterIndex, info.LogicalIndex, min, max, defaultValue);
                }
            }
            return null;
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)displays);
        }
    }

    private nint Export(string name) => NativeLibrary.TryGetExport(_library, name, out nint address) ? address : 0;

    private static string Text(byte* chars, int max)
    {
        int length = 0;
        while (length < max && chars[length] != 0)
            length++;
        return Encoding.ASCII.GetString(chars, length);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint Allocate(int size) => Marshal.AllocCoTaskMem(size);

    [StructLayout(LayoutKind.Sequential)]
    internal struct AdapterInfo // 1572 bytes
    {
        public int Size;
        public int AdapterIndex;
        public fixed byte Udid[MaxPath];
        public int BusNumber;
        public int DeviceNumber;
        public int FunctionNumber;
        public int VendorId;
        public fixed byte AdapterName[MaxPath];
        public fixed byte DisplayName[MaxPath];
        public int Present;
        public int Exist;
        public fixed byte DriverPath[MaxPath];
        public fixed byte DriverPathExt[MaxPath];
        public fixed byte PnpString[MaxPath];
        public int OsDisplayIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DisplayInfo // 552 bytes
    {
        public int LogicalIndex;
        public int PhysicalIndex;
        public int LogicalAdapterIndex;
        public int PhysicalAdapterIndex;
        public int ControllerIndex;
        public fixed byte Name[MaxPath];
        public fixed byte ManufacturerName[MaxPath];
        public int DisplayType;
        public int OutputType;
        public int Connector;
        public int InfoMask;
        public int InfoValue;
    }
}
