using DispHub.Core.Effects;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi.Common;

namespace DispHub.Platform.Effects;

/// <summary>
/// Renders looks onto a picture off screen, with the same shaders the screen uses: for the previews in the app and
/// for tests. Thread-safe; renders one picture at a time.
/// </summary>
public sealed unsafe class EffectPreview : IDisposable
{
    private readonly Lock _lock = new();
    private readonly EffectRenderer _renderer;
    private ID3D11Texture2D* _source;
    private ID3D11ShaderResourceView* _sourceView;
    private ID3D11Texture2D* _target;
    private ID3D11RenderTargetView* _targetView;
    private ID3D11Texture2D* _readback;
    private bool _disposed;

    /// <param name="software">Render with WARP, Windows' software renderer, instead of the GPU.</param>
    public EffectPreview(bool software = false)
    {
        EffectRenderer? renderer = null;
        if (!software)
        {
            try
            {
                renderer = new EffectRenderer(null, software: false);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException)
            {
                renderer = null;
            }
        }
        _renderer = renderer ?? new EffectRenderer(null, software: true);
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Sets the picture looks are drawn on: 32-bit BGRA pixels, top row first.</summary>
    public void SetPicture(ReadOnlySpan<byte> bgra, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (bgra.Length < width * height * 4)
            throw new ArgumentException("The picture is smaller than its size says.", nameof(bgra));
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ReleaseTextures();
            ID3D11Device* device = _renderer.Device;
            fixed (byte* pixels = bgra)
            {
                var init = new D3D11_SUBRESOURCE_DATA { pSysMem = pixels, SysMemPitch = (uint)(width * 4) };
                D3D11_TEXTURE2D_DESC desc = Describe(width, height, D3D11_USAGE.D3D11_USAGE_IMMUTABLE, D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE);
                ID3D11Texture2D* source;
                device->CreateTexture2D(&desc, &init, &source);
                _source = source;
            }
            ID3D11ShaderResourceView* sourceView;
            device->CreateShaderResourceView((ID3D11Resource*)_source, null, &sourceView);
            _sourceView = sourceView;

            D3D11_TEXTURE2D_DESC targetDesc = Describe(width, height, D3D11_USAGE.D3D11_USAGE_DEFAULT, D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET);
            ID3D11Texture2D* target;
            device->CreateTexture2D(&targetDesc, null, &target);
            _target = target;
            ID3D11RenderTargetView* targetView;
            device->CreateRenderTargetView((ID3D11Resource*)_target, null, &targetView);
            _targetView = targetView;

            D3D11_TEXTURE2D_DESC readbackDesc = Describe(width, height, D3D11_USAGE.D3D11_USAGE_STAGING, 0);
            readbackDesc.CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ;
            ID3D11Texture2D* readback;
            device->CreateTexture2D(&readbackDesc, null, &readback);
            _readback = readback;
            (Width, Height) = (width, height);
        }
    }

    /// <summary>Draws <paramref name="look"/> on the picture into <paramref name="bgra"/> (same size as the picture).</summary>
    /// <param name="seconds">Where an animated look is in time; it picks the grain pattern.</param>
    public void Render(Look look, Span<byte> bgra, double seconds = 1)
    {
        ArgumentNullException.ThrowIfNull(look);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_source is null)
                throw new InvalidOperationException("Set a picture first.");
            if (bgra.Length < Width * Height * 4)
                throw new ArgumentException("The output is smaller than the picture.", nameof(bgra));

            // Sized as if the picture filled a 1080p screen, so grain and pixels look in proportion.
            _renderer.Draw(look, FrameClock.At(seconds, Math.Max(look.GrainFps, 1)), Height / 1080.0, _sourceView, Width, Height, _targetView, Width, Height);

            ID3D11DeviceContext* context = _renderer.Context;
            context->CopyResource((ID3D11Resource*)_readback, (ID3D11Resource*)_target);
            D3D11_MAPPED_SUBRESOURCE mapped;
            context->Map((ID3D11Resource*)_readback, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped);
            try
            {
                int row = Width * 4;
                for (int y = 0; y < Height; y++)
                    new ReadOnlySpan<byte>((byte*)mapped.pData + (y * mapped.RowPitch), row).CopyTo(bgra.Slice(y * row, row));
            }
            finally
            {
                context->Unmap((ID3D11Resource*)_readback, 0);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            ReleaseTextures();
            _renderer.Dispose();
        }
    }

    private void ReleaseTextures()
    {
        Com.Release(ref _readback);
        Com.Release(ref _targetView);
        Com.Release(ref _target);
        Com.Release(ref _sourceView);
        Com.Release(ref _source);
    }

    private static D3D11_TEXTURE2D_DESC Describe(int width, int height, D3D11_USAGE usage, D3D11_BIND_FLAG bind) => new()
    {
        Width = (uint)width,
        Height = (uint)height,
        MipLevels = 1,
        ArraySize = 1,
        Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
        SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
        Usage = usage,
        BindFlags = bind,
    };
}
