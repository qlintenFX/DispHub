// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using Windows.Win32;
using Windows.Win32.System.Power;

namespace DispHub.Platform.Shell;

public static class PowerSource
{
    /// <summary>The PC runs on battery now. Desktops, and laptops on the charger, don't.</summary>
    public static bool OnBattery() => PInvoke.GetSystemPowerStatus(out SYSTEM_POWER_STATUS status) && status.ACLineStatus == 0;
}
