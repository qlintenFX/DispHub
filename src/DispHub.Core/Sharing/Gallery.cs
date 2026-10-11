using System.Text.Json;
using System.Text.Json.Serialization;

namespace DispHub.Core.Sharing;

/// <summary>The community preset list (presets/gallery.json in the repository).</summary>
public sealed class GalleryFile
{
    public int Version { get; set; } = 1;
    public List<GalleryEntry> Presets { get; set; } = [];

    /// <summary>Parses the gallery and drops entries whose link isn't a valid preset. Returns null for unreadable JSON.</summary>
    public static IReadOnlyList<(GalleryEntry Entry, SharedPreset Preset)>? Parse(string json)
    {
        try
        {
            GalleryFile? file = JsonSerializer.Deserialize(json, GalleryJsonContext.Default.GalleryFile);
            if (file is null)
                return null;
            return file.Presets
                .Where(e => e is not null)
                .Select(e => (Entry: e, Ok: SharedPreset.TryParse(e.Link, out SharedPreset? p), Preset: p))
                .Where(x => x.Ok)
                .Select(x => (x.Entry, x.Preset!))
                .ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class GalleryEntry
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string Link { get; set; } = "";

    /// <summary>GitHub name of whoever shared it; empty for DispHub's own starter presets.</summary>
    public string? Author { get; set; }
}

/// <summary>Where people send presets for the gallery: a GitHub issue form, so sharing needs no git knowledge.</summary>
public static class GallerySubmission
{
    public const string FormUrl = "https://github.com/qlintenFX/DispHub/issues/new?template=preset.yml";

    /// <summary>The form with the preset's link and name already filled in.</summary>
    public static Uri For(SharedPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        return new Uri(
            $"{FormUrl}&title={Uri.EscapeDataString("Preset: " + preset.Name)}" +
            $"&name={Uri.EscapeDataString(preset.Name)}&link={Uri.EscapeDataString(preset.ToLink())}");
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(GalleryFile))]
internal sealed partial class GalleryJsonContext : JsonSerializerContext;
