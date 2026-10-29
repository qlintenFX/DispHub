// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Hotkeys;
using DispHub.Platform.Shell;
using DispHub.Services;

namespace DispHub.Tests.Platform;

public sealed class KeyboardTypingTests
{
    [Theory]
    [InlineData(HotkeyModifiers.Alt | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Shift)]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win)]
    public void OnlyCtrlAltWithoutWin_CanBeAltGr(HotkeyModifiers modifiers) =>
        Assert.Null(KeyboardTyping.Character(new HotkeyGesture(modifiers, '2')));

    [Fact]
    public void ATypedCharacter_IsNamedInTheMessage()
    {
        string message = AppController.DescribeHotkeyFailure("Hotkey_TypesCharacter:@");

        Assert.Contains("@", message, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", message, StringComparison.Ordinal);
        Assert.Equal("Used by another app", AppController.DescribeHotkeyFailure("Hotkey_InUseElsewhere"));
    }
}
