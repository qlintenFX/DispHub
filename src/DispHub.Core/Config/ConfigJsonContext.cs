using System.Text.Json.Serialization;
using DispHub.Core.Model;

namespace DispHub.Core.Config;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(AppStateData))]
[JsonSerializable(typeof(RestoreJournalData))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext;
