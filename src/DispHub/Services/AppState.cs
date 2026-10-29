// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Config;
using DispHub.Platform;

namespace DispHub.Services;

/// <summary>
/// The one owner of state.json (first run, usage counters, prompts shown), so the services that use it never write
/// over each other's values.
/// </summary>
public sealed class AppState
{
    public AppState(TimeProvider time)
    {
        Data = JsonFiles.ReadState(AppPaths.StatePath);
        if (Data.FirstRunUtc is null)
        {
            Data.FirstRunUtc = time.GetUtcNow();
            Save();
        }
    }

    public AppStateData Data { get; }

    public void Save()
    {
        try
        {
            JsonFiles.WriteState(AppPaths.StatePath, Data);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
        }
    }
}
