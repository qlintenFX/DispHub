namespace DispHub.Core.Effects;

public enum LookCategory
{
    Film,
    Camera,
    Retro,
    Colour,
    Comfort,
    Access,
}

/// <summary>A ready-made look; its name and description come from the app's text (Look_{Id}, Look_{Id}Hint).</summary>
public sealed record LookPreset(string Id, LookCategory Category, Look Look);

/// <summary>
/// The built-in looks. Each is a starting point: every value stays adjustable. Tuned to read as the real medium
/// (print film, Super 8, VHS, a CRT) rather than a cartoon of it.
/// </summary>
public static class Looks
{
    public static IReadOnlyList<LookPreset> All { get; } =
    [
        // Film
        new("film", LookCategory.Film, new Look
        {
            Contrast = 1.06, Saturation = 0.92, Fade = 0.04, Temperature = 0.06,
            ShadowHue = 195, ShadowTint = 0.12, HighlightHue = 35, HighlightTint = 0.1,
            Halation = 0.25, Grain = 0.28, GrainSize = 1.4, Vignette = 0.22, Weave = 0.12, Flicker = 0.08,
        }),
        new("super8", LookCategory.Film, new Look
        {
            Contrast = 1.08, Saturation = 0.82, Fade = 0.09, Temperature = 0.25, Bloom = 0.15, BloomThreshold = 0.6,
            Halation = 0.3, Grain = 0.55, GrainSize = 2.2, GrainColor = 0.3, GrainFps = 18, Vignette = 0.55,
            VignetteSize = 0.75, Weave = 0.6, Flicker = 0.4, Dust = 0.35,
        }),
        new("blackAndWhite", LookCategory.Film, new Look
        {
            Saturation = 0, Contrast = 1.16, Fade = 0.02, Grain = 0.36, GrainSize = 1.6, Vignette = 0.35,
            Flicker = 0.12, Dust = 0.12,
        }),
        new("sepia", LookCategory.Film, new Look
        {
            Saturation = 0.15, Sepia = 1, Fade = 0.08, Contrast = 1.05, Grain = 0.25, GrainSize = 1.6,
            Vignette = 0.5, VignetteSoftness = 0.65, Dust = 0.25, Flicker = 0.15,
        }),
        new("bleachBypass", LookCategory.Film, new Look
        {
            Saturation = 0.45, Contrast = 1.32, Sharpen = 0.2, Grain = 0.18, Vignette = 0.2,
        }),
        new("tealOrange", LookCategory.Film, new Look
        {
            Contrast = 1.08, Saturation = 1.05, ShadowHue = 190, ShadowTint = 0.35, HighlightHue = 30, HighlightTint = 0.3,
            Vignette = 0.2, Grain = 0.1, Letterbox = 2.39,
        }),

        // Camera
        new("vhs", LookCategory.Camera, new Look
        {
            Saturation = 0.85, Contrast = 0.94, Fade = 0.06, Temperature = -0.05, Tint = 0.06, Bloom = 0.2,
            ChromaBleed = 0.6, Tracking = 0.3, Jitter = 0.25, Scanlines = 0.12, Grain = 0.2, GrainSize = 1.2,
            GrainColor = 0.6, GrainFps = 30,
        }),
        new("bodycam", LookCategory.Camera, new Look
        {
            Exposure = 0.1, Contrast = 1.12, Saturation = 0.72, Temperature = -0.15, Sharpen = 0.35, Bloom = 0.2,
            Distortion = 0.45, Aberration = 0.35, Vignette = 0.55, VignetteSize = 0.7, Grain = 0.25, GrainSize = 1.3,
            GrainFps = 30,
        }),
        new("security", LookCategory.Camera, new Look
        {
            Saturation = 0.12, Contrast = 1.2, Tint = -0.15, Sharpen = 0.3, Distortion = 0.25, Scanlines = 0.15,
            Grain = 0.4, GrainSize = 1.3, GrainFps = 15, Vignette = 0.3,
        }),
        new("nightVision", LookCategory.Camera, new Look
        {
            Exposure = 0.3, Saturation = 0, Contrast = 1.15, ShadowHue = 125, ShadowTint = 0.85, HighlightHue = 100,
            HighlightTint = 0.45, Bloom = 0.3, BloomThreshold = 0.6, Grain = 0.45, GrainSize = 1.4, GrainFps = 30,
            Vignette = 0.65, VignetteSize = 0.65, Scanlines = 0.1, Distortion = 0.1,
        }),
        new("cinema", LookCategory.Camera, new Look
        {
            Contrast = 1.06, Saturation = 0.95, Grain = 0.12, Vignette = 0.2, Letterbox = 2.39,
        }),

        // Retro
        new("arcadeCrt", LookCategory.Retro, new Look
        {
            Saturation = 1.15, Bloom = 0.35, BloomThreshold = 0.5, Aberration = 0.12, Curvature = 0.5, Mask = 0.45,
            Scanlines = 0.35, ScanlineSpacing = 3, Vignette = 0.25,
        }),
        new("gameBoy", LookCategory.Retro, new Look
        {
            Contrast = 1.15, Palette = RetroPalette.GameBoy, Pixelate = 3, Dither = 0.8,
        }),
        new("pico8", LookCategory.Retro, new Look
        {
            Saturation = 1.15, Contrast = 1.05, Palette = RetroPalette.Pico8, Pixelate = 4, Dither = 0.6,
        }),
        new("commodore64", LookCategory.Retro, new Look
        {
            Contrast = 1.05, Palette = RetroPalette.Commodore64, Pixelate = 4, Dither = 0.5, Scanlines = 0.15,
        }),
        new("oneBit", LookCategory.Retro, new Look
        {
            Contrast = 1.1, Palette = RetroPalette.OneBit, Pixelate = 2, Dither = 1,
        }),
        new("amberTerminal", LookCategory.Retro, new Look
        {
            Contrast = 1.2, Palette = RetroPalette.Amber, Bloom = 0.4, BloomThreshold = 0.4, Curvature = 0.3,
            Scanlines = 0.4, Vignette = 0.3,
        }),
        new("greenTerminal", LookCategory.Retro, new Look
        {
            Contrast = 1.2, Palette = RetroPalette.GreenScreen, Bloom = 0.4, BloomThreshold = 0.4, Curvature = 0.3,
            Scanlines = 0.4, Vignette = 0.3,
        }),

        // Colour
        new("dreamy", LookCategory.Colour, new Look
        {
            Contrast = 0.95, Saturation = 0.9, Fade = 0.08, Temperature = 0.15, Bloom = 0.45, BloomThreshold = 0.55,
            Vignette = 0.2,
        }),
        new("vivid", LookCategory.Colour, new Look
        {
            Saturation = 1.2, Vibrance = 0.35, Contrast = 1.05, Sharpen = 0.35,
        }),
        new("poster", LookCategory.Colour, new Look
        {
            Saturation = 1.25, Contrast = 1.1, Sharpen = 0.4, Posterize = 5, Dither = 0.15,
        }),

        // Comfort
        new("dim", LookCategory.Comfort, new Look { Dim = 0.5 }),
        new("greyscale", LookCategory.Comfort, new Look { Saturation = 0 }),
        new("redNight", LookCategory.Comfort, new Look
        {
            Saturation = 0, ShadowHue = 0, ShadowTint = 1, HighlightHue = 0, HighlightTint = 1, Exposure = -1.6,
        }),
        new("sharpen", LookCategory.Comfort, new Look { Sharpen = 0.55 }),

        // Accessibility
        new("redGreenAssist", LookCategory.Access, new Look { ColorVision = ColorVision.GreenWeak }),
        new("redAssist", LookCategory.Access, new Look { ColorVision = ColorVision.RedWeak }),
    ];

    public static LookPreset? Find(string? id) => All.FirstOrDefault(l => l.Id == id);
}
