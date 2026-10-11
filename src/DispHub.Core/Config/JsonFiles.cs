using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DispHub.Core.Config;

/// <summary>Usage counters and other non-preference data. Saved as state.json.</summary>
public sealed class AppStateData
{
    public DateTimeOffset? FirstRunUtc { get; set; }
    public int ProfileSwitchCount { get; set; }
    public DateTimeOffset? ReviewPromptedUtc { get; set; }
}

/// <summary>
/// The colours each monitor had before DispHub changed them, written before the first change so a crash
/// can be undone on the next start. Saved as restore.json and deleted after a clean restore.
/// </summary>
public sealed class RestoreJournalData
{
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public Dictionary<string, RestoreEntry> Monitors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RestoreEntry
{
    /// <summary>GDI device name at the time of capture, e.g. \\.\DISPLAY1.</summary>
    public string? DeviceName { get; set; }

    /// <summary>Original gamma ramp, base64 of 768 little-endian ushorts.</summary>
    public string? Ramp { get; set; }

    /// <summary>Original vendor vibrance level, in driver units.</summary>
    public int? VibranceLevel { get; set; }
}

/// <summary>Small JSON files without backups (state.json, restore.json).</summary>
public static class JsonFiles
{
    public static AppStateData ReadState(string path) => Read(path, ConfigJsonContext.Default.AppStateData) ?? new();

    public static void WriteState(string path, AppStateData data) => Write(path, data, ConfigJsonContext.Default.AppStateData);

    public static RestoreJournalData? ReadJournal(string path) => Read(path, ConfigJsonContext.Default.RestoreJournalData);

    public static void WriteJournal(string path, RestoreJournalData data) =>
        Write(path, data, ConfigJsonContext.Default.RestoreJournalData);

    private static T? Read<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize(File.ReadAllText(path), typeInfo) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Write<T>(string path, T data, JsonTypeInfo<T> typeInfo) =>
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(data, typeInfo));
}
