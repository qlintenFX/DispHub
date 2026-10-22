// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Effects;

/// <summary>
/// One effect in a look, as the editor shows it: a main amount (grain) with its details (grain size, colour noise,
/// speed). A part is in a look when its main setting isn't neutral; adding one gives it a sensible starting amount.
/// </summary>
public sealed class LookPart
{
    internal LookPart(string key, LookGroup group, string main, double addValue, params string[] details)
    {
        Key = key;
        Group = group;
        Main = LookParameters.Find(main) ?? throw new ArgumentException($"Unknown setting {main}.", nameof(main));
        AddValue = addValue;
        Details = [.. details.Select(d => LookParameters.Find(d) ?? throw new ArgumentException($"Unknown setting {d}.", nameof(details)))];
    }

    /// <summary>Also the text key suffix (LookPart_{Key}, LookPart_{Key}Hint).</summary>
    public string Key { get; }

    public LookGroup Group { get; }

    public LookParameter Main { get; }

    public IReadOnlyList<LookParameter> Details { get; }

    /// <summary>The main setting's value when the part is added.</summary>
    public double AddValue { get; }

    public bool IsIn(Look look) => Math.Abs(Main.Get(look) - Main.Default) > 1e-9;

    public Look Add(Look look) => IsIn(look) ? look : Main.Set(look, AddValue);

    /// <summary>Takes the part out: its main setting and the details no other part in the look still uses.</summary>
    public Look Remove(Look look)
    {
        ArgumentNullException.ThrowIfNull(look);
        Look result = Main.Set(look, Main.Default);
        foreach (LookParameter detail in Details)
        {
            bool shared = LookParts.All.Any(p => p != this && p.IsIn(result) && p.Details.Contains(detail));
            if (!shared)
                result = detail.Set(result, detail.Default);
        }
        return result;
    }
}

public static class LookParts
{
    /// <summary>The settings every look shows at the top of the editor.</summary>
    public static IReadOnlyList<LookParameter> Basics { get; } =
        [.. new[] { "exposure", "contrast", "saturation", "temperature" }.Select(k => LookParameters.Find(k)!)];

    public static IReadOnlyList<LookPart> All { get; } =
    [
        new("vibrance", LookGroup.Colour, "vibrance", 0.3),
        new("tint", LookGroup.Colour, "tint", 0.2),
        new("fade", LookGroup.Colour, "fade", 0.08),
        new("sepia", LookGroup.Colour, "sepia", 0.8),
        new("shadowTint", LookGroup.Colour, "shadowTint", 0.3, "shadowHue"),
        new("highlightTint", LookGroup.Colour, "highlightTint", 0.3, "highlightHue"),

        new("sharpen", LookGroup.Light, "sharpen", 0.4),
        new("bloom", LookGroup.Light, "bloom", 0.35, "bloomThreshold"),
        new("halation", LookGroup.Light, "halation", 0.3, "bloomThreshold"),

        new("vignette", LookGroup.Lens, "vignette", 0.35, "vignetteSize", "vignetteSoftness"),
        new("aberration", LookGroup.Lens, "aberration", 0.3),
        new("distortion", LookGroup.Lens, "distortion", 0.3),

        new("grain", LookGroup.Film, "grain", 0.3, "grainSize", "grainColor", "grainFps"),
        new("flicker", LookGroup.Film, "flicker", 0.3, "grainFps"),
        new("weave", LookGroup.Film, "weave", 0.3, "grainFps"),
        new("dust", LookGroup.Film, "dust", 0.3, "grainFps"),
        new("letterbox", LookGroup.Film, "letterbox", 2.39),

        new("scanlines", LookGroup.Retro, "scanlines", 0.3, "scanlineSpacing"),
        new("curvature", LookGroup.Retro, "curvature", 0.4),
        new("mask", LookGroup.Retro, "mask", 0.4),
        new("chromaBleed", LookGroup.Retro, "chromaBleed", 0.5),
        new("tracking", LookGroup.Retro, "tracking", 0.3, "grainFps"),
        new("jitter", LookGroup.Retro, "jitter", 0.25, "grainFps"),
        new("pixelate", LookGroup.Retro, "pixelate", 4),
        new("posterize", LookGroup.Retro, "posterize", 6, "dither"),
        new("palette", LookGroup.Retro, "palette", (int)RetroPalette.GameBoy, "dither"),

        new("dim", LookGroup.Comfort, "dim", 0.4),

        new("colorVision", LookGroup.Access, "colorVision", (int)ColorVision.GreenWeak, "colorVisionStrength"),
    ];

    public static LookPart? Find(string? key) => All.FirstOrDefault(p => p.Key == key);

    /// <summary>The parts a look uses, in editor order.</summary>
    public static IEnumerable<LookPart> In(Look look) => All.Where(p => p.IsIn(look));
}
