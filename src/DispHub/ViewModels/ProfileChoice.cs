// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows.Media;
using DispHub.Core.Model;

namespace DispHub.ViewModels;

/// <summary>A profile in a picker, with its preview strip.</summary>
public sealed record ProfileChoice(Guid Id, string Name, ImageSource Preview)
{
    public static ProfileChoice From(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new ProfileChoice(profile.Id, profile.Name, Controls.ColorPreview.Render(profile.Color));
    }

    public override string ToString() => Name;
}
