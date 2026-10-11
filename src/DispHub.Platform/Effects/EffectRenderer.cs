using System.Runtime.InteropServices;
using DispHub.Core.Effects;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;

namespace DispHub.Platform.Effects;

/// <summary>
/// Draws a <see cref="Look"/> with the effect shaders (Effects.hlsl) on one Direct3D 11 device: an optional bloom
/// chain at quarter size, then the main pass into the caller's render target. Use from one thread.
/// </summary>
internal sealed unsafe class EffectRenderer : IDisposable
{
    private static readonly D3D_FEATURE_LEVEL[] FeatureLevels =
    [
        D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
        D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1,
        D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0,
    ];

    private ID3D11Device* _device;
    private ID3D11DeviceContext* _context;
    private ID3D11VertexShader* _vertexShader;
    private ID3D11PixelShader* _main;
    private ID3D11PixelShader* _bright;
    private ID3D11PixelShader* _blur;
    private ID3D11PixelShader* _differs;
    private ID3D11Query* _anyPassed;
    private ID3D11SamplerState* _sampler;
    private ID3D11Buffer* _constants;
    private readonly RenderTexture _bloomA = new();
    private readonly RenderTexture _bloomB = new();

    /// <param name="adapter">The GPU to use; null for the default one.</param>
    /// <param name="software">Use WARP, Windows' software renderer (for tests and when there's no usable GPU).</param>
    public EffectRenderer(IDXGIAdapter* adapter, bool software)
    {
        D3D_DRIVER_TYPE driver = adapter is not null ? D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_UNKNOWN
            : software ? D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP : D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE;
        ID3D11Device* device;
        ID3D11DeviceContext* context;
        PInvoke.D3D11CreateDevice(adapter, driver, HMODULE.Null, D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            FeatureLevels, PInvoke.D3D11_SDK_VERSION, &device, out _, &context).ThrowOnFailure();
        _device = device;
        _context = context;
        try
        {
            CreatePipeline();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public ID3D11Device* Device => _device;

    public ID3D11DeviceContext* Context => _context;

    /// <summary>
    /// Draws one frame. Without a <paramref name="source"/> only the layers drawn on top are output, as
    /// premultiplied alpha.
    /// </summary>
    /// <param name="pixelScale">Target pixels per 96-dpi pixel: grain, noise and pixelation are sized in those.</param>
    public void Draw(Look look, FrameClock frame, double pixelScale, ID3D11ShaderResourceView* source, int sourceWidth,
        int sourceHeight, ID3D11RenderTargetView* target, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(look);
        ShaderConstants constants = ShaderConstants.From(look, frame, width, height, source is not null);
        constants.PixelScale = (float)Math.Clamp(pixelScale, 0.1, 8);
        constants.SourceTexelX = 1f / Math.Max(sourceWidth, 1);
        constants.SourceTexelY = 1f / Math.Max(sourceHeight, 1);

        ID3D11DeviceContext* context = _context;
        context->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetShader(_vertexShader, null, 0);
        ID3D11Buffer* buffer = _constants;
        context->PSSetConstantBuffers(0, 1, &buffer);
        ID3D11SamplerState* sampler = _sampler;
        context->PSSetSamplers(0, 1, &sampler);

        bool bloom = source is not null && (look.Bloom > 0 || look.Halation > 0);
        if (bloom)
        {
            int w = Math.Max(sourceWidth / 4, 1), h = Math.Max(sourceHeight / 4, 1);
            _bloomA.EnsureSize(_device, w, h);
            _bloomB.EnsureSize(_device, w, h);
            Pass(_bright, ref constants, source, null, _bloomA.Target, w, h);
            for (int round = 1; round <= 2; round++)
            {
                // A second, wider round makes the glow spread like light rather than a blur.
                (constants.BlurStepX, constants.BlurStepY) = (round / (float)w, 0);
                Pass(_blur, ref constants, _bloomA.View, null, _bloomB.Target, w, h);
                (constants.BlurStepX, constants.BlurStepY) = (0, round / (float)h);
                Pass(_blur, ref constants, _bloomB.View, null, _bloomA.Target, w, h);
            }
        }
        Pass(_main, ref constants, source, bloom ? _bloomA.View : null, target, width, height);
        Unbind();
    }

    /// <summary>
    /// Whether two same-sized textures differ anywhere. Runs on the GPU and waits only for that tiny pass.
    /// <paramref name="scratch"/> is a render target at least as big whose content may change (GPUs skip a pass
    /// that writes nothing, and with it the comparison).
    /// </summary>
    public bool Differs(ID3D11ShaderResourceView* a, ID3D11ShaderResourceView* b, ID3D11RenderTargetView* scratch, int width, int height)
    {
        ID3D11DeviceContext* context = _context;
        Unbind();
        context->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetShader(_vertexShader, null, 0);
        context->PSSetShader(_differs, null, 0);
        context->OMSetRenderTargets(1, &scratch, null);
        var viewport = new D3D11_VIEWPORT { Width = width, Height = height, MaxDepth = 1 };
        context->RSSetViewports(1, &viewport);
        ID3D11ShaderResourceView** views = stackalloc ID3D11ShaderResourceView*[2];
        views[0] = a;
        views[1] = b;
        context->PSSetShaderResources(0, 2, views);
        context->Begin((ID3D11Asynchronous*)_anyPassed);
        context->Draw(3, 0);
        context->End((ID3D11Asynchronous*)_anyPassed);
        Unbind();

        BOOL any;
        long giveUpAt = Environment.TickCount64 + 100;
        while (context->GetData((ID3D11Asynchronous*)_anyPassed, &any, (uint)sizeof(BOOL), 0) == (HRESULT)1) // S_FALSE: not ready yet
        {
            if (Environment.TickCount64 > giveUpAt)
                return true;
            Thread.Yield();
        }
        return any;
    }

    public void Dispose()
    {
        _bloomA.Dispose();
        _bloomB.Dispose();
        Com.Release(ref _anyPassed);
        Com.Release(ref _differs);
        Com.Release(ref _constants);
        Com.Release(ref _sampler);
        Com.Release(ref _blur);
        Com.Release(ref _bright);
        Com.Release(ref _main);
        Com.Release(ref _vertexShader);
        if (_context is not null)
        {
            _context->ClearState();
            _context->Flush();
        }
        Com.Release(ref _context);
        Com.Release(ref _device);
    }

    private void Pass(ID3D11PixelShader* shader, ref ShaderConstants constants, ID3D11ShaderResourceView* source,
        ID3D11ShaderResourceView* bloom, ID3D11RenderTargetView* target, int width, int height)
    {
        ID3D11DeviceContext* context = _context;
        Unbind();
        fixed (ShaderConstants* data = &constants)
            context->UpdateSubresource((ID3D11Resource*)_constants, 0, null, data, 0, 0);
        context->OMSetRenderTargets(1, &target, null);
        var viewport = new D3D11_VIEWPORT { Width = width, Height = height, MaxDepth = 1 };
        context->RSSetViewports(1, &viewport);
        ID3D11ShaderResourceView** views = stackalloc ID3D11ShaderResourceView*[2];
        views[0] = source;
        views[1] = bloom;
        context->PSSetShaderResources(0, 2, views);
        context->PSSetShader(shader, null, 0);
        context->Draw(3, 0);
    }

    /// <summary>Unbinds textures and targets, so a texture can switch between being read and written.</summary>
    private void Unbind()
    {
        ID3D11ShaderResourceView** views = stackalloc ID3D11ShaderResourceView*[2];
        _context->PSSetShaderResources(0, 2, views);
        _context->OMSetRenderTargets(0, null, null);
    }

    private void CreatePipeline()
    {
        ShaderCode code = ShaderCode.Instance;
        fixed (byte* vs = code.Vertex)
        {
            ID3D11VertexShader* shader;
            _device->CreateVertexShader(vs, (nuint)code.Vertex.Length, null, &shader);
            _vertexShader = shader;
        }
        _main = CreatePixelShader(code.Main);
        _bright = CreatePixelShader(code.Bright);
        _blur = CreatePixelShader(code.Blur);
        _differs = CreatePixelShader(code.Differs);

        var queryDesc = new D3D11_QUERY_DESC { Query = D3D11_QUERY.D3D11_QUERY_OCCLUSION_PREDICATE };
        ID3D11Query* query;
        _device->CreateQuery(&queryDesc, &query);
        _anyPassed = query;

        var samplerDesc = new D3D11_SAMPLER_DESC
        {
            Filter = D3D11_FILTER.D3D11_FILTER_MIN_MAG_MIP_LINEAR,
            AddressU = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressV = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            AddressW = D3D11_TEXTURE_ADDRESS_MODE.D3D11_TEXTURE_ADDRESS_CLAMP,
            ComparisonFunc = D3D11_COMPARISON_FUNC.D3D11_COMPARISON_NEVER,
            MaxLOD = float.MaxValue,
        };
        ID3D11SamplerState* sampler;
        _device->CreateSamplerState(&samplerDesc, &sampler);
        _sampler = sampler;

        var bufferDesc = new D3D11_BUFFER_DESC
        {
            ByteWidth = (uint)sizeof(ShaderConstants),
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
        };
        ID3D11Buffer* buffer;
        _device->CreateBuffer(&bufferDesc, null, &buffer);
        _constants = buffer;
    }

    private ID3D11PixelShader* CreatePixelShader(byte[] code)
    {
        fixed (byte* ps = code)
        {
            ID3D11PixelShader* shader;
            _device->CreatePixelShader(ps, (nuint)code.Length, null, &shader);
            return shader;
        }
    }

    /// <summary>A texture the bloom chain draws into and then reads from.</summary>
    private sealed class RenderTexture : IDisposable
    {
        private ID3D11Texture2D* _texture;
        private ID3D11RenderTargetView* _target;
        private ID3D11ShaderResourceView* _view;
        private int _width;
        private int _height;

        public ID3D11RenderTargetView* Target => _target;

        public ID3D11ShaderResourceView* View => _view;

        public void EnsureSize(ID3D11Device* device, int width, int height)
        {
            if (_texture is not null && width == _width && height == _height)
                return;
            Dispose();
            var desc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                // Half floats: an 8-bit glow bands visibly in dark gradients.
                Format = DXGI_FORMAT.DXGI_FORMAT_R16G16B16A16_FLOAT,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
                Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
                BindFlags = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE,
            };
            ID3D11Texture2D* texture;
            device->CreateTexture2D(&desc, null, &texture);
            _texture = texture;
            ID3D11RenderTargetView* target;
            device->CreateRenderTargetView((ID3D11Resource*)texture, null, &target);
            _target = target;
            ID3D11ShaderResourceView* view;
            device->CreateShaderResourceView((ID3D11Resource*)texture, null, &view);
            _view = view;
            (_width, _height) = (width, height);
        }

        public void Dispose()
        {
            Com.Release(ref _view);
            Com.Release(ref _target);
            Com.Release(ref _texture);
        }
    }
}

/// <summary>Where an animated look is: the time and the grain frame, which picks the noise pattern.</summary>
internal readonly record struct FrameClock(double Seconds, long Frame)
{
    public static FrameClock At(double seconds, int framesPerSecond) =>
        new(seconds, framesPerSecond > 0 ? (long)(seconds * framesPerSecond) : 0);
}

/// <summary>The shader's constant buffer; the layout matches <c>cbuffer Params</c> in Effects.hlsl.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ShaderConstants
{
    public float Width, Height, Time, Seed;
    public float HasSource, Exposure, Contrast, Saturation;
    public float Vibrance, Temperature, Tint, Fade;
    public float Sepia, Posterize, Sharpen, Bloom;
    public float Halation, Aberration, Distortion, Vignette;
    public float VignetteSize, VignetteSoftness, Grain, GrainSize;
    public float GrainColor, Flicker, Weave, Dust;
    public float Letterbox, Scanlines, ScanlineSpacing, Curvature;
    public float Mask, ChromaBleed, Tracking, Jitter;
    public float Pixelate, Dither, ColorVision, ColorVisionStrength;
    public float ShadowR, ShadowG, ShadowB, ShadowA;
    public float HighlightR, HighlightG, HighlightB, HighlightA;
    public float BloomThreshold, PixelScale, SourceTexelX, SourceTexelY;
    public float BlurStepX, BlurStepY, Unused1, Unused2;
    public float PaletteSize, PaletteSpread, PaletteByBrightness, Dim;
    public fixed float Palette[Palettes.MaxColours * 4];

    public static ShaderConstants From(Look look, FrameClock frame, int width, int height, bool hasSource)
    {
        (double sr, double sg, double sb) = LookColour.Tint(look.ShadowHue, look.ShadowTint);
        (double hr, double hg, double hb) = LookColour.Tint(look.HighlightHue, look.HighlightTint);
        var constants = new ShaderConstants
        {
            Width = width,
            Height = height,
            Time = (float)(frame.Seconds % 3600),
            // Kept below 2^24 so the float holds it exactly.
            Seed = frame.Frame % 16_777_216,
            HasSource = hasSource ? 1 : 0,
            Exposure = (float)look.Exposure,
            Contrast = (float)look.Contrast,
            Saturation = (float)look.Saturation,
            Vibrance = (float)look.Vibrance,
            Temperature = (float)look.Temperature,
            Tint = (float)look.Tint,
            Fade = (float)look.Fade,
            Sepia = (float)look.Sepia,
            Posterize = look.Posterize,
            Sharpen = (float)look.Sharpen,
            Bloom = (float)look.Bloom,
            Halation = (float)look.Halation,
            Aberration = (float)look.Aberration,
            Distortion = (float)look.Distortion,
            Vignette = (float)look.Vignette,
            VignetteSize = (float)look.VignetteSize,
            VignetteSoftness = (float)look.VignetteSoftness,
            Grain = (float)look.Grain,
            GrainSize = (float)look.GrainSize,
            GrainColor = (float)look.GrainColor,
            Flicker = (float)look.Flicker,
            Weave = (float)look.Weave,
            Dust = (float)look.Dust,
            Letterbox = (float)look.Letterbox,
            Scanlines = (float)look.Scanlines,
            ScanlineSpacing = look.ScanlineSpacing,
            Curvature = (float)look.Curvature,
            Mask = (float)look.Mask,
            ChromaBleed = (float)look.ChromaBleed,
            Tracking = (float)look.Tracking,
            Jitter = (float)look.Jitter,
            Pixelate = look.Pixelate,
            Dither = (float)look.Dither,
            ColorVision = (float)look.ColorVision,
            ColorVisionStrength = (float)look.ColorVisionStrength,
            ShadowR = (float)sr,
            ShadowG = (float)sg,
            ShadowB = (float)sb,
            ShadowA = 1,
            HighlightR = (float)hr,
            HighlightG = (float)hg,
            HighlightB = (float)hb,
            HighlightA = 1,
            BloomThreshold = (float)look.BloomThreshold,
            Dim = (float)look.Dim,
        };
        if (Palettes.Get(look.Palette) is { } palette)
        {
            int count = Math.Min(palette.Colours.Count, Palettes.MaxColours);
            constants.PaletteSize = count;
            constants.PaletteSpread = (float)palette.Spread;
            constants.PaletteByBrightness = palette.ByBrightness ? 1 : 0;
            for (int i = 0; i < count; i++)
            {
                int colour = palette.Colours[i];
                constants.Palette[(i * 4) + 0] = ((colour >> 16) & 0xFF) / 255f;
                constants.Palette[(i * 4) + 1] = ((colour >> 8) & 0xFF) / 255f;
                constants.Palette[(i * 4) + 2] = (colour & 0xFF) / 255f;
            }
        }
        return constants;
    }
}

/// <summary>The compiled effect shaders, compiled once per process.</summary>
internal sealed class ShaderCode
{
    private const uint OptimizationLevel3 = 1 << 15; // D3DCOMPILE_OPTIMIZATION_LEVEL3
    private static readonly Lazy<ShaderCode> Compiled = new(() => new ShaderCode());

