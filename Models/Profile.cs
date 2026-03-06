using System;
using System.Text.Json.Serialization;
using DisplayHub.Constants;

namespace DisplayHub.Models
{
    [Serializable]
    public class Profile
    {
        private string name = "New Profile";
        private double gamma = AppConstants.GammaDefault;
        private double contrast = AppConstants.ContrastDefault;
        private int vibrance = AppConstants.VibranceDefault;

        public string Name
        {
            get => name;
            set => name = string.IsNullOrWhiteSpace(value) ? "New Profile" : value;
        }

        public double Gamma
        {
            get => gamma;
            set => gamma = Math.Clamp(value, AppConstants.GammaMin, AppConstants.GammaMax);
        }

        public double Contrast
        {
            get => contrast;
            set => contrast = Math.Clamp(value, AppConstants.ContrastMin, AppConstants.ContrastMax);
        }

        public int Vibrance
        {
            get => vibrance;
            set => vibrance = Math.Clamp(value, AppConstants.VibranceMin, AppConstants.VibranceMax);
        }

        /// <summary>Virtual key code for the hotkey (0 = None).</summary>
        [JsonIgnore]
        public int HotKey { get; set; }

        /// <summary>Modifier key flags (0 = None).</summary>
        [JsonIgnore]
        public int HotKeyModifier { get; set; }

        public int HotKeyValue
        {
            get => HotKey;
            set => HotKey = value;
        }

        public int HotKeyModifierValue
        {
            get => HotKeyModifier;
            set => HotKeyModifier = value;
        }

        public int HotkeyId { get; set; }

        public Profile()
        {
            Name = "New Profile";
            Gamma = AppConstants.GammaDefault;
            Contrast = AppConstants.ContrastDefault;
            Vibrance = AppConstants.VibranceDefault;
            HotKey = 0;
            HotKeyModifier = 0;
            HotkeyId = -1;
        }

        public Profile(string name, double gamma, double contrast, int vibrance = 50)
        {
            Name = name;
            Gamma = gamma;
            Contrast = contrast;
            Vibrance = vibrance;
            HotKey = 0;
            HotKeyModifier = 0;
            HotkeyId = -1;
        }

        /// <summary>
        /// Returns a new Profile with updated display settings.
        /// </summary>
        public Profile WithSettings(double newGamma, double newContrast, int newVibrance)
        {
            return new Profile(Name, newGamma, newContrast, newVibrance)
            {
                HotKey = HotKey,
                HotKeyModifier = HotKeyModifier,
                HotkeyId = HotkeyId
            };
        }

        /// <summary>
        /// Returns a new Profile with an updated hotkey binding.
        /// </summary>
        public Profile WithHotkey(int key, int modifier, int id)
        {
            return new Profile(Name, Gamma, Contrast, Vibrance)
            {
                HotKey = key,
                HotKeyModifier = modifier,
                HotkeyId = id
            };
        }

        public override string ToString()
        {
            string hotkeyText = HotKey != 0
                ? $"Modifier+VK{HotKey}"
                : "No hotkey";

            return $"{Name} (Gamma: {Gamma:F2}, Contrast: {Contrast * 100:F0}%, Vibrance: {Vibrance}%, Hotkey: {hotkeyText})";
        }
    }
}
