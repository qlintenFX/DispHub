// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Serialization;
using DispHub.Constants;
using System.Windows.Input;

namespace DispHub.Models;

public class Profile
{
    private string _name = "New Profile";
    private double _gamma = AppConstants.GammaDefault;
    private double _contrast = AppConstants.ContrastDefault;
    private int _vibrance = AppConstants.VibranceDefault;
    private int _colorTemperature = AppConstants.ColorTempDefault;

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

    /// <summary>
    /// Color temperature: 0 = warmest (night light), 50 = neutral, 100 = coolest
    /// </summary>
    public int ColorTemperature
    {
        get => _colorTemperature;
        set => _colorTemperature = Math.Clamp(value, AppConstants.ColorTempMin, AppConstants.ColorTempMax);
    }

    public int HotKeyValue { get; set; }

    public uint HotKeyModifierValue { get; set; }

    /// <summary>Runtime-only hotkey registration ID - not persisted.</summary>
    [JsonIgnore]
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

    public Profile() { }

    public Profile(string name, double gamma = AppConstants.GammaDefault,
                   double contrast = AppConstants.ContrastDefault,
                   int vibrance = AppConstants.VibranceDefault,
                   int colorTemperature = AppConstants.ColorTempDefault)
    {
        Name = name;
        Gamma = gamma;
        Contrast = contrast;
        Vibrance = vibrance;
        ColorTemperature = colorTemperature;
    }
}
