namespace DispHub.Core.Effects;

/// <summary>The sections of the look editor.</summary>
public enum LookGroup
{
    Colour,
    Light,
    Lens,
    Film,
    Retro,
    Comfort,
    Access,
}

/// <summary>How a value reads in the editor.</summary>
public enum LookUnit
{
    /// <summary>0 to 1 shown as 0 to 100 %.</summary>
    Percent,

    /// <summary>-1 to 1 shown as -100 to +100, with 0 in the middle.</summary>
    Balance,

    /// <summary>Stops of exposure, e.g. +0.5.</summary>
    Stops,

    Degrees,
    Pixels,
    Fps,

    /// <summary>A count where the lowest value means off (pixel size, colour levels).</summary>
    Steps,

    /// <summary>One of <see cref="LookParameter.Options"/>; for enums the value is the enum's number.</summary>
    Choice,
}

/// <summary>
/// One setting of a <see cref="Look"/>, described once so the editor, share links and tests all work from the same
/// list.
/// </summary>
public sealed class LookParameter
{
    internal LookParameter(string key, LookGroup group, LookUnit unit, double min, double max, double step,
        Func<Look, double> get, Func<Look, double, Look> set, Func<Look, bool>? appliesWhen = null, IReadOnlyList<double>? options = null)
    {
        Key = key;
        Group = group;
        Unit = unit;
        Min = min;
        Max = max;
        Step = step;
        Get = get;
        Set = set;
        AppliesWhen = appliesWhen;
        Options = options;
    }

    /// <summary>The property name in camel case; also the share link parameter and the text key suffix.</summary>
    public string Key { get; }

    public LookGroup Group { get; }

    public LookUnit Unit { get; }

    public double Min { get; }

    public double Max { get; }

    public double Step { get; }

    public Func<Look, double> Get { get; }

    /// <summary>Returns the look with this setting changed (and clamped).</summary>
    public Func<Look, double, Look> Set { get; }

    /// <summary>When the setting does something; e.g. grain size only matters with some grain. Null: always.</summary>
    public Func<Look, bool>? AppliesWhen { get; }

    /// <summary>The values a <see cref="LookUnit.Choice"/> can take.</summary>
    public IReadOnlyList<double>? Options { get; }

    public double Default => Get(Look.Neutral);

    /// <summary>
    /// An amount, which a look's strength scales (grain, saturation); not a shape or a choice (grain size, hue,
    /// palette), which stays as it is.
    /// </summary>
    public bool IsAmount => !LookParameters.Shapes.Contains(Key);

    public bool Applies(Look look) => AppliesWhen?.Invoke(look) ?? true;
}

public static class LookParameters
{
    public const double MaxStrength = 1.5;

    private static readonly double[] LetterboxOptions = [0, 1.85, 2, 2.39, 2.76];

    internal static readonly HashSet<string> Shapes =
    [
        "shadowHue", "highlightHue", "bloomThreshold", "vignetteSize", "vignetteSoftness", "grainSize", "grainFps",
        "letterbox", "scanlineSpacing", "pixelate", "posterize", "palette", "colorVision",
    ];

