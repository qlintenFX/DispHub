// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;
using DispHub.Platform.Displays;

namespace DispHub.Platform.Vibrance;

/// <summary>Vendor-specific saturation ("digital vibrance") control. All calls happen on the colour worker thread.</summary>
public interface IVibranceProvider : IDisposable
{
    string Vendor { get; }

    /// <summary>Re-reads which of <paramref name="monitors"/> this vendor drives. Call after a display change.</summary>
    void Refresh(IReadOnlyList<MonitorInfo> monitors);

    /// <summary>The driver range for a GDI device such as \\.\DISPLAY1, or null if this vendor doesn't drive it.</summary>
    VibranceRange? GetRange(string deviceName);

    int? GetLevel(string deviceName);

    bool SetLevel(string deviceName, int level);
}
