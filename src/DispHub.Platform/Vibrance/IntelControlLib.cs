using System.Runtime.InteropServices;

namespace DispHub.Platform.Vibrance;

/// <summary>
/// Minimal binding to the Intel Graphics Control Library (ControlLib.dll, installed with Intel graphics drivers).
/// Saturation is applied through the display pipe's 3x3 colour matrix ("CSC") block. Struct layouts mirror
/// igcl_api.h (version 1.1) under the x64 MSVC ABI; sizes are asserted in the unit tests.
/// </summary>
internal sealed unsafe class IntelControlLib : IDisposable
{
    private const int Success = 0;
    private const uint DisplayActive = 1 << 0;
    private const uint DisplayAttached = 1 << 1;
    private const int BlockType3x3Matrix = 3;
    private const int BlockType3x3MatrixAndOffsets = 4;
    private const int QueryCapability = 0;
    private const int OperationRestoreDefault = 1;
    private const int OperationSetCustom = 2;

    private readonly nint _library;
    private readonly nint _api;
    private readonly delegate* unmanaged[Cdecl]<nint, int> _close;
    private readonly delegate* unmanaged[Cdecl]<nint, uint*, nint*, int> _enumerateDevices;
    private readonly delegate* unmanaged[Cdecl]<nint, uint*, nint*, int> _enumerateDisplays;
    private readonly delegate* unmanaged[Cdecl]<nint, DeviceProperties*, int> _getDeviceProperties;
    private readonly delegate* unmanaged[Cdecl]<nint, DisplayProperties*, int> _getDisplayProperties;
    private readonly delegate* unmanaged[Cdecl]<nint, PipeGetConfig*, int> _getPipeConfig;
    private readonly delegate* unmanaged[Cdecl]<nint, PipeSetConfig*, int> _setPipeConfig;
    private bool _disposed;

    private IntelControlLib(nint library, nint api)
    {
        _library = library;
        _api = api;
        _close = (delegate* unmanaged[Cdecl]<nint, int>)Export("ctlClose");
        _enumerateDevices = (delegate* unmanaged[Cdecl]<nint, uint*, nint*, int>)Export("ctlEnumerateDevices");
        _enumerateDisplays = (delegate* unmanaged[Cdecl]<nint, uint*, nint*, int>)Export("ctlEnumerateDisplayOutputs");
        _getDeviceProperties = (delegate* unmanaged[Cdecl]<nint, DeviceProperties*, int>)Export("ctlGetDeviceProperties");
        _getDisplayProperties = (delegate* unmanaged[Cdecl]<nint, DisplayProperties*, int>)Export("ctlGetDisplayProperties");
        _getPipeConfig = (delegate* unmanaged[Cdecl]<nint, PipeGetConfig*, int>)Export("ctlPixelTransformationGetConfig");
        _setPipeConfig = (delegate* unmanaged[Cdecl]<nint, PipeSetConfig*, int>)Export("ctlPixelTransformationSetConfig");
    }

    /// <summary>An active Intel display output that has a colour matrix block.</summary>
    public readonly record struct Display(nint Handle, long AdapterLuid, uint TargetId, uint MatrixBlockId, int MatrixBlockType);

    public static IntelControlLib? TryLoad()
    {
        if (!Environment.Is64BitProcess || !NativeLibrary.TryLoad("ControlLib.dll", out nint library))
            return null;

        if (!NativeLibrary.TryGetExport(library, "ctlInit", out nint initAddress))
        {
            NativeLibrary.Free(library);
            return null;
        }

        var init = (delegate* unmanaged[Cdecl]<InitArgs*, nint*, int>)initAddress;
        var args = new InitArgs
        {
            Size = (uint)sizeof(InitArgs),
            AppVersion = (1u << 16) | 1u,
            ApplicationUid = Guid.Empty, // IGCL rejects unregistered IDs; all zeros is the documented default.
        };
        nint api;
        if (init(&args, &api) != Success || api == 0)
        {
            NativeLibrary.Free(library);
            return null;
        }
        return new IntelControlLib(library, api);
    }

    public List<Display> EnumerateDisplays()
    {
        var result = new List<Display>();
        if (_enumerateDevices == null || _enumerateDisplays == null || _getDisplayProperties == null || _getPipeConfig == null)
            return result;

        uint deviceCount = 0;
        if (_enumerateDevices(_api, &deviceCount, null) != Success || deviceCount == 0)
            return result;
        nint* devices = stackalloc nint[(int)Math.Min(deviceCount, 16)];
        deviceCount = Math.Min(deviceCount, 16);
        if (_enumerateDevices(_api, &deviceCount, devices) != Success)
            return result;

        nint* displays = stackalloc nint[32];
        for (int d = 0; d < deviceCount; d++)
        {
            long luid = GetAdapterLuid(devices[d]);

            uint displayCount = 0;
            if (_enumerateDisplays(devices[d], &displayCount, null) != Success || displayCount == 0)
                continue;
            displayCount = Math.Min(displayCount, 32);
            if (_enumerateDisplays(devices[d], &displayCount, displays) != Success)
                continue;

            for (int i = 0; i < displayCount; i++)
            {
                var properties = new DisplayProperties { Size = (uint)sizeof(DisplayProperties) };
                if (_getDisplayProperties(displays[i], &properties) != Success)
                    continue;
                if ((properties.DisplayConfigFlags & (DisplayActive | DisplayAttached)) != (DisplayActive | DisplayAttached))
                    continue;
                if (FindMatrixBlock(displays[i]) is { } block)
                    result.Add(new Display(displays[i], luid, properties.WindowsDisplayEncoderId, block.Id, block.Type));
            }
        }
        return result;
    }