    public static IReadOnlyList<LookParameter> All { get; } =
    [
        // Colour
        P("exposure", LookGroup.Colour, LookUnit.Stops, -2, 2, 0.05, l => l.Exposure, (l, v) => l with { Exposure = v }),
        P("contrast", LookGroup.Colour, LookUnit.Percent, 0.5, 1.6, 0.01, l => l.Contrast, (l, v) => l with { Contrast = v }),
        P("saturation", LookGroup.Colour, LookUnit.Percent, 0, 2, 0.01, l => l.Saturation, (l, v) => l with { Saturation = v }),
        P("vibrance", LookGroup.Colour, LookUnit.Balance, -1, 1, 0.01, l => l.Vibrance, (l, v) => l with { Vibrance = v }),
        P("temperature", LookGroup.Colour, LookUnit.Balance, -1, 1, 0.01, l => l.Temperature, (l, v) => l with { Temperature = v }),
        P("tint", LookGroup.Colour, LookUnit.Balance, -1, 1, 0.01, l => l.Tint, (l, v) => l with { Tint = v }),
        P("fade", LookGroup.Colour, LookUnit.Percent, 0, 0.3, 0.01, l => l.Fade, (l, v) => l with { Fade = v }),
        P("sepia", LookGroup.Colour, LookUnit.Percent, 0, 1, 0.01, l => l.Sepia, (l, v) => l with { Sepia = v }),
        P("shadowTint", LookGroup.Colour, LookUnit.Percent, 0, 1, 0.01, l => l.ShadowTint, (l, v) => l with { ShadowTint = v }),
        P("shadowHue", LookGroup.Colour, LookUnit.Degrees, 0, 359, 1, l => l.ShadowHue, (l, v) => l with { ShadowHue = v }, l => l.ShadowTint > 0),
        P("highlightTint", LookGroup.Colour, LookUnit.Percent, 0, 1, 0.01, l => l.HighlightTint, (l, v) => l with { HighlightTint = v }),
        P("highlightHue", LookGroup.Colour, LookUnit.Degrees, 0, 359, 1, l => l.HighlightHue, (l, v) => l with { HighlightHue = v }, l => l.HighlightTint > 0),

        // Light and detail
        P("sharpen", LookGroup.Light, LookUnit.Percent, 0, 1, 0.01, l => l.Sharpen, (l, v) => l with { Sharpen = v }),
        P("bloom", LookGroup.Light, LookUnit.Percent, 0, 1, 0.01, l => l.Bloom, (l, v) => l with { Bloom = v }),
        P("halation", LookGroup.Light, LookUnit.Percent, 0, 1, 0.01, l => l.Halation, (l, v) => l with { Halation = v }),
        P("bloomThreshold", LookGroup.Light, LookUnit.Percent, 0, 0.95, 0.01, l => l.BloomThreshold, (l, v) => l with { BloomThreshold = v }, l => l.Bloom > 0 || l.Halation > 0),

        // Lens
        P("vignette", LookGroup.Lens, LookUnit.Percent, 0, 1, 0.01, l => l.Vignette, (l, v) => l with { Vignette = v }),
        P("vignetteSize", LookGroup.Lens, LookUnit.Percent, 0.3, 1.5, 0.01, l => l.VignetteSize, (l, v) => l with { VignetteSize = v }, l => l.Vignette > 0),
        P("vignetteSoftness", LookGroup.Lens, LookUnit.Percent, 0.05, 1, 0.01, l => l.VignetteSoftness, (l, v) => l with { VignetteSoftness = v }, l => l.Vignette > 0),
        P("aberration", LookGroup.Lens, LookUnit.Percent, 0, 1, 0.01, l => l.Aberration, (l, v) => l with { Aberration = v }),
        P("distortion", LookGroup.Lens, LookUnit.Percent, 0, 1, 0.01, l => l.Distortion, (l, v) => l with { Distortion = v }),

        // Film
        P("grain", LookGroup.Film, LookUnit.Percent, 0, 1, 0.01, l => l.Grain, (l, v) => l with { Grain = v }),
        P("grainSize", LookGroup.Film, LookUnit.Pixels, 1, 4, 0.1, l => l.GrainSize, (l, v) => l with { GrainSize = v }, l => l.Grain > 0),
        P("grainColor", LookGroup.Film, LookUnit.Percent, 0, 1, 0.01, l => l.GrainColor, (l, v) => l with { GrainColor = v }, l => l.Grain > 0),
        P("flicker", LookGroup.Film, LookUnit.Percent, 0, 1, 0.01, l => l.Flicker, (l, v) => l with { Flicker = v }),
        P("weave", LookGroup.Film, LookUnit.Percent, 0, 1, 0.01, l => l.Weave, (l, v) => l with { Weave = v }),
        P("dust", LookGroup.Film, LookUnit.Percent, 0, 1, 0.01, l => l.Dust, (l, v) => l with { Dust = v }),
        P("grainFps", LookGroup.Film, LookUnit.Fps, 0, 60, 1, l => l.GrainFps, (l, v) => l with { GrainFps = (int)Math.Round(v) },
            l => l.Grain > 0 || l.Flicker > 0 || l.Weave > 0 || l.Dust > 0 || l.Jitter > 0 || l.Tracking > 0),
        P("letterbox", LookGroup.Film, LookUnit.Choice, 0, 3, 0, l => l.Letterbox, (l, v) => l with { Letterbox = v }, options: LetterboxOptions),

        // Retro
        P("scanlines", LookGroup.Retro, LookUnit.Percent, 0, 1, 0.01, l => l.Scanlines, (l, v) => l with { Scanlines = v }),
        P("scanlineSpacing", LookGroup.Retro, LookUnit.Pixels, 2, 8, 1, l => l.ScanlineSpacing, (l, v) => l with { ScanlineSpacing = (int)Math.Round(v) }, l => l.Scanlines > 0),
        P("curvature", LookGroup.Retro, LookUnit.Percent, 0, 1, 0.01, l => l.Curvature, (l, v) => l with { Curvature = v }),
        P("mask", LookGroup.Retro, LookUnit.Percent, 0, 1, 0.01, l => l.Mask, (l, v) => l with { Mask = v }),
        P("chromaBleed", LookGroup.Retro, LookUnit.Percent, 0, 1, 0.01, l => l.ChromaBleed, (l, v) => l with { ChromaBleed = v }),
        P("tracking", LookGroup.Retro, LookUnit.Percent, 0, 1, 0.01, l => l.Tracking, (l, v) => l with { Tracking = v }),
        P("jitter", LookGroup.Retro, LookUnit.Percent, 0, 1, 0.01, l => l.Jitter, (l, v) => l with { Jitter = v }),
        P("pixelate", LookGroup.Retro, LookUnit.Steps, 1, 16, 1, l => Math.Max(l.Pixelate, 1), (l, v) => l with { Pixelate = v <= 1 ? 0 : (int)Math.Round(v) }),
        P("posterize", LookGroup.Retro, LookUnit.Steps, 1, 32, 1, l => Math.Max(l.Posterize, 1), (l, v) => l with { Posterize = v <= 1 ? 0 : (int)Math.Round(v) }),
        P("palette", LookGroup.Retro, LookUnit.Choice, 0, 0, 0, l => (int)l.Palette, (l, v) => l with { Palette = (RetroPalette)(int)v },
            options: [.. Enum.GetValues<RetroPalette>().Select(p => (double)(int)p)]),
        P("dither", LookGroup.Retro, LookUnit.Percent, 0, 1, 0.01, l => l.Dither, (l, v) => l with { Dither = v }, l => l.Posterize > 1 || l.Palette != RetroPalette.None),

        // Comfort
        P("dim", LookGroup.Comfort, LookUnit.Percent, 0, 0.95, 0.01, l => l.Dim, (l, v) => l with { Dim = v }),

        // Accessibility
        P("colorVision", LookGroup.Access, LookUnit.Choice, 0, 0, 0, l => (int)l.ColorVision, (l, v) => l with { ColorVision = (ColorVision)(int)v },
            options: [.. Enum.GetValues<ColorVision>().Select(c => (double)(int)c)]),
        P("colorVisionStrength", LookGroup.Access, LookUnit.Percent, 0, 1, 0.01, l => l.ColorVisionStrength, (l, v) => l with { ColorVisionStrength = v }, l => l.ColorVision != ColorVision.Typical),
    ];

