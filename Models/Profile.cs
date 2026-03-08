using System.Text.Json.Serialization;
using DisplayHub.Constants;
using System.Windows.Input;

namespace DisplayHub.Models;

[Serializable]
public class Profile
{
    private string _name = "New Profile";
    private double _gamma = AppConstants.GammaDefault;
    private double _contrast = AppConstants.ContrastDefault;
    private int _vibrance = AppConstants.VibranceDefault;

    public string Name
    {
        get => _name;
        set => _name = string.IsNullOrWhiteSpace(value) ? "New Profile" : value;
    }

    public double Gamma
    {
        get => _gamma;
        set => _gamma = Math.Clamp(value, AppConstants.GammaMin, AppConstants.GammaMax);
    }

    public double Contrast
    {
        get => _contrast;
        set => _contrast = Math.Clamp(value, AppConstants.ContrastMin, AppConstants.ContrastMax);
    }

    public int Vibrance
    {
        get => _vibrance;
        set => _vibrance = Math.Clamp(value, AppConstants.VibranceMin, AppConstants.VibranceMax);
    }

    public int HotKeyValue { get; set; } = 0;

    public uint HotKeyModifierValue { get; set; } = 0;

    public int HotkeyId { get; set; } = -1;

    [JsonIgnore]
    public string HotkeyDisplayText
    {
        get
        {
            if (HotKeyValue == 0) return "None";
            var parts = new List<string>();
            if ((HotKeyModifierValue & AppConstants.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((HotKeyModifierValue & AppConstants.MOD_ALT) != 0) parts.Add("Alt");
            if ((HotKeyModifierValue & AppConstants.MOD_SHIFT) != 0) parts.Add("Shift");
            Key key = KeyInterop.KeyFromVirtualKey(HotKeyValue);
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }
    }

    public Profile()
    {
        Name = "New Profile";
        Gamma = AppConstants.GammaDefault;
        Contrast = AppConstants.ContrastDefault;
        Vibrance = AppConstants.VibranceDefault;
        HotKeyValue = 0;
        HotKeyModifierValue = 0;
        HotkeyId = -1;
    }

    public Profile(string name, double gamma, double contrast, int vibrance = 50)
    {
        Name = name;
        Gamma = gamma;
        Contrast = contrast;
        Vibrance = vibrance;
        HotKeyValue = 0;
        HotKeyModifierValue = 0;
        HotkeyId = -1;
    }

    public override string ToString()
    {
        string hotkeyText = HotKeyValue != 0 ? HotkeyDisplayText : "No hotkey";
        return $"{Name} (Gamma: {Gamma:F2}, Contrast: {Contrast * 100:F0}%, Vibrance: {Vibrance}, Hotkey: {hotkeyText})";
    }
}
