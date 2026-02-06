using System;
using System.Windows.Forms;
using KeyedColors.Constants;
using KeyedColors.Services.Display;

namespace KeyedColors.Services.Hotkeys
{
    public class DynamicControls
    {
        public double Gamma { get; private set; }
        public double Contrast { get; private set; }
        public int Vibrance { get; private set; }
        public bool IsEnabled { get; set; }

        public event EventHandler? ValuesChanged;

        private readonly DisplayManager displayManager;

        // Hotkey IDs for the 6 directional adjustments
        private int hotkeyIdGammaUp = -1;
        private int hotkeyIdGammaDown = -1;
        private int hotkeyIdContrastUp = -1;
        private int hotkeyIdContrastDown = -1;
        private int hotkeyIdVibranceUp = -1;
        private int hotkeyIdVibranceDown = -1;

        public DynamicControls(DisplayManager displayManager)
        {
            this.displayManager = displayManager;
            Gamma = AppConstants.GammaDefault;
            Contrast = AppConstants.ContrastDefault;
            Vibrance = AppConstants.VibranceDefault;
            IsEnabled = false;
        }

        public void AdjustGamma(double delta)
        {
            if (!IsEnabled) return;

            double newGamma = Math.Clamp(Gamma + delta, AppConstants.GammaMin, AppConstants.GammaMax);
            if (Math.Abs(newGamma - Gamma) > double.Epsilon)
            {
                Gamma = newGamma;
                ApplySettings();
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void AdjustContrast(double delta)
        {
            if (!IsEnabled) return;

            double newContrast = Math.Clamp(Contrast + delta, AppConstants.ContrastMin, AppConstants.ContrastMax);
            if (Math.Abs(newContrast - Contrast) > double.Epsilon)
            {
                Contrast = newContrast;
                ApplySettings();
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void AdjustVibrance(int delta)
        {
            if (!IsEnabled) return;

            int newValue = Math.Clamp(Vibrance + delta, AppConstants.VibranceMin, AppConstants.VibranceMax);
            if (newValue != Vibrance)
            {
                Vibrance = newValue;
                ApplySettings();
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ApplySettings()
        {
            if (IsEnabled)
            {
                displayManager.ApplySettings(Gamma, Contrast, Vibrance);
            }
        }

        public void SetValues(double gamma, double contrast, int? vibrance = null)
        {
            Gamma = Math.Clamp(gamma, AppConstants.GammaMin, AppConstants.GammaMax);
            Contrast = Math.Clamp(contrast, AppConstants.ContrastMin, AppConstants.ContrastMax);
            if (vibrance.HasValue)
            {
                Vibrance = Math.Clamp(vibrance.Value, AppConstants.VibranceMin, AppConstants.VibranceMax);
            }

            if (IsEnabled)
            {
                ApplySettings();
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RegisterHotkeys(HotkeyManager hotkeyManager, IntPtr formHandle)
        {
            UnregisterHotkeys(hotkeyManager);

            hotkeyIdGammaUp = hotkeyManager.RegisterRawHotkey(Keys.Up, AppConstants.MOD_SHIFT);
            hotkeyIdGammaDown = hotkeyManager.RegisterRawHotkey(Keys.Down, AppConstants.MOD_SHIFT);
            hotkeyIdContrastUp = hotkeyManager.RegisterRawHotkey(Keys.Right, AppConstants.MOD_SHIFT);
            hotkeyIdContrastDown = hotkeyManager.RegisterRawHotkey(Keys.Left, AppConstants.MOD_SHIFT);
            hotkeyIdVibranceUp = hotkeyManager.RegisterRawHotkey(Keys.PageUp, AppConstants.MOD_SHIFT);
            hotkeyIdVibranceDown = hotkeyManager.RegisterRawHotkey(Keys.PageDown, AppConstants.MOD_SHIFT);
        }

        public void UnregisterHotkeys(HotkeyManager hotkeyManager)
        {
            int[] ids = { hotkeyIdGammaUp, hotkeyIdGammaDown, hotkeyIdContrastUp,
                          hotkeyIdContrastDown, hotkeyIdVibranceUp, hotkeyIdVibranceDown };

            foreach (int id in ids)
            {
                if (id > 0)
                {
                    hotkeyManager.UnregisterRawHotkey(id);
                }
            }

            hotkeyIdGammaUp = -1;
            hotkeyIdGammaDown = -1;
            hotkeyIdContrastUp = -1;
            hotkeyIdContrastDown = -1;
            hotkeyIdVibranceUp = -1;
            hotkeyIdVibranceDown = -1;
        }

        public bool ProcessHotkey(IntPtr wParam)
        {
            if (!IsEnabled) return false;

            int id = wParam.ToInt32();

            if (id == hotkeyIdGammaUp) { AdjustGamma(AppConstants.GammaStep); return true; }
            if (id == hotkeyIdGammaDown) { AdjustGamma(-AppConstants.GammaStep); return true; }
            if (id == hotkeyIdContrastUp) { AdjustContrast(AppConstants.ContrastStep); return true; }
            if (id == hotkeyIdContrastDown) { AdjustContrast(-AppConstants.ContrastStep); return true; }
            if (id == hotkeyIdVibranceUp) { AdjustVibrance(AppConstants.VibranceStep); return true; }
            if (id == hotkeyIdVibranceDown) { AdjustVibrance(-AppConstants.VibranceStep); return true; }

            return false;
        }
    }
}
