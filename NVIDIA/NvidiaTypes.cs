using System.Runtime.InteropServices;

namespace DisplayHub.NVIDIA;

[StructLayout(LayoutKind.Sequential)]
public struct NvDisplayDvcInfo
{
    public uint Version;
    public int CurrentLevel;
    public int MinLevel;
    public int MaxLevel;
}

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct NvDisplayDvcInfoEx
{
    public uint Version;
    public int CurrentLevel;
    public int MinLevel;
    public int MaxLevel;
    public int DefaultLevel;
}

public enum NvSystemType
{
    Unknown = 0,
    Laptop = 1,
    Desktop = 2
}
