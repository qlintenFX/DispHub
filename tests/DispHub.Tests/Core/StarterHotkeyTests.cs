// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Hotkeys;
using DispHub.Core.Model;

namespace DispHub.Tests.Core;

public sealed class StarterHotkeyTests
{
    private const HotkeyModifiers CtrlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    [Fact]
    public void Starters_AreCtrlAltDigits_WhereTheyTypeNothing()
    {
        AppConfig config = DefaultConfig.Create(_ => false);

        Assert.Equal(["Ctrl+Alt+1", "Ctrl+Alt+2", "Ctrl+Alt+3"], config.Profiles.Select(p => p.Hotkey));
        Assert.Equal("Ctrl+Alt+0", config.Hotkeys.TogglePause);
    }

    [Fact]
    public void Starters_AddShift_WhereAltGrTypesACharacter()
    {
        // A Belgian keyboard: AltGr+1, 2, 3 and 0 type |, @, # and }; with Shift as well they type nothing.
        AppConfig config = DefaultConfig.Create(g => g.Modifiers == CtrlAlt);

        Assert.Equal(["Ctrl+Alt+Shift+1", "Ctrl+Alt+Shift+2", "Ctrl+Alt+Shift+3"], config.Profiles.Select(p => p.Hotkey));
        Assert.Equal("Ctrl+Alt+Shift+0", config.Hotkeys.TogglePause);
    }

    [Fact]
    public void Starters_LeaveAKeyOut_WhenBothWaysTypeSomething()
    {
        AppConfig config = DefaultConfig.Create(g => g.VirtualKey == '1');

        Assert.Null(config.Profiles[0].Hotkey);
        Assert.Equal("Ctrl+Alt+2", config.Profiles[1].Hotkey);
    }
}
