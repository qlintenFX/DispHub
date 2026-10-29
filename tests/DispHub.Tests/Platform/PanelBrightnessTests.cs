// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Platform.Displays;

namespace DispHub.Tests.Platform;

public sealed class PanelBrightnessTests
{
    [Fact]
    public void InstancePrefix_TurnsTheMonitorPathIntoWmisInstanceName()
    {
        // Seen on a Lenovo laptop: WMI calls this screen DISPLAY\EDO4245\4&6cbc18c&1&UID8388688_0.
        string? prefix = PanelBrightness.InstancePrefix(@"\\?\display#edo4245#4&6cbc18c&1&uid8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}");

        Assert.Equal(@"display\edo4245\4&6cbc18c&1&uid8388688", prefix);
        Assert.StartsWith(prefix + "_", @"DISPLAY\EDO4245\4&6cbc18c&1&UID8388688_0", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("simulated-monitor")]
    [InlineData(@"\\?\#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}")]
    public void InstancePrefix_NothingForOtherKeys(string key) => Assert.Null(PanelBrightness.InstancePrefix(key));

    [Fact]
    public void ExternalMonitors_AreNotAskedOverWmi()
    {
        var external = new MonitorInfo(@"\\?\display#del1234#5&1&0&uid1#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}", @"\\.\DISPLAY2", "Dell", false, false, false, "DEL1234");

        Assert.Null(new PanelBrightness().ReadLevel(external));
        Assert.False(new PanelBrightness().WriteLevel(external, 50));
    }
}
