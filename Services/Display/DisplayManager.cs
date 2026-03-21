using System.Runtime.InteropServices;
using DisplayHub.Constants;
using DisplayHub.Helpers;
using DisplayHub.Services.Logging;

namespace DisplayHub.Services.Display;

public class DisplayManager : IDisposable
{
    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP ramp);

    [DllImport("gdi32.dll")]
    private static extern bool GetDeviceGammaRamp(IntPtr hDC, ref RAMP ramp);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    private RAMP _originalRamp;
    private bool _hasOriginalRamp;
    private bool _apiAvailable = true;
    private readonly IVibranceService _vibranceService;

    public bool IsApiAvailable => _apiAvailable;
    public IVibranceService VibranceService => _vibranceService;

    public DisplayManager(IVibranceService? vibranceService = null)
    {
        _vibranceService = vibranceService ?? NullVibranceService.Instance;
        try
        {
            _originalRamp = new RAMP
            {
                Red = new ushort[AppConstants.GammaRampSize],
                Green = new ushort[AppConstants.GammaRampSize],
                Blue = new ushort[AppConstants.GammaRampSize]
            };

            IntPtr hDC = GetDC(IntPtr.Zero);
            if (hDC != IntPtr.Zero)
            {
                try
                {
                    if (GetDeviceGammaRamp(hDC, ref _originalRamp))
                        _hasOriginalRamp = true;
                    else
                        _apiAvailable = false;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, hDC);
                }
            }
            else
            {
                _apiAvailable = false;
            }

            if (!_apiAvailable)
                Logger.Log("Display API unavailable");
        }
        catch (Exception ex)
        {
            _apiAvailable = false;
            Logger.LogError("Error initializing display settings", ex);
        }
    }

    public bool ApplySettings(double gamma, double contrast, int? vibrance = null, int colorTemperature = 50)
    {
        if (!_apiAvailable) return false;
        try
        {
            if (gamma < AppConstants.GammaMin || gamma > AppConstants.GammaMax ||
                contrast < AppConstants.ContrastMin || contrast > AppConstants.ContrastMax)
                return false;

            // Convert color temperature (0-100) to RGB multipliers
            // 0 = warmest (reduce blue, boost red), 50 = neutral, 100 = coolest (reduce red, boost blue)
            var (rMult, gMult, bMult) = ColorTemperatureMapper.GetMultipliers(colorTemperature);

            RAMP ramp = new RAMP
            {
                Red = new ushort[AppConstants.GammaRampSize],
                Green = new ushort[AppConstants.GammaRampSize],
                Blue = new ushort[AppConstants.GammaRampSize]
            };

            for (int i = 0; i < AppConstants.GammaRampSize; i++)
            {
                double value = Math.Pow(i / 255.0, 1.0 / gamma) * AppConstants.GammaRampMaxValue;
                double adjustedContrast = contrast * 2.0;
                value = ((value / AppConstants.GammaRampMaxValue) - 0.5) * adjustedContrast + 0.5;
                value = Math.Max(0, Math.Min(1, value)) * AppConstants.GammaRampMaxValue;
                
                // Apply color temperature per channel
                ramp.Red[i] = (ushort)Math.Round(Math.Clamp(value * rMult, 0, AppConstants.GammaRampMaxValue));
                ramp.Green[i] = (ushort)Math.Round(Math.Clamp(value * gMult, 0, AppConstants.GammaRampMaxValue));
                ramp.Blue[i] = (ushort)Math.Round(Math.Clamp(value * bMult, 0, AppConstants.GammaRampMaxValue));
            }

            bool success = false;
            IntPtr hDC = GetDC(IntPtr.Zero);
            if (hDC != IntPtr.Zero)
            {
                try { success = SetDeviceGammaRamp(hDC, ref ramp); }
                finally { ReleaseDC(IntPtr.Zero, hDC); }
            }

            if (vibrance.HasValue)
                _vibranceService.ApplyVibrance(ClampVibrance(vibrance.Value));

            return success;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to apply display settings", ex);
            return false;
        }
    }

    public bool ApplyVibrance(int vibrance) => _vibranceService.ApplyVibrance(ClampVibrance(vibrance));

    public bool ResetToDefault()
    {
        if (!_apiAvailable || !_hasOriginalRamp) return false;
        try
        {
            bool success = false;
            IntPtr hDC = GetDC(IntPtr.Zero);
            if (hDC != IntPtr.Zero)
            {
                try { success = SetDeviceGammaRamp(hDC, ref _originalRamp); }
                finally { ReleaseDC(IntPtr.Zero, hDC); }
            }
            _vibranceService.ResetVibrance();
            return success;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to reset display", ex);
            return false;
        }
    }

    private int ClampVibrance(int value)
    {
        int min = _vibranceService.MinValue;
        int max = _vibranceService.MaxValue;
        if (min > max) (min, max) = (0, 100);
        return Math.Max(min, Math.Min(max, value));
    }

    public void Dispose() => _vibranceService.Dispose();
}
