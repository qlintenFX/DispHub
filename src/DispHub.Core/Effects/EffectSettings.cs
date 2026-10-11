using System.Text.Json.Serialization;

namespace DispHub.Core.Effects;

/// <summary>
/// A colour grade for the whole screen, applied by Windows as a 5x5 colour matrix after everything is drawn.
/// Unlike profiles (gamma ramps), it can mix colour channels, so it can desaturate, tone and tint.
/// </summary>
public sealed record LookSettings(
    double Saturation = 1,
    double Contrast = 1,
    double Fade = 0,
    double Sepia = 0,
    double TintHue = 35,
    double TintStrength = 0)
{
    [JsonIgnore]
    public bool IsNeutral =>
        Math.Abs(Saturation - 1) < 1e-6 && Math.Abs(Contrast - 1) < 1e-6 && Fade < 1e-6 && Sepia < 1e-6 && TintStrength < 1e-6;

    public LookSettings Clamp() => new(
        Math.Clamp(Saturation, 0, 2),
        Math.Clamp(Contrast, 0.5, 1.5),
        Math.Clamp(Fade, 0, 0.3),
        Math.Clamp(Sepia, 0, 1),
        ((TintHue % 360) + 360) % 360,
        Math.Clamp(TintStrength, 0, 1));
}

/// <summary>
/// Texture drawn over the screen: film grain, vignette, scanlines, flicker and letterbox bars.
/// Sizes are in physical pixels, amounts from 0 (off) to 1.
/// </summary>
public sealed record OverlaySettings(
    double Grain = 0,
    double GrainSize = 1.5,
    int GrainFps = 24,
    double Vignette = 0,
    double VignetteSize = 0.8,
    double VignetteSoftness = 0.5,
    double Scanlines = 0,
    int ScanlineSpacing = 3,
    double Flicker = 0,
    double LetterboxAspect = 0)
{
    [JsonIgnore]
    public bool IsEmpty => Grain < 1e-6 && Vignette < 1e-6 && Scanlines < 1e-6 && Flicker < 1e-6 && LetterboxAspect <= 0;

    /// <summary>Grain and flicker change every frame; the rest is drawn once.</summary>
    [JsonIgnore]
    public bool IsAnimated => (Grain > 1e-6 && GrainFps > 0) || Flicker > 1e-6;

    public OverlaySettings Clamp() => new(
        Math.Clamp(Grain, 0, 1),
        Math.Clamp(GrainSize, 1, 4),
        Math.Clamp(GrainFps, 0, 30),
        Math.Clamp(Vignette, 0, 1),
        Math.Clamp(VignetteSize, 0.3, 1.5),
        Math.Clamp(VignetteSoftness, 0.05, 1),
        Math.Clamp(Scanlines, 0, 1),
        Math.Clamp(ScanlineSpacing, 2, 8),
        Math.Clamp(Flicker, 0, 1),
        LetterboxAspect > 0 ? Math.Clamp(LetterboxAspect, 1.2, 3) : 0);
}

public sealed class EffectsConfig
{
    public bool Enabled { get; set; }

    /// <summary>The preset the settings started from, if any.</summary>
    public string? Preset { get; set; }

    public LookSettings Look { get; set; } = new();
    public OverlaySettings Overlay { get; set; } = new();

    /// <summary>Hide the overlay while a full-screen app or game is in front, so it can't add latency there.</summary>
    public bool HideOverFullScreen { get; set; } = true;

    /// <summary>Keep grain and the rest out of screenshots and recordings. The colour grade always shows in them.</summary>
    public bool HideOverlayFromCapture { get; set; }
}

public sealed record EffectPreset(string Id, LookSettings Look, OverlaySettings Overlay);

/// <summary>Starting points; every value stays adjustable.</summary>
public static class EffectPresets
{
    public static IReadOnlyList<EffectPreset> All { get; } =
    [
        new("film", new LookSettings(Saturation: 0.85, Contrast: 1.05, Fade: 0.06, TintHue: 38, TintStrength: 0.06),
            new OverlaySettings(Grain: 0.22, GrainSize: 1.6, Vignette: 0.3, Flicker: 0.08)),
        new("blackAndWhite", new LookSettings(Saturation: 0, Contrast: 1.12, Fade: 0.03),
            new OverlaySettings(Grain: 0.3, GrainSize: 1.8, Vignette: 0.35, Flicker: 0.1)),
        new("sepia", new LookSettings(Saturation: 0, Fade: 0.05, Sepia: 1),
            new OverlaySettings(Grain: 0.2, GrainSize: 1.6, Vignette: 0.45, VignetteSoftness: 0.6)),
        new("camcorder", new LookSettings(Saturation: 0.8, Contrast: 0.95, Fade: 0.07, TintHue: 300, TintStrength: 0.05),
            new OverlaySettings(Grain: 0.18, GrainSize: 1.2, GrainFps: 30, Scanlines: 0.16, ScanlineSpacing: 3)),
        new("bodycam", new LookSettings(Saturation: 0.6, Contrast: 1.1, Fade: 0.02, TintHue: 195, TintStrength: 0.1),
            new OverlaySettings(Grain: 0.25, GrainSize: 1.3, GrainFps: 30, Vignette: 0.55, VignetteSize: 0.7)),
        new("nightVision", new LookSettings(Saturation: 0, Contrast: 1.2, Fade: 0.04, TintHue: 120, TintStrength: 1),
            new OverlaySettings(Grain: 0.35, GrainSize: 1.4, GrainFps: 30, Vignette: 0.6, VignetteSize: 0.65, Scanlines: 0.1)),
        new("cinema", new LookSettings(Saturation: 0.92, Contrast: 1.06, TintHue: 30, TintStrength: 0.04),
            new OverlaySettings(Grain: 0.1, Vignette: 0.2, LetterboxAspect: 2.39)),
    ];
}
