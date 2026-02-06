using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KeyedColors.Constants;
using KeyedColors.Services.Logging;

namespace KeyedColors.Services.Display
{
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

        private RAMP originalRamp;
        private bool hasOriginalRamp;
        private bool apiAvailable = true;
        private readonly IVibranceService vibranceService;

        public bool IsApiAvailable => apiAvailable;
        public IVibranceService VibranceService => vibranceService;

        public DisplayManager(IVibranceService? vibranceService = null)
        {
            this.vibranceService = vibranceService ?? NullVibranceService.Instance;

            try
            {
                originalRamp = new RAMP
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
                        if (GetDeviceGammaRamp(hDC, ref originalRamp))
                        {
                            hasOriginalRamp = true;
                        }
                        else
                        {
                            apiAvailable = false;
                        }
                    }
                    finally
                    {
                        ReleaseDC(IntPtr.Zero, hDC);
                    }
                }
                else
                {
                    apiAvailable = false;
                }

                if (!apiAvailable)
                {
                    Logger.Log("Display API unavailable - application may need administrator privileges");
                }
            }
            catch (Exception ex)
            {
                apiAvailable = false;
                Logger.LogError("Error initializing display settings", ex);
            }
        }

        public bool ApplySettings(double gamma, double contrast, int? vibrance = null)
        {
            if (!apiAvailable)
                return false;

            try
            {
                if (gamma < AppConstants.GammaMin || gamma > AppConstants.GammaMax ||
                    contrast < AppConstants.ContrastMin || contrast > AppConstants.ContrastMax)
                    return false;

                RAMP ramp = new RAMP
                {
                    Red = new ushort[AppConstants.GammaRampSize],
                    Green = new ushort[AppConstants.GammaRampSize],
                    Blue = new ushort[AppConstants.GammaRampSize]
                };

                for (int i = 0; i < AppConstants.GammaRampSize; i++)
                {
                    double value = Math.Pow(i / 255.0, 1.0 / gamma) * AppConstants.GammaRampMaxValue;

                    // Contrast: 0 = min (0.0x), 0.5 = normal (1.0x), 1.0 = max (2.0x)
                    double adjustedContrast = contrast * 2.0;
                    value = ((value / AppConstants.GammaRampMaxValue) - 0.5) * adjustedContrast + 0.5;
                    value = Math.Max(0, Math.Min(1, value)) * AppConstants.GammaRampMaxValue;

                    ushort val = (ushort)Math.Round(value);
                    ramp.Red[i] = val;
                    ramp.Green[i] = val;
                    ramp.Blue[i] = val;
                }

                bool success = false;
                IntPtr hDC = GetDC(IntPtr.Zero);
                if (hDC != IntPtr.Zero)
                {
                    try
                    {
                        success = SetDeviceGammaRamp(hDC, ref ramp);
                    }
                    finally
                    {
                        ReleaseDC(IntPtr.Zero, hDC);
                    }
                }

                // Apply vibrance independently of gamma ramp success
                if (vibrance.HasValue)
                {
                    vibranceService.ApplyVibrance(ClampVibrance(vibrance.Value));
                }

                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to apply display settings", ex);
                return false;
            }
        }

        public bool ApplyVibrance(int vibrance)
        {
            return vibranceService.ApplyVibrance(ClampVibrance(vibrance));
        }

        public bool ResetToDefault()
        {
            if (!apiAvailable || !hasOriginalRamp)
                return false;

            try
            {
                bool success = false;
                IntPtr hDC = GetDC(IntPtr.Zero);
                if (hDC != IntPtr.Zero)
                {
                    try
                    {
                        success = SetDeviceGammaRamp(hDC, ref originalRamp);
                    }
                    finally
                    {
                        ReleaseDC(IntPtr.Zero, hDC);
                    }
                }

                vibranceService.ResetVibrance();
                return success;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to reset display to default", ex);
                return false;
            }
        }

        private int ClampVibrance(int value)
        {
            int min = vibranceService.MinValue;
            int max = vibranceService.MaxValue;
            if (min > max)
            {
                (min, max) = (0, 100);
            }

            return Math.Max(min, Math.Min(max, value));
        }

        public void Dispose()
        {
            vibranceService.Dispose();
        }
    }
}
