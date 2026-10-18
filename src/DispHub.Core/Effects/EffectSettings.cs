using System.Text.Json.Serialization;

namespace DispHub.Core.Effects;

/// <summary>
/// A screen effect: every setting of DispHub's effect shader. Amounts run from 0 (off) upwards, so a default Look
/// changes nothing. Looks that only add texture (grain, vignette, scanlines...) are drawn over the screen with no
/// delay; anything that reads the picture (colour, blur, lens, VHS) processes a captured copy of the screen.
/// </summary>
public sealed record Look
{
    /// <summary>A look that changes nothing.</summary>
    public static Look Neutral { get; } = new();

    // Colour
    public double Exposure { get; init; }
    public double Contrast { get; init; } = 1;
    public double Saturation { get; init; } = 1;
    public double Vibrance { get; init; }
    public double Temperature { get; init; }
    public double Tint { get; init; }
    public double Fade { get; init; }
    public double Sepia { get; init; }
    public double ShadowHue { get; init; } = 200;
    public double ShadowTint { get; init; }
    public double HighlightHue { get; init; } = 35;
    public double HighlightTint { get; init; }

    // Light and detail
    public double Sharpen { get; init; }
    public double Bloom { get; init; }
    public double BloomThreshold { get; init; } = 0.7;
    public double Halation { get; init; }

    // Lens
    public double Aberration { get; init; }
    public double Distortion { get; init; }
    public double Vignette { get; init; }
    public double VignetteSize { get; init; } = 0.8;
    public double VignetteSoftness { get; init; } = 0.5;

    // Film
    public double Grain { get; init; }
    public double GrainSize { get; init; } = 1.5;
    public double GrainColor { get; init; }
    /// <summary>How often grain, flicker, dust and tape noise change, per second; 0 holds them still.</summary>
    public int GrainFps { get; init; } = 24;
    public double Flicker { get; init; }
    public double Weave { get; init; }
    public double Dust { get; init; }
    public double Letterbox { get; init; }

    // Retro
    public double Scanlines { get; init; }
    public int ScanlineSpacing { get; init; } = 3;
    public double Curvature { get; init; }
    public double Mask { get; init; }
    public double ChromaBleed { get; init; }
    public double Tracking { get; init; }
    public double Jitter { get; init; }
    public int Pixelate { get; init; }
    public int Posterize { get; init; }
    public RetroPalette Palette { get; init; }

    /// <summary>Ordered dithering of the posterized or palette colours, from 0 (none) to 1.</summary>
    public double Dither { get; init; }

    // Comfort

    /// <summary>Darkens the screen beyond what its own brightness setting allows (0 to 0.95).</summary>
    public double Dim { get; init; }

    // Accessibility
    public ColorVision ColorVision { get; init; }
    public double ColorVisionStrength { get; init; } = 1;

    /// <summary>True when the look reads the screen's pixels, so it needs a captured copy (one frame of delay).</summary>
    [JsonIgnore]
    public bool NeedsCapture =>
        Exposure != 0 || Contrast != 1 || Saturation != 1 || Vibrance != 0 || Temperature != 0 || Tint != 0 ||
        Fade > 0 || Sepia > 0 || ShadowTint > 0 || HighlightTint > 0 || Sharpen > 0 || Bloom > 0 || Halation > 0 ||
        Aberration > 0 || Distortion > 0 || Curvature > 0 || ChromaBleed > 0 || Jitter > 0 || Tracking > 0 || Weave > 0 ||
        Pixelate > 1 || Posterize > 1 || Palette != RetroPalette.None || ColorVision != ColorVision.Typical;

    /// <summary>
    /// True when it moves (grain, flicker, VHS noise), so it is redrawn even on a still screen, <see cref="GrainFps"/>
    /// times a second.
    /// </summary>
    [JsonIgnore]
    public bool IsAnimated => GrainFps > 0 && (Grain > 0 || Flicker > 0 || Dust > 0 || Tracking > 0 || Jitter > 0 || Weave > 0);

    [JsonIgnore]
    public bool IsEmpty => !NeedsCapture && !HasLayers;

    /// <summary>
    /// True when part of it can be drawn on top of the live screen without reading it: grain, dust, tape snow,
    /// vignette, scanlines and mask (as shading), flicker, dimming, fading and letterbox bars.
    /// </summary>
    [JsonIgnore]
    public bool HasLayers => Grain > 0 || Vignette > 0 || Flicker > 0 || Dust > 0 || Letterbox > 0 || Scanlines > 0 ||
        Mask > 0 || Tracking > 0 || Dim > 0 || Fade > 0;

