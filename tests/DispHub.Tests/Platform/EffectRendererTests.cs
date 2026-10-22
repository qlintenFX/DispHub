// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Effects;
using DispHub.Platform.Effects;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi.Common;

namespace DispHub.Tests.Platform;

/// <summary>
/// The effect shaders, run on WARP (Windows' software renderer) so the results are the same on any machine,
/// including CI without a GPU.
/// </summary>
public sealed class EffectRendererTests : IDisposable
{
    private const int Width = 64;
    private const int Height = 40;
    private readonly EffectPreview _preview = new(software: true);
    private readonly byte[] _picture = Picture();

    public EffectRendererTests() => _preview.SetPicture(_picture, Width, Height);

    public void Dispose() => _preview.Dispose();

    [Fact]
    public void NeutralLook_LeavesThePictureExactlyAsItWas()
    {
        Assert.Equal(_picture, Render(Look.Neutral));
    }

    [Fact]
    public void Greyscale_MakesEveryPixelGrey()
    {
        byte[] output = Render(Looks.Find("greyscale")!.Look);

        for (int i = 0; i < output.Length; i += 4)
        {
            Assert.InRange(Math.Abs(output[i] - output[i + 1]), 0, 1);
            Assert.InRange(Math.Abs(output[i + 1] - output[i + 2]), 0, 1);
        }
    }

    [Fact]
    public void Dim_DarkensByItsAmount()
    {
        byte[] output = Render(new Look { Dim = 0.5 });

        static double Colour(byte[] bgra) => bgra.Where((_, i) => i % 4 != 3).Average(b => (double)b);
        Assert.Equal(Colour(_picture) / 2, Colour(output), 1.5);
    }

    [Fact]
    public void Letterbox_DrawsBlackBarsAboveAndBelow()
    {
        byte[] output = Render(new Look { Letterbox = 2.39 });

        Assert.Equal(0, Pixel(output, 10, 0) & 0xFFFFFF);
        Assert.Equal(0, Pixel(output, 10, Height - 1) & 0xFFFFFF);
        Assert.Equal(Pixel(_picture, 10, Height / 2), Pixel(output, 10, Height / 2));
    }

    [Fact]
    public void Palette_LimitsTheColours()
    {
        byte[] output = Render(Looks.Find("gameBoy")!.Look);

        var allowed = Palettes.Get(RetroPalette.GameBoy)!.Colours.ToHashSet();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
                Assert.Contains(Pixel(output, x, y) & 0xFFFFFF, allowed);
        }
    }

    [Fact]
    public void EveryPreset_Renders()
    {
        foreach (LookPreset preset in Looks.All)
        {
            byte[] output = Render(preset.Look);
            Assert.NotEqual(_picture, output);
        }
    }

    [Fact]
    public unsafe void Differs_SpotsASinglePixel()
    {
        using var renderer = new EffectRenderer(null, software: true);
        byte[] changed = (byte[])_picture.Clone();
        changed[((17 * Width) + 40) * 4] ^= 1;
        ID3D11ShaderResourceView* a = Texture(renderer.Device, _picture, target: false);
        ID3D11ShaderResourceView* b = Texture(renderer.Device, (byte[])_picture.Clone(), target: false);
        ID3D11ShaderResourceView* c = Texture(renderer.Device, changed, target: false);
        var scratch = (ID3D11RenderTargetView*)Texture(renderer.Device, null, target: true);
        try
        {
            var all = new System.Drawing.Rectangle(0, 0, Width, Height);
            Assert.False(renderer.Differs(a, b, scratch, all));
            Assert.True(renderer.Differs(a, c, scratch, all));
            // Only the area Windows says changed is compared.
            Assert.True(renderer.Differs(a, c, scratch, new System.Drawing.Rectangle(38, 15, 4, 4)));
            Assert.False(renderer.Differs(a, c, scratch, new System.Drawing.Rectangle(0, 0, 30, Height)));
        }
        finally
        {
            a->Release();
            b->Release();
            c->Release();
            scratch->Release();
        }
    }

    [Fact]
    public void Lines_StayPutWithoutLineEffects()
    {
        float[] lines = new float[100 * FrameNoise.LineStride];
        FrameNoise.Lines(new Look { Grain = 0.5 }, 12.3f, 7, 100, lines);

        for (int row = 0; row < 100; row++)
        {
            Assert.Equal(0, lines[row * FrameNoise.LineStride]);
            Assert.Equal(1, lines[(row * FrameNoise.LineStride) + 2]);
        }
    }

    [Fact]
    public void Lines_HeadSwitchingPushesTheBottomLinesRight()
    {
        float[] lines = new float[400 * FrameNoise.LineStride];
        // Early in the tracking band's roll, so it is above the picture.
        FrameNoise.Lines(new Look { Tracking = 1 }, 0.01f, 7, 400, lines);

        Assert.True(lines[399 * FrameNoise.LineStride] > 15);
        Assert.Equal(0, lines[200 * FrameNoise.LineStride]);
    }

    private byte[] Render(Look look)
    {
        byte[] output = new byte[_picture.Length];
        _preview.Render(look, output);
        return output;
    }

    private static int Pixel(byte[] bgra, int x, int y)
    {
        int i = ((y * Width) + x) * 4;
        return (bgra[i + 2] << 16) | (bgra[i + 1] << 8) | bgra[i];
    }

    /// <summary>A colourful gradient, opaque.</summary>
    private static byte[] Picture()
    {
        byte[] pixels = new byte[Width * Height * 4];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = ((y * Width) + x) * 4;
                pixels[i] = (byte)(x * 4);
                pixels[i + 1] = (byte)(y * 6);
                pixels[i + 2] = (byte)(255 - (x * 3));
                pixels[i + 3] = 255;
            }
        }
        return pixels;
    }

    private static unsafe ID3D11ShaderResourceView* Texture(ID3D11Device* device, byte[]? pixels, bool target)
    {
        var desc = new D3D11_TEXTURE2D_DESC
        {
            Width = Width,
            Height = Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = target ? D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET : D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
        };
        ID3D11Texture2D* texture;
        if (pixels is null)
        {
            device->CreateTexture2D(&desc, null, &texture);
        }
        else
        {
            fixed (byte* data = pixels)
            {
                var init = new D3D11_SUBRESOURCE_DATA { pSysMem = data, SysMemPitch = Width * 4 };
                device->CreateTexture2D(&desc, &init, &texture);
            }
        }
        try
        {
            if (target)
            {
                ID3D11RenderTargetView* view;
                device->CreateRenderTargetView((ID3D11Resource*)texture, null, &view);
                return (ID3D11ShaderResourceView*)view;
            }
            ID3D11ShaderResourceView* resource;
            device->CreateShaderResourceView((ID3D11Resource*)texture, null, &resource);
            return resource;
        }
        finally
        {
            texture->Release();
        }
    }
}
