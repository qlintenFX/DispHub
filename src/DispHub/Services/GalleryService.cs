using System.IO;
using System.Net.Http;
using DispHub.Core.Config;
using DispHub.Core.Sharing;
using DispHub.Platform;
using Microsoft.Extensions.Logging;

namespace DispHub.Services;

/// <summary>
/// Loads the community preset gallery from the repository. Only runs when the Presets page is opened. Falls back to
/// the last downloaded copy, then to the copy built into the app, so the page works offline.
/// </summary>
public sealed class GalleryService(ILogger<GalleryService> logger)
{
    public const string GalleryUrl = "https://raw.githubusercontent.com/qlintenFX/DispHub/main/presets/gallery.json";
    public const string ContributeUrl = "https://github.com/qlintenFX/DispHub/tree/main/presets";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public async Task<IReadOnlyList<(GalleryEntry Entry, SharedPreset Preset)>> LoadAsync(CancellationToken cancellationToken = default)
    {
        string cachePath = Path.Combine(AppPaths.DataDirectory, "gallery.json");
        try
        {
            string json = await Http.GetStringAsync(new Uri(GalleryUrl), cancellationToken);
            if (GalleryFile.Parse(json) is { Count: > 0 } online)
            {
                AtomicFile.WriteAllText(cachePath, json);
                return online;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            logger.LogInformation("Preset gallery offline ({Reason}); using a saved copy", ex.Message);
        }

        try
        {
            if (File.Exists(cachePath) && GalleryFile.Parse(await File.ReadAllTextAsync(cachePath, cancellationToken)) is { Count: > 0 } cached)
                return cached;
        }
        catch (IOException)
        {
        }

        await using Stream? builtIn = typeof(GalleryService).Assembly.GetManifestResourceStream("DispHub.gallery.json");
        if (builtIn is null)
            return [];
        using var reader = new StreamReader(builtIn);
        return GalleryFile.Parse(await reader.ReadToEndAsync(cancellationToken)) ?? [];
    }
}
