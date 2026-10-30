// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Controls;

/// <summary>DispHub's own animations, which the user can turn off in Settings.</summary>
public static class Motion
{
    /// <summary>The user turned off animations: things move straight to where they end up.</summary>
    public static bool Reduced { get; set; }
}
