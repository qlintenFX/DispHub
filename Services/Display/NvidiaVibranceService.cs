// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Constants;
using DispHub.NVIDIA;
using DispHub.Services.Logging;

namespace DispHub.Services.Display;

public sealed class NvidiaVibranceService : IVibranceService
{
    private readonly IntPtr _displayHandle;
    private readonly bool _isSupported;
    private readonly bool _useExtendedApi;
    private readonly int _dvcMin;
    private readonly int _dvcMax;
    private readonly int _dvcDefault;
    private int _lastAppliedLevel = int.MinValue;
    private bool _disposed;

    public bool IsSupported => _isSupported && !_disposed;
    public int MinValue => AppConstants.VibranceMin;
    public int MaxValue => AppConstants.VibranceMax;
    public int DefaultValue => AppConstants.VibranceDefault;

    public NvidiaVibranceService()
    {
        try
        {
            if (!TryInitializeNvidiaApi() || !TryValidateGpus())
            {
                _isSupported = false;
                return;
            }

            if (!NvidiaApi.EnumDisplayHandle(0, out _displayHandle))
            {
                Logger.Log("No NVIDIA display handle found");
                _isSupported = false;
                return;
            }

            (_useExtendedApi, _dvcMin, _dvcMax, _dvcDefault) = ResolveDvcRange(_displayHandle);
            _isSupported = true;
            Logger.Log($"NVIDIA vibrance service initialized (DVC: {_dvcMin} to {_dvcMax}, extended: {_useExtendedApi})");
        }
        catch (DllNotFoundException ex)
        {
            Logger.LogError("nvapi.dll not found", ex);
            _isSupported = false;
        }
        catch (Exception ex)
        {
            Logger.LogError("NVIDIA vibrance initialization failed", ex);
            _isSupported = false;
        }
    }

    private static bool TryInitializeNvidiaApi()
    {
        if (!NvidiaApi.Load())
        {
            Logger.Log("NVAPI loading failed");
            return false;
        }
        if (!NvidiaApi.Initialize())
        {
            Logger.Log("NVAPI initialization failed");
            return false;
        }
        return true;
    }

    private static bool TryValidateGpus()
    {
        if (!NvidiaApi.EnumPhysicalGPUs(out IntPtr[] gpuHandles, out int gpuCount))
        {
            Logger.Log("Failed to enumerate NVIDIA GPUs");
            return false;
        }

        for (int i = 0; i < gpuCount; i++)
        {
            if (gpuHandles[i] == IntPtr.Zero) continue;
            NvSystemType systemType = NvidiaApi.GetGpuSystemType(gpuHandles[i]);
            if (systemType == NvSystemType.Laptop)
            {
                Logger.Log("NVIDIA laptop GPU - DVC not supported");
                return false;
            }
            if (systemType == NvSystemType.Unknown)
            {
                Logger.Log("NVIDIA GPU system type unknown");
                return false;
            }
        }
        return true;
    }

    private static (bool useExtended, int min, int max, int defaultLevel) ResolveDvcRange(IntPtr displayHandle)
    {
        if (NvidiaApi.HasExtendedDvc &&
            NvidiaApi.GetDVCInfoEx(displayHandle, out NvDisplayDvcInfoEx dvcInfoEx) &&
            dvcInfoEx.MaxLevel > dvcInfoEx.MinLevel)
        {
            return (true, dvcInfoEx.MinLevel, dvcInfoEx.MaxLevel, dvcInfoEx.DefaultLevel);
        }

        if (NvidiaApi.GetDVCInfo(displayHandle, out NvDisplayDvcInfo dvcInfo) &&
            dvcInfo.MaxLevel > dvcInfo.MinLevel)
        {
            return (false, dvcInfo.MinLevel, dvcInfo.MaxLevel, AppConstants.NvidiaVibranceDefault);
        }

        return (false, AppConstants.NvidiaVibranceMin, AppConstants.NvidiaVibranceMax, AppConstants.NvidiaVibranceDefault);
    }

    public bool ApplyVibrance(int value)
    {
        if (!IsSupported) return false;
        int level = MapToNvidiaRange(value, _dvcMin, _dvcMax);
        if (level == _lastAppliedLevel) return true;
        try
        {
            bool result = _useExtendedApi
                ? NvidiaApi.SetDVCLevelEx(_displayHandle, level)
                : NvidiaApi.SetDVCLevel(_displayHandle, level);
            if (result) _lastAppliedLevel = level;
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to set NVIDIA vibrance level to {level}", ex);
            return false;
        }
    }

    public void ResetVibrance()
    {
        if (!IsSupported) return;
        try
        {
            if (_useExtendedApi)
                NvidiaApi.SetDVCLevelEx(_displayHandle, _dvcDefault);
            else
                NvidiaApi.SetDVCLevel(_displayHandle, _dvcDefault);
            _lastAppliedLevel = _dvcDefault;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to reset NVIDIA vibrance", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_isSupported)
        {
            try
            {
                ResetVibrance();
                NvidiaApi.Unload();
                Logger.Log("NVIDIA vibrance service disposed");
            }
            catch (Exception ex)
            {
                Logger.LogError("Error during NVIDIA vibrance service disposal", ex);
            }
        }
    }

    internal static int MapToNvidiaRange(int appValue, int dvcMin, int dvcMax)
    {
        int clamped = Math.Clamp(appValue, AppConstants.VibranceMin, AppConstants.VibranceMax);
        return dvcMin + (clamped * (dvcMax - dvcMin)) / AppConstants.VibranceMax;
    }
}
