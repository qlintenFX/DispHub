using KeyedColors.Constants;
using KeyedColors.NVIDIA;
using KeyedColors.Services.Logging;

namespace KeyedColors.Services.Display;

/// <summary>
/// NVIDIA vibrance control via direct NVAPI calls (no vibranceDLL dependency).
/// Prefers the extended API (GetDVCInfoEx/SetDVCLevelEx) for full range including desaturation.
/// Falls back to standard API (GetDVCInfo/SetDVCLevel) if extended functions aren't available.
/// </summary>
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
            if (!NvidiaApi.Load())
            {
                Logger.Log("NVAPI loading failed - NVIDIA drivers may not be installed");
                _isSupported = false;
                return;
            }

            if (!NvidiaApi.Initialize())
            {
                Logger.Log("NVAPI initialization failed");
                _isSupported = false;
                return;
            }

            if (!NvidiaApi.EnumPhysicalGPUs(out IntPtr[] gpuHandles, out int gpuCount))
            {
                Logger.Log("Failed to enumerate NVIDIA GPUs");
                _isSupported = false;
                return;
            }

            for (int i = 0; i < gpuCount; i++)
            {
                if (gpuHandles[i] == IntPtr.Zero) continue;

                NvSystemType systemType = NvidiaApi.GetGpuSystemType(gpuHandles[i]);
                if (systemType == NvSystemType.Laptop)
                {
                    Logger.Log("NVIDIA laptop GPU detected - Digital Vibrance Control not supported");
                    _isSupported = false;
                    return;
                }

                if (systemType == NvSystemType.Unknown)
                {
                    Logger.Log("NVIDIA GPU system type unknown - skipping");
                    _isSupported = false;
                    return;
                }
            }

            if (!NvidiaApi.EnumDisplayHandle(0, out _displayHandle))
            {
                Logger.Log("No NVIDIA display handle found");
                _isSupported = false;
                return;
            }

            // Try extended API first (supports full range including desaturation)
            if (NvidiaApi.HasExtendedDvc &&
                NvidiaApi.GetDVCInfoEx(_displayHandle, out NvDisplayDvcInfoEx dvcInfoEx) &&
                dvcInfoEx.MaxLevel > dvcInfoEx.MinLevel)
            {
                _useExtendedApi = true;
                _dvcMin = dvcInfoEx.MinLevel;
                _dvcMax = dvcInfoEx.MaxLevel;
                _dvcDefault = dvcInfoEx.DefaultLevel;
                Logger.Log($"NVIDIA DVC (extended): min={_dvcMin}, max={_dvcMax}, default={_dvcDefault}, current={dvcInfoEx.CurrentLevel}");
            }
            // Fall back to standard API
            else if (NvidiaApi.GetDVCInfo(_displayHandle, out NvDisplayDvcInfo dvcInfo) &&
                     dvcInfo.MaxLevel > dvcInfo.MinLevel)
            {
                _useExtendedApi = false;
                _dvcMin = dvcInfo.MinLevel;
                _dvcMax = dvcInfo.MaxLevel;
                _dvcDefault = AppConstants.NvidiaVibranceDefault;
                Logger.Log($"NVIDIA DVC (standard): min={_dvcMin}, max={_dvcMax}, current={dvcInfo.CurrentLevel}");
            }
            else
            {
                _useExtendedApi = false;
                _dvcMin = AppConstants.NvidiaVibranceMin;
                _dvcMax = AppConstants.NvidiaVibranceMax;
                _dvcDefault = AppConstants.NvidiaVibranceDefault;
                Logger.Log($"NVIDIA getDVCInfo failed, using fallback {_dvcMin}-{_dvcMax}");
            }

            _isSupported = true;
            Logger.Log($"NVIDIA vibrance service initialized (display: {_displayHandle}, DVC: {_dvcMin} to {_dvcMax}, extended: {_useExtendedApi})");
        }
        catch (DllNotFoundException ex)
        {
            Logger.LogError("nvapi.dll not found - NVIDIA vibrance disabled", ex);
            _isSupported = false;
        }
        catch (Exception ex)
        {
            Logger.LogError("NVIDIA vibrance initialization failed", ex);
            _isSupported = false;
        }
    }

    public bool ApplyVibrance(int value)
    {
        if (!IsSupported) return false;

        int level = MapToNvidiaRange(value, _dvcMin, _dvcMax);

        // Skip if value hasn't changed - prevents lag on gamma/contrast slider moves
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

    /// <summary>
    /// Maps app range (0-100) to NVIDIA DVC range (dvcMin to dvcMax).
    /// App 0 → dvcMin (e.g. -63 = B&W), App 100 → dvcMax (e.g. +63 = max saturation).
    /// </summary>
    internal static int MapToNvidiaRange(int appValue, int dvcMin, int dvcMax)
    {
        int clamped = Math.Clamp(appValue, AppConstants.VibranceMin, AppConstants.VibranceMax);
        return dvcMin + (clamped * (dvcMax - dvcMin)) / AppConstants.VibranceMax;
    }

    /// <summary>
    /// Maps NVIDIA DVC range (dvcMin to dvcMax) back to app range (0-100).
    /// </summary>
    internal static int MapFromNvidiaRange(int nvidiaValue, int dvcMin, int dvcMax)
    {
        if (dvcMax == dvcMin) return AppConstants.VibranceDefault;
        int clamped = Math.Clamp(nvidiaValue, dvcMin, dvcMax);
        return ((clamped - dvcMin) * AppConstants.VibranceMax) / (dvcMax - dvcMin);
    }
}
