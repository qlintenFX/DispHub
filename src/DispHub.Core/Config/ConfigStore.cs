// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.Json;
using DispHub.Core.Model;

namespace DispHub.Core.Config;

public enum ConfigLoadStatus
{
    /// <summary>No config existed; defaults were created.</summary>
    Created,
    Loaded,
    /// <summary>config.json was unreadable; it was set aside and the backup was used.</summary>
    RecoveredFromBackup,
    /// <summary>config.json and its backup were unreadable; both were set aside and defaults were used.</summary>
    ResetToDefaults,
}

public sealed record ConfigLoadResult(
    AppConfig Config,
    ConfigLoadStatus Status,
    IReadOnlyList<string> Fixes,
    bool IsReadOnly);

/// <summary>Loads and saves config.json with a .bak copy. Never silently discards a user's file.</summary>
public sealed class ConfigStore(string directory, TimeProvider? timeProvider = null)
{
    private readonly Lock _gate = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public string ConfigPath { get; } = Path.Combine(directory, "config.json");
    public string BackupPath => ConfigPath + ".bak";

    /// <summary>True when the file was written by a newer DispHub; saving would lose its data.</summary>
    public bool IsReadOnly { get; private set; }

    public ConfigLoadResult Load()
    {
        lock (_gate)
        {
            if (!File.Exists(ConfigPath) && !File.Exists(BackupPath))
            {
                AppConfig created = DefaultConfig.Create();
                Save(created);
                return new ConfigLoadResult(created, ConfigLoadStatus.Created, [], false);
            }

            var status = ConfigLoadStatus.Loaded;
            AppConfig? config = TryRead(ConfigPath);
            if (config is null)
            {
                SetAside(ConfigPath);
                config = TryRead(BackupPath);
                status = ConfigLoadStatus.RecoveredFromBackup;
                if (config is null)
                {
                    SetAside(BackupPath);
                    config = DefaultConfig.Create();
                    status = ConfigLoadStatus.ResetToDefaults;
                }
            }

            IsReadOnly = config.Version > AppConfig.CurrentVersion;
            IReadOnlyList<string> fixes = ConfigNormalizer.Normalize(config);
            if (status != ConfigLoadStatus.Loaded || (fixes.Count > 0 && !IsReadOnly))
                Save(config);

            return new ConfigLoadResult(config, status, fixes, IsReadOnly);
        }
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_gate)
        {
            if (IsReadOnly)
                return;
            string json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);
            AtomicFile.WriteAllText(ConfigPath, json, BackupPath);
        }
    }

    /// <summary>Serialises without writing, so callers can snapshot on the UI thread and save elsewhere.</summary>
    public static string Serialize(AppConfig config) =>
        JsonSerializer.Serialize(config, ConfigJsonContext.Default.AppConfig);

    /// <summary>Reads just the UI language from config.json without loading the rest; null if unset or unreadable.</summary>
    public static string? PeekLanguage(string directory)
    {
        try
        {
            string path = Path.Combine(directory, "config.json");
            if (!File.Exists(path))
                return null;
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("ui", out JsonElement ui) &&
                   ui.TryGetProperty("language", out JsonElement language) &&
                   language.ValueKind == JsonValueKind.String
                ? language.GetString()
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static AppConfig Deserialize(string json) =>
        JsonSerializer.Deserialize(json, ConfigJsonContext.Default.AppConfig)
        ?? throw new JsonException("Config is empty.");

    public void SaveSerialized(string json)
    {
        lock (_gate)
        {
            if (!IsReadOnly)
                AtomicFile.WriteAllText(ConfigPath, json, BackupPath);
        }
    }

    private static AppConfig? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            string json = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(json) ? null : Deserialize(json);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private void SetAside(string path)
    {
        if (!File.Exists(path))
            return;
        string stamp = _time.GetUtcNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string target = Path.Combine(
            Path.GetDirectoryName(path)!,
            $"{Path.GetFileName(path)}.corrupt-{stamp}");
        File.Move(path, target, overwrite: true);
    }
}
