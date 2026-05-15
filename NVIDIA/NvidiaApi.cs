// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using DispHub.Services.Logging;

namespace DispHub.NVIDIA;

internal static class NvidiaApi
{
    private const int NVAPI_OK = 0;
    private const int NVAPI_MAX_PHYSICAL_GPUS = 64;

    private const uint ID_NvAPI_Initialize = 0x0150E828;
    private const uint ID_NvAPI_Unload = 0xD22BDD7E;
    private const uint ID_NvAPI_EnumPhysicalGPUs = 0xE5AC921F;
    private const uint ID_NvAPI_GPU_GetSystemType = 0xBAAABFCC;
    private const uint ID_NvAPI_EnumNvidiaDisplayHandle = 0x9ABDD40D;
    private const uint ID_NvAPI_GetDVCInfo = 0x4085DE45;
    private const uint ID_NvAPI_SetDVCLevel = 0x172409B4;
    private const uint ID_NvAPI_GetDVCInfoEx = 0x0E45002D;
    private const uint ID_NvAPI_SetDVCLevelEx = 0x4A82C2B1;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_Initialize_t();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_Unload_t();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_EnumPhysicalGPUs_t([In, Out] IntPtr[] gpuHandles, out int gpuCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_GPU_GetSystemType_t(IntPtr gpuHandle, out int systemType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_EnumNvidiaDisplayHandle_t(int thisEnum, out IntPtr displayHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_GetDVCInfo_t(IntPtr displayHandle, uint outputId, ref NvDisplayDvcInfo info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_SetDVCLevel_t(IntPtr displayHandle, uint outputId, int level);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_GetDVCInfoEx_t(IntPtr displayHandle, uint outputId, ref NvDisplayDvcInfoEx info);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_SetDVCLevelEx_t(IntPtr displayHandle, uint outputId, ref NvDisplayDvcInfoEx info);

    private static NvAPI_Initialize_t? _initialize;
    private static NvAPI_Unload_t? _unload;
    private static NvAPI_EnumPhysicalGPUs_t? _enumGpus;
    private static NvAPI_GPU_GetSystemType_t? _getSystemType;
    private static NvAPI_EnumNvidiaDisplayHandle_t? _enumDisplayHandle;
    private static NvAPI_GetDVCInfo_t? _getDVCInfo;
    private static NvAPI_SetDVCLevel_t? _setDVCLevel;
    private static NvAPI_GetDVCInfoEx_t? _getDVCInfoEx;
    private static NvAPI_SetDVCLevelEx_t? _setDVCLevelEx;

    private static bool _loaded;

    public static bool HasExtendedDvc => _getDVCInfoEx != null && _setDVCLevelEx != null;

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    private static T? GetDelegate<T>(uint id) where T : Delegate
    {
        IntPtr ptr = QueryInterface(id);
        if (ptr == IntPtr.Zero) return null;
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    public static bool Load()
    {
        if (_loaded) return true;
        try
        {
            _initialize = GetDelegate<NvAPI_Initialize_t>(ID_NvAPI_Initialize);
            _unload = GetDelegate<NvAPI_Unload_t>(ID_NvAPI_Unload);
            _enumGpus = GetDelegate<NvAPI_EnumPhysicalGPUs_t>(ID_NvAPI_EnumPhysicalGPUs);
            _getSystemType = GetDelegate<NvAPI_GPU_GetSystemType_t>(ID_NvAPI_GPU_GetSystemType);
            _enumDisplayHandle = GetDelegate<NvAPI_EnumNvidiaDisplayHandle_t>(ID_NvAPI_EnumNvidiaDisplayHandle);
            _getDVCInfo = GetDelegate<NvAPI_GetDVCInfo_t>(ID_NvAPI_GetDVCInfo);
            _setDVCLevel = GetDelegate<NvAPI_SetDVCLevel_t>(ID_NvAPI_SetDVCLevel);
            _getDVCInfoEx = GetDelegate<NvAPI_GetDVCInfoEx_t>(ID_NvAPI_GetDVCInfoEx);
            _setDVCLevelEx = GetDelegate<NvAPI_SetDVCLevelEx_t>(ID_NvAPI_SetDVCLevelEx);

            _loaded = _initialize != null && _unload != null &&
                      _enumGpus != null && _enumDisplayHandle != null &&
                      _getDVCInfo != null && _setDVCLevel != null;

            return _loaded;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load NVAPI function pointers", ex);
            _loaded = false;
            return false;
        }
    }

    public static bool Initialize() => _initialize?.Invoke() == NVAPI_OK;

    public static void Unload() => _unload?.Invoke();

    public static bool EnumPhysicalGPUs(out IntPtr[] gpuHandles, out int count)
    {
        gpuHandles = new IntPtr[NVAPI_MAX_PHYSICAL_GPUS];
        count = 0;
        if (_enumGpus == null) return false;
        return _enumGpus(gpuHandles, out count) == NVAPI_OK;
    }

    public static NvSystemType GetGpuSystemType(IntPtr gpuHandle)
    {
        if (_getSystemType == null) return NvSystemType.Unknown;
        return _getSystemType(gpuHandle, out int systemType) == NVAPI_OK
            ? (NvSystemType)systemType
            : NvSystemType.Unknown;
    }

    public static bool EnumDisplayHandle(int index, out IntPtr displayHandle)
    {
        displayHandle = IntPtr.Zero;
        if (_enumDisplayHandle == null) return false;
        return _enumDisplayHandle(index, out displayHandle) == NVAPI_OK;
    }

    public static bool GetDVCInfo(IntPtr displayHandle, out NvDisplayDvcInfo info)
    {
        info = new NvDisplayDvcInfo();
        if (_getDVCInfo == null) return false;
        info.Version = (uint)Marshal.SizeOf<NvDisplayDvcInfo>() | 0x10000;
        return _getDVCInfo(displayHandle, 0, ref info) == NVAPI_OK;
    }

    public static bool SetDVCLevel(IntPtr displayHandle, int level)
    {
        if (_setDVCLevel == null) return false;
        return _setDVCLevel(displayHandle, 0, level) == NVAPI_OK;
    }

    public static bool GetDVCInfoEx(IntPtr displayHandle, out NvDisplayDvcInfoEx info)
    {
        info = new NvDisplayDvcInfoEx();
        if (_getDVCInfoEx == null) return false;
        info.Version = (uint)Marshal.SizeOf<NvDisplayDvcInfoEx>() | 0x10000;
        return _getDVCInfoEx(displayHandle, 0, ref info) == NVAPI_OK;
    }

    public static bool SetDVCLevelEx(IntPtr displayHandle, int level)
    {
        if (_setDVCLevelEx == null) return false;
        var info = new NvDisplayDvcInfoEx
        {
            Version = (uint)Marshal.SizeOf<NvDisplayDvcInfoEx>() | 0x10000,
            CurrentLevel = level
        };
        return _setDVCLevelEx(displayHandle, 0, ref info) == NVAPI_OK;
    }
}
