// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Platform;

/// <summary>Where DispHub keeps its files: %LocalAppData%\DispHub, or the package's LocalState when installed from the Store.</summary>
public static class AppPaths
{
    private static readonly Lazy<string> Data = new(ResolveDataDirectory);
    private static string? _dataOverride;

    public static string DataDirectory => Data.Value;
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
    public static string JournalPath => Path.Combine(DataDirectory, "restore.json");
    public static string StatePath => Path.Combine(DataDirectory, "state.json");

    /// <summary>Keeps settings and logs in <paramref name="path"/> instead (a --fresh trial run). Call before anything reads them.</summary>
    public static void UseDataDirectory(string path)
    {
        if (Data.IsValueCreated)
            throw new InvalidOperationException("The data folder is already in use.");
        _dataOverride = path;
    }

    private static string ResolveDataDirectory()
    {
        string path = _dataOverride ?? (PackageInfo.IsPackaged
            ? global::Windows.Storage.ApplicationData.Current.LocalFolder.Path
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DispHub"));
        Directory.CreateDirectory(path);
        return path;
    }
}
