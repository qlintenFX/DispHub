using DisplayHub.Constants;
using DisplayHub.Services.Display;

namespace DisplayHub.Services.Hotkeys;

public class DynamicControls
{
    public double Gamma { get; private set; }
    public double Contrast { get; private set; }
    public int Vibrance { get; private set; }
    public bool IsEnabled { get; set; }

    public event EventHandler? ValuesChanged;

    private readonly DisplayManager _displayManager;

    private int _hotkeyIdGammaUp = -1;
    private int _hotkeyIdGammaDown = -1;
    private int _hotkeyIdContrastUp = -1;
    private int _hotkeyIdContrastDown = -1;
    private int _hotkeyIdVibranceUp = -1;
    private int _hotkeyIdVibranceDown = -1;

    // Virtual key codes for arrow keys (no WinForms dependency)
    private const uint VK_UP = 0x26;
    private const uint VK_DOWN = 0x28;
    private const uint VK_LEFT = 0x25;
    private const uint VK_RIGHT = 0x27;
    private const uint VK_PRIOR = 0x21; // Page Up
    private const uint VK_NEXT = 0x22;  // Page Down

    public DynamicControls(DisplayManager displayManager)
    {
        _displayManager = displayManager;
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
            _displayManager.ApplySettings(Gamma, Contrast, Vibrance);
    }

    public void SetValues(double gamma, double contrast, int? vibrance = null)
    {
        Gamma = Math.Clamp(gamma, AppConstants.GammaMin, AppConstants.GammaMax);
        Contrast = Math.Clamp(contrast, AppConstants.ContrastMin, AppConstants.ContrastMax);
        if (vibrance.HasValue)
            Vibrance = Math.Clamp(vibrance.Value, AppConstants.VibranceMin, AppConstants.VibranceMax);
        if (IsEnabled)
        {
            ApplySettings();
            ValuesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RegisterHotkeys(HotkeyManager hotkeyManager)
    {
        UnregisterHotkeys(hotkeyManager);
        _hotkeyIdGammaUp = hotkeyManager.RegisterRawHotkey(VK_UP, AppConstants.MOD_SHIFT);
        _hotkeyIdGammaDown = hotkeyManager.RegisterRawHotkey(VK_DOWN, AppConstants.MOD_SHIFT);
        _hotkeyIdContrastUp = hotkeyManager.RegisterRawHotkey(VK_RIGHT, AppConstants.MOD_SHIFT);
        _hotkeyIdContrastDown = hotkeyManager.RegisterRawHotkey(VK_LEFT, AppConstants.MOD_SHIFT);
        _hotkeyIdVibranceUp = hotkeyManager.RegisterRawHotkey(VK_PRIOR, AppConstants.MOD_SHIFT);
        _hotkeyIdVibranceDown = hotkeyManager.RegisterRawHotkey(VK_NEXT, AppConstants.MOD_SHIFT);
    }

    public void UnregisterHotkeys(HotkeyManager hotkeyManager)
    {
        int[] ids = { _hotkeyIdGammaUp, _hotkeyIdGammaDown, _hotkeyIdContrastUp,
                      _hotkeyIdContrastDown, _hotkeyIdVibranceUp, _hotkeyIdVibranceDown };
        foreach (int id in ids)
            if (id > 0) hotkeyManager.UnregisterRawHotkey(id);

        _hotkeyIdGammaUp = _hotkeyIdGammaDown = _hotkeyIdContrastUp =
        _hotkeyIdContrastDown = _hotkeyIdVibranceUp = _hotkeyIdVibranceDown = -1;
    }

    public bool ProcessHotkey(int id)
    {
        if (!IsEnabled) return false;
        if (id == _hotkeyIdGammaUp) { AdjustGamma(AppConstants.GammaStep); return true; }
        if (id == _hotkeyIdGammaDown) { AdjustGamma(-AppConstants.GammaStep); return true; }
        if (id == _hotkeyIdContrastUp) { AdjustContrast(AppConstants.ContrastStep); return true; }
        if (id == _hotkeyIdContrastDown) { AdjustContrast(-AppConstants.ContrastStep); return true; }
        if (id == _hotkeyIdVibranceUp) { AdjustVibrance(AppConstants.VibranceStep); return true; }
        if (id == _hotkeyIdVibranceDown) { AdjustVibrance(-AppConstants.VibranceStep); return true; }
        return false;
    }
}
