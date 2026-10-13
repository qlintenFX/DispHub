using DispHub.Core.Color;

namespace DispHub.Core.Model;

/// <summary>A starting point for a new profile; its name and description come from the app's text.</summary>
public sealed record ProfileTemplate(string Key, ColorSettings Color);

/// <summary>General looks to start a profile from. All stay within Windows' default gamma limits.</summary>
public static class ProfileTemplates
{
    public static IReadOnlyList<ProfileTemplate> All { get; } =
    [
        new("vivid", new ColorSettings(Contrast: 4, Vibrance: 70)),
        new("shadows", new ColorSettings(Brightness: 6, Gamma: 1.2)),
        new("film", new ColorSettings(Brightness: -5, Contrast: 8, Gamma: 0.95, Vibrance: 55)),
        new("evening", new ColorSettings(Brightness: -8, TemperatureK: 4200)),
        new("dim", new ColorSettings(Brightness: -30, TemperatureK: 5000)),
    ];
}