    /// <summary>
    /// The look at a strength: 0 is neutral, 1 is as set, up to <see cref="MaxStrength"/> exaggerates it. Only
    /// amounts scale; shapes and choices stay (half a Game Boy is still a Game Boy palette, just with less grain).
    /// </summary>
    public static Look WithStrength(Look look, double strength)
    {
        ArgumentNullException.ThrowIfNull(look);
        strength = Math.Clamp(strength, 0, MaxStrength);
        if (Math.Abs(strength - 1) < 1e-9)
            return look;
        if (strength <= 0)
            return Look.Neutral;
        Look scaled = look;
        foreach (LookParameter parameter in All.Where(p => p.IsAmount))
            scaled = parameter.Set(scaled, parameter.Default + ((parameter.Get(look) - parameter.Default) * strength));
        return scaled;
    }

    public static LookParameter? Find(string? key) => All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The settings that differ from a neutral look.</summary>
    public static IEnumerable<LookParameter> Changed(Look look)
    {
        ArgumentNullException.ThrowIfNull(look);
        return All.Where(p => Math.Abs(p.Get(look) - p.Default) > 1e-9);
    }

    private static LookParameter P(string key, LookGroup group, LookUnit unit, double min, double max, double step,
        Func<Look, double> get, Func<Look, double, Look> set, Func<Look, bool>? appliesWhen = null, IReadOnlyList<double>? options = null) =>
        new(key, group, unit, min, max, step, get, (look, value) => set(look, value).Clamp(), appliesWhen, options);
}
