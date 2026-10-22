// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Hotkeys;

namespace DispHub.Tests.Core;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Alt+F1")]
    [InlineData("Ctrl+Shift+Win+A")]
    [InlineData("Alt+PageUp")]
    [InlineData("F13")]
    [InlineData("Ctrl+Alt+1")]
    [InlineData("Win+Num5")]
    public void TryParse_ValidGesture_RoundTrips(string text)
    {
        Assert.True(HotkeyGesture.TryParse(text, out HotkeyGesture gesture));
        Assert.Equal(text, gesture.ToString());
    }

    [Theory]
    [InlineData("control + alt + f1", "Ctrl+Alt+F1")]
    [InlineData("Alt+Ctrl+Z", "Ctrl+Alt+Z")]
    public void TryParse_LooseInput_IsNormalised(string text, string expected)
    {
        Assert.True(HotkeyGesture.TryParse(text, out HotkeyGesture gesture));
        Assert.Equal(expected, gesture.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Shift+Up")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Banana")]
    public void TryParse_InvalidOrUnsafeGesture_IsRejected(string? text) =>
        Assert.False(HotkeyGesture.TryParse(text, out _));

    [Fact]
    public void IsModifierKey_RecognisesLeftAndRightModifiers()
    {
        Assert.True(KeyNames.IsModifierKey(0xA0));
        Assert.True(KeyNames.IsModifierKey(0x5B));
        Assert.False(KeyNames.IsModifierKey(0x41));
    }
}