    /// <summary>Sets saturation: 0 is greyscale, 1 is unchanged, 2 is double.</summary>
    public bool SetSaturation(Display display, double saturation)
    {
        if (Math.Abs(saturation - 1) < 0.001)
            return RestoreDefault(display);

        var block = new BlockConfig
        {
            Size = (uint)sizeof(BlockConfig),
            BlockId = display.MatrixBlockId,
            BlockType = display.MatrixBlockType,
        };
        block.Matrix.Size = (uint)sizeof(MatrixConfig);
        double[] matrix = SaturationMatrix(saturation);
        for (int i = 0; i < 9; i++)
            block.Matrix.Values[i] = matrix[i];

        var set = new PipeSetConfig
        {
            Size = (uint)sizeof(PipeSetConfig),
            OperationType = OperationSetCustom,
            NumBlocks = 1,
            Blocks = &block,
        };
        return _setPipeConfig(display.Handle, &set) == Success;
    }

    public bool RestoreDefault(Display display)
    {
        var block = new BlockConfig
        {
            Size = (uint)sizeof(BlockConfig),
            BlockId = display.MatrixBlockId,
            BlockType = display.MatrixBlockType,
        };
        var set = new PipeSetConfig
        {
            Size = (uint)sizeof(PipeSetConfig),
            OperationType = OperationRestoreDefault,
            NumBlocks = 1,
            Blocks = &block,
        };
        return _setPipeConfig != null && _setPipeConfig(display.Handle, &set) == Success;
    }

    /// <summary>Row-major saturation matrix using Rec.709 luma weights.</summary>
    internal static double[] SaturationMatrix(double s)
    {
        const double R = 0.2126, G = 0.7152, B = 0.0722;
        return
        [
            R + (s * (1 - R)), G - (s * G), B - (s * B),
            R - (s * R), G + (s * (1 - G)), B - (s * B),
            R - (s * R), G - (s * G), B + (s * (1 - B)),
        ];
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_close != null)
            _close(_api);
        NativeLibrary.Free(_library);
    }

    private (uint Id, int Type)? FindMatrixBlock(nint display)
    {
        var query = new PipeGetConfig { Size = (uint)sizeof(PipeGetConfig), QueryType = QueryCapability };
        if (_getPipeConfig(display, &query) != Success || query.NumBlocks == 0 || query.NumBlocks > 16)
            return null;

        int count = (int)query.NumBlocks;
        BlockConfig* blocks = stackalloc BlockConfig[count];
        new Span<byte>(blocks, count * sizeof(BlockConfig)).Clear();
        for (int i = 0; i < count; i++)
            blocks[i].Size = (uint)sizeof(BlockConfig);
        query.Blocks = blocks;
        if (_getPipeConfig(display, &query) != Success)
            return null;

        for (int i = 0; i < count; i++)
        {
            if (blocks[i].BlockType is BlockType3x3Matrix or BlockType3x3MatrixAndOffsets)
                return (blocks[i].BlockId, blocks[i].BlockType);
        }
        return null;
    }

    private long GetAdapterLuid(nint device)
    {
        if (_getDeviceProperties == null)
            return 0;
        long luid = 0;
        var properties = new DeviceProperties { Size = (uint)sizeof(DeviceProperties), DeviceId = &luid, DeviceIdSize = sizeof(long) };
        return _getDeviceProperties(device, &properties) == Success ? luid : 0;
    }

    private nint Export(string name) => NativeLibrary.TryGetExport(_library, name, out nint address) ? address : 0;

    // Layouts below follow igcl_api.h. Offsets in comments are for x64.

    [StructLayout(LayoutKind.Sequential)]
    internal struct InitArgs // 36 bytes
    {
        public uint Size;
        public byte Version;
        public uint AppVersion;
        public uint Flags;
        public uint SupportedVersion;
        public Guid ApplicationUid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 320)]
    internal struct DeviceProperties
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(4)] public byte Version;
        [FieldOffset(8)] public void* DeviceId;
        [FieldOffset(16)] public uint DeviceIdSize;
    }

    [StructLayout(LayoutKind.Explicit, Size = 200)]
    internal struct DisplayProperties
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(4)] public byte Version;
        [FieldOffset(8)] public uint WindowsDisplayEncoderId;
        [FieldOffset(48)] public uint DisplayConfigFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct MatrixConfig
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(4)] public byte Version;
        [FieldOffset(56)] public fixed double Values[9];
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    internal struct BlockConfig
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(4)] public byte Version;
        [FieldOffset(8)] public uint BlockId;
        [FieldOffset(12)] public int BlockType;
        [FieldOffset(16)] public MatrixConfig Matrix;
    }

    [StructLayout(LayoutKind.Explicit, Size = 272)]
    internal struct PipeGetConfig
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(4)] public byte Version;
        [FieldOffset(8)] public int QueryType;
        [FieldOffset(256)] public uint NumBlocks;
        [FieldOffset(264)] public BlockConfig* Blocks;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct PipeSetConfig
    {
        [FieldOffset(0)] public uint Size;
        [FieldOffset(4)] public byte Version;
        [FieldOffset(8)] public int OperationType;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint NumBlocks;
        [FieldOffset(24)] public BlockConfig* Blocks;
    }
}
