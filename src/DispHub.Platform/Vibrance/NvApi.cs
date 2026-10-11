using System.Runtime.InteropServices;
using System.Text;

namespace DispHub.Platform.Vibrance;

/// <summary>
/// Minimal binding to NVIDIA's driver library (nvapi64.dll, installed with every NVIDIA driver).
/// Functions are looked up by ID through nvapi_QueryInterface. The DVC (digital vibrance) IDs are not in the
/// public NVAPI headers; they are the community-documented IDs also used by NvAPIWrapper.
/// </summary>
internal sealed unsafe class NvApi : IDisposable
{
    public const int Ok = 0;
    private const int EndEnumeration = -7;

    private readonly nint _library;
    private readonly delegate* unmanaged[Cdecl]<int> _unload;
    private readonly delegate* unmanaged[Cdecl]<uint, nint*, int> _enumDisplayHandle;
    private readonly delegate* unmanaged[Cdecl]<nint, byte*, int> _getDisplayName;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, DvcInfo*, int> _getDvcInfo;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, int, int> _setDvcLevel;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, DvcInfoEx*, int> _getDvcInfoEx;
    private readonly delegate* unmanaged[Cdecl]<nint, uint, DvcInfoEx*, int> _setDvcLevelEx;
    private bool _disposed;

    private NvApi(nint library, delegate* unmanaged[Cdecl]<uint, nint> query)
    {
        _library = library;
        _unload = (delegate* unmanaged[Cdecl]<int>)query(0xD22BDD7E);
        _enumDisplayHandle = (delegate* unmanaged[Cdecl]<uint, nint*, int>)query(0x9ABDD40D);
        _getDisplayName = (delegate* unmanaged[Cdecl]<nint, byte*, int>)query(0x22A78B05);
        _getDvcInfo = (delegate* unmanaged[Cdecl]<nint, uint, DvcInfo*, int>)query(0x4085DE45);
        _setDvcLevel = (delegate* unmanaged[Cdecl]<nint, uint, int, int>)query(0x172409B4);
        _getDvcInfoEx = (delegate* unmanaged[Cdecl]<nint, uint, DvcInfoEx*, int>)query(0x0E45002D);
        _setDvcLevelEx = (delegate* unmanaged[Cdecl]<nint, uint, DvcInfoEx*, int>)query(0x4A82C2B1);
    }

    /// <summary>Loads and initialises NVAPI, or returns null when there is no NVIDIA driver.</summary>
    public static NvApi? TryLoad()
    {
        if (!Environment.Is64BitProcess || !NativeLibrary.TryLoad("nvapi64.dll", out nint library))
            return null;

        if (!NativeLibrary.TryGetExport(library, "nvapi_QueryInterface", out nint queryAddress))
        {
            NativeLibrary.Free(library);
            return null;
        }

        var query = (delegate* unmanaged[Cdecl]<uint, nint>)queryAddress;
        var initialize = (delegate* unmanaged[Cdecl]<int>)query(0x0150E828);
        if (initialize == null || initialize() != Ok)
        {
            NativeLibrary.Free(library);
            return null;
        }

        var api = new NvApi(library, query);
        if (api._enumDisplayHandle == null || api._getDisplayName == null ||
            (api._getDvcInfoEx == null && api._getDvcInfo == null))
        {
            api.Dispose();
            return null;
        }
        return api;
    }

    /// <summary>Display handles for every display driven by an NVIDIA GPU, with their GDI names.</summary>
    public List<(nint Handle, string DeviceName)> EnumerateDisplays()
    {
        var displays = new List<(nint, string)>();
        byte* name = stackalloc byte[64];
        for (uint index = 0; index < 64; index++)
        {
            nint handle;
            int status = _enumDisplayHandle(index, &handle);
            if (status == EndEnumeration || status != Ok)
                break;

            new Span<byte>(name, 64).Clear();
            if (_getDisplayName(handle, name) == Ok)
                displays.Add((handle, Encoding.ASCII.GetString(name, IndexOfNull(name, 64))));
        }
        return displays;
    }

    public bool TryGetDvc(nint display, out int current, out int min, out int max, out int defaultLevel, out bool extended)
    {
        if (_getDvcInfoEx != null)
        {
            var info = new DvcInfoEx { Version = (uint)sizeof(DvcInfoEx) | 0x10000 };
            if (_getDvcInfoEx(display, 0, &info) == Ok && info.MaxLevel > info.MinLevel)
            {
                (current, min, max, defaultLevel, extended) = (info.CurrentLevel, info.MinLevel, info.MaxLevel, info.DefaultLevel, true);
                return true;
            }
        }

        if (_getDvcInfo != null)
        {
            var info = new DvcInfo { Version = (uint)sizeof(DvcInfo) | 0x10000 };
            if (_getDvcInfo(display, 0, &info) == Ok && info.MaxLevel > info.MinLevel)
            {
                // The legacy call has no default; NVIDIA's default is the bottom of the 0-63 range.
                (current, min, max, defaultLevel, extended) = (info.CurrentLevel, info.MinLevel, info.MaxLevel, info.MinLevel, false);
                return true;
            }
        }

        (current, min, max, defaultLevel, extended) = (0, 0, 0, 0, false);
        return false;
    }

    public bool SetDvc(nint display, int level, bool extended)
    {
        if (extended && _setDvcLevelEx != null)
        {
            var info = new DvcInfoEx { Version = (uint)sizeof(DvcInfoEx) | 0x10000, CurrentLevel = level };
            return _setDvcLevelEx(display, 0, &info) == Ok;
        }
        return _setDvcLevel != null && _setDvcLevel(display, 0, level) == Ok;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_unload != null)
            _unload();
        NativeLibrary.Free(_library);
    }

    private static int IndexOfNull(byte* text, int max)
    {
        int length = 0;
        while (length < max && text[length] != 0)
            length++;
        return length;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DvcInfo
    {
        public uint Version;
        public int CurrentLevel;
        public int MinLevel;
        public int MaxLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DvcInfoEx
    {
        public uint Version;
        public int CurrentLevel;
        public int MinLevel;
        public int MaxLevel;
        public int DefaultLevel;
    }
}
