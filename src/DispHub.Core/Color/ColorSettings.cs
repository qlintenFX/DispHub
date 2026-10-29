// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json.Serialization;

namespace DispHub.Core.Color;

/// <summary>
/// One set of display adjustments. Brightness and contrast are offsets where 0 is neutral;
/// vibrance is a percentage where 50 is the driver default (same as NVIDIA Control Panel); shadow boost lifts dark
/// tones, 0 to 100, like a monitor's black equaliser.
/// </summary>
public sealed record ColorSettings(
    int Brightness = 0,
    int Contrast = 0,
    double Gamma = 1.0,
    int TemperatureK = ColorSettings.NeutralTemperatureK,
    int Vibrance = ColorSettings.NeutralVibrance,
    int ShadowBoost = 0)
{
    public const int BrightnessMin = -50;
    public const int BrightnessMax = 50;
    public const int ContrastMin = -50;
    public const int ContrastMax = 50;
    public const double GammaMin = 0.3;
    public const double GammaMax = 2.8;
    public const int TemperatureMinK = 2000;
    public const int TemperatureMaxK = 10000;
    public const int NeutralTemperatureK = 6500;
    public const int VibranceMin = 0;
    public const int VibranceMax = 100;
    public const int NeutralVibrance = 50;
    public const int ShadowBoostMax = 100;

    public static ColorSettings Neutral { get; } = new();

    /// <summary>True when the gamma ramp part of these settings is the identity ramp.</summary>
    [JsonIgnore]
    public bool HasNeutralRamp =>
        Brightness == 0 && Contrast == 0 && Math.Abs(Gamma - 1.0) < 0.0005 && TemperatureK == NeutralTemperatureK && ShadowBoost == 0;

    public ColorSettings Clamp() => new(
        Math.Clamp(Brightness, BrightnessMin, BrightnessMax),
        Math.Clamp(Contrast, ContrastMin, ContrastMax),
        double.IsFinite(Gamma) ? Math.Round(Math.Clamp(Gamma, GammaMin, GammaMax), 2) : 1.0,
        Math.Clamp(TemperatureK, TemperatureMinK, TemperatureMaxK),
        Math.Clamp(Vibrance, VibranceMin, VibranceMax),
        Math.Clamp(ShadowBoost, 0, ShadowBoostMax));
}