    public Look Clamp() => this with
    {
        Exposure = Math.Clamp(Exposure, -2, 2),
        Contrast = Math.Clamp(Contrast, 0.5, 1.6),
        Saturation = Math.Clamp(Saturation, 0, 2),
        Vibrance = Math.Clamp(Vibrance, -1, 1),
        Temperature = Math.Clamp(Temperature, -1, 1),
        Tint = Math.Clamp(Tint, -1, 1),
        Fade = Math.Clamp(Fade, 0, 0.3),
        Sepia = Math.Clamp(Sepia, 0, 1),
        ShadowHue = Hue(ShadowHue),
        ShadowTint = Math.Clamp(ShadowTint, 0, 1),
        HighlightHue = Hue(HighlightHue),
        HighlightTint = Math.Clamp(HighlightTint, 0, 1),
        Sharpen = Math.Clamp(Sharpen, 0, 1),
        Bloom = Math.Clamp(Bloom, 0, 1),
        BloomThreshold = Math.Clamp(BloomThreshold, 0, 0.95),
        Halation = Math.Clamp(Halation, 0, 1),
        Aberration = Math.Clamp(Aberration, 0, 1),
        Distortion = Math.Clamp(Distortion, 0, 1),
        Vignette = Math.Clamp(Vignette, 0, 1),
        VignetteSize = Math.Clamp(VignetteSize, 0.3, 1.5),
        VignetteSoftness = Math.Clamp(VignetteSoftness, 0.05, 1),
        Grain = Math.Clamp(Grain, 0, 1),
        GrainSize = Math.Clamp(GrainSize, 1, 4),
        GrainColor = Math.Clamp(GrainColor, 0, 1),
        GrainFps = Math.Clamp(GrainFps, 0, 60),
        Flicker = Math.Clamp(Flicker, 0, 1),
        Weave = Math.Clamp(Weave, 0, 1),
        Dust = Math.Clamp(Dust, 0, 1),
        Letterbox = Letterbox > 0 ? Math.Clamp(Letterbox, 1.2, 3) : 0,
        Scanlines = Math.Clamp(Scanlines, 0, 1),
        ScanlineSpacing = Math.Clamp(ScanlineSpacing, 2, 8),
        Curvature = Math.Clamp(Curvature, 0, 1),
        Mask = Math.Clamp(Mask, 0, 1),
        ChromaBleed = Math.Clamp(ChromaBleed, 0, 1),
        Tracking = Math.Clamp(Tracking, 0, 1),
        Jitter = Math.Clamp(Jitter, 0, 1),
        Pixelate = Pixelate > 1 ? Math.Clamp(Pixelate, 2, 16) : 0,
        Posterize = Posterize > 1 ? Math.Clamp(Posterize, 2, 32) : 0,
        Palette = Enum.IsDefined(Palette) ? Palette : RetroPalette.None,
        Dither = Math.Clamp(Dither, 0, 1),
        Dim = Math.Clamp(Dim, 0, 0.95),
        ColorVision = Enum.IsDefined(ColorVision) ? ColorVision : ColorVision.Typical,
        ColorVisionStrength = Math.Clamp(ColorVisionStrength, 0, 1),
    };

    private static double Hue(double hue) => ((hue % 360) + 360) % 360;
}

/// <summary>Colour vision correction: shifts colours a viewer can't tell apart towards ones they can.</summary>
public enum ColorVision
{
    Typical,
    RedWeak,
    GreenWeak,
    BlueWeak,
}

public sealed class EffectsConfig
{
    public bool Enabled { get; set; }

    /// <summary>The built-in look or saved look (its id) the current settings started from, if any.</summary>
    public string? Preset { get; set; }

    public Look Look { get; set; } = new();

    /// <summary>How strongly <see cref="Look"/> shows, from 0 to 1.5 (see <see cref="LookParameters.WithStrength"/>).</summary>
    public double Strength { get; set; } = 1;

    /// <summary>The look as it shows on screen: <see cref="Look"/> at <see cref="Strength"/>.</summary>
    [JsonIgnore]
    public Look ShownLook => LookParameters.WithStrength(Look, Strength);

    /// <summary>Looks the user saved, shown with the built-in ones.</summary>
    public List<SavedLook> SavedLooks { get; set; } = [];

    /// <summary>
    /// Most frames per second for looks that process the screen, on the desktop; 0 follows the display. A full-screen
    /// game always gets every frame the display shows.
    /// </summary>
    public int MaxFps { get; set; }

    /// <summary>
    /// No effects while a full-screen game or video is in front. Otherwise they show there too, at every frame the
    /// display can show.
    /// </summary>
    public bool HideOverFullScreen { get; set; }

    /// <summary>Only the main display; otherwise every display.</summary>
    public bool MainDisplayOnly { get; set; }
}

/// <summary>A look the user saved under a name.</summary>
public sealed class SavedLook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public Look Look { get; set; } = new();

    /// <summary>The built-in look it was made from, which decides whether it needs Pro.</summary>
    public string? BasedOn { get; set; }

    /// <summary>The id used in <see cref="EffectsConfig.Preset"/> and in profiles.</summary>
    public string Key => "saved:" + Id.ToString("N");
}
