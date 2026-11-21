// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Config;
using DispHub.Core.Licensing;
using DispHub.Platform;
using DispHub.Platform.Store;

namespace DispHub.Services;

/// <summary>
/// The one owner of state.json (first run, usage counters, prompts shown), so the services that use it never write
/// over each other's values.
/// </summary>
public sealed class AppState
{
    private readonly string _path;

    /// <param name="path">Where state.json is; tests give a temporary one.</param>
    public AppState(TimeProvider time, IFirstRunMarker firstRun, string? path = null)
    {
        _path = path ?? AppPaths.StatePath;
        Data = JsonFiles.ReadState(_path);

        // The trial counts from the first run of any install, so reinstalling doesn't start it again.
        DateTimeOffset? earlier = firstRun.Read();
        DateTimeOffset first = FeatureGate.FirstRun(Data.FirstRunUtc, earlier, time.GetUtcNow());
        if (Data.FirstRunUtc != first)
        {
            Data.FirstRunUtc = first;
            Save();
        }
        if (earlier != first)
            firstRun.Write(first);
    }

    public AppStateData Data { get; }

    public void Save()
    {
        try
        {
            JsonFiles.WriteState(_path, Data);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
        }
    }
}
