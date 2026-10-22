// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Effects;

/// <summary>Colour sets of old computers and screens; a look can limit the picture to one of them.</summary>
public enum RetroPalette
{
    None,
    GameBoy,
    Pico8,
    Commodore64,
    Cga,
    OneBit,
    Amber,
    GreenScreen,
}

/// <param name="Colours">Up to 16 colours as 0xRRGGBB.</param>
/// <param name="Spread">How far dithering pushes a colour: about the gap between neighbouring colours.</param>
/// <param name="ByBrightness">
/// A single-hue screen: colours run dark to light and are picked by brightness alone, in even steps.
/// </param>
public sealed record PaletteInfo(IReadOnlyList<int> Colours, double Spread, bool ByBrightness);

public static class Palettes
{
    public const int MaxColours = 16;

    private static readonly Dictionary<RetroPalette, PaletteInfo> All = new()
    {
        [RetroPalette.GameBoy] = new([0x0F380F, 0x306230, 0x8BAC0F, 0x9BBC0F], 0.3, true),
        [RetroPalette.Pico8] = new(
        [
            0x000000, 0x1D2B53, 0x7E2553, 0x008751, 0xAB5236, 0x5F574F, 0xC2C3C7, 0xFFF1E8,
            0xFF004D, 0xFFA300, 0xFFEC27, 0x00E436, 0x29ADFF, 0x83769C, 0xFF77A8, 0xFFCCAA,
        ], 0.22, false),
        // The "Pepto" measurements of the real chip.
        [RetroPalette.Commodore64] = new(
        [
            0x000000, 0xFFFFFF, 0x68372B, 0x70A4B2, 0x6F3D86, 0x588D43, 0x352879, 0xB8C76F,
            0x6F4F25, 0x433900, 0x9A6759, 0x444444, 0x6C6C6C, 0x9AD284, 0x6C5EB5, 0x959595,
        ], 0.2, false),
        [RetroPalette.Cga] = new([0x000000, 0x55FFFF, 0xFF55FF, 0xFFFFFF], 0.5, false),
        [RetroPalette.OneBit] = new([0x000000, 0xFFFFFF], 1, true),
        [RetroPalette.Amber] = new(Ramp(0xFFB000, 8), 0.15, true),
        [RetroPalette.GreenScreen] = new(Ramp(0x33FF66, 8), 0.15, true),
    };

    public static PaletteInfo? Get(RetroPalette palette) => All.GetValueOrDefault(palette);

    /// <summary>Black up to <paramref name="colour"/> in even steps, like a single-colour monitor.</summary>
    private static int[] Ramp(int colour, int steps)
    {
        var ramp = new int[steps];
        for (int i = 0; i < steps; i++)
        {
            double t = i / (double)(steps - 1);
            int r = (int)Math.Round(((colour >> 16) & 0xFF) * t);
            int g = (int)Math.Round(((colour >> 8) & 0xFF) * t);
            int b = (int)Math.Round((colour & 0xFF) * t);
            ramp[i] = (r << 16) | (g << 8) | b;
        }
        return ramp;
    }
}
