// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Platform.Shell;

namespace DispHub.Tests.Platform;

public sealed class ProgramInfoTests
{
    [Fact]
    public void Find_ARunningProgramByItsName()
    {
        // This test runs inside a program that is certainly running.
        string self = Path.GetFileName(Environment.ProcessPath!);

        Assert.Equal(Environment.ProcessPath, ProgramInfo.Find(self), StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-program-4f1c2a.exe")]
    public void Find_NothingForUnknownNames(string name) => Assert.Null(ProgramInfo.Find(name));

    [Fact]
    public void GenericIcon_IsWindowsOwnProgramIcon()
    {
        nint icon = ProgramInfo.GenericIcon(64);
        try
        {
            Assert.NotEqual(0, icon);
        }
        finally
        {
            ProgramInfo.DestroyIcon(icon);
        }
    }
}