    private ShaderCode()
    {
        byte[] source = LoadSource();
        Vertex = Compile(source, "VS", "vs_4_0");
        Main = Compile(source, "Main", "ps_4_0");
        Bright = Compile(source, "Bright", "ps_4_0");
        Blur = Compile(source, "Blur", "ps_4_0");
        Differs = Compile(source, "Differs", "ps_4_0");
    }

    public static ShaderCode Instance => Compiled.Value;

    public byte[] Vertex { get; }

    public byte[] Main { get; }

    public byte[] Bright { get; }

    public byte[] Blur { get; }

    public byte[] Differs { get; }

    private static byte[] LoadSource()
    {
        using Stream stream = typeof(ShaderCode).Assembly.GetManifestResourceStream("DispHub.Effects.hlsl")
            ?? throw new InvalidOperationException("The effect shader is missing from the build.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static unsafe byte[] Compile(byte[] source, string entryPoint, string target)
    {
        ID3DBlob* code = null;
        ID3DBlob* errors = null;
        try
        {
            HRESULT result = PInvoke.D3DCompile(source, "Effects.hlsl", null, null, entryPoint, target, OptimizationLevel3, 0, &code, &errors);
            if (result.Failed)
            {
                string message = errors is null ? result.ToString() : Marshal.PtrToStringAnsi((nint)errors->GetBufferPointer()) ?? result.ToString();
                throw new InvalidOperationException($"The effect shader {entryPoint} didn't compile: {message}");
            }
            return new ReadOnlySpan<byte>(code->GetBufferPointer(), (int)code->GetBufferSize()).ToArray();
        }
        finally
        {
            if (code is not null)
                code->Release();
            if (errors is not null)
                errors->Release();
        }
    }
}

internal static unsafe class Com
{
    public static void Release<T>(ref T* pointer)
        where T : unmanaged
    {
        if (pointer is null)
            return;
        ((Windows.Win32.System.Com.IUnknown*)pointer)->Release();
        pointer = null;
    }

    public static T* Query<T>(void* unknown, Guid id)
        where T : unmanaged
    {
        void* result;
        ((Windows.Win32.System.Com.IUnknown*)unknown)->QueryInterface(&id, &result).ThrowOnFailure();
        return (T*)result;
    }
}
