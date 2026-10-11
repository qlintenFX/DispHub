using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace DispHub.Platform.Effects;

/// <summary>A display as DXGI sees it, in physical pixels.</summary>
internal readonly record struct OutputInfo(string DeviceName, Rectangle Bounds, double Scale, bool IsPrimary);

internal static unsafe class DxgiOutputs
{
    /// <summary>The displays that are part of the desktop.</summary>
    public static List<OutputInfo> Enumerate()
    {
        var outputs = new List<OutputInfo>();
        ForEach((adapter, output, desc) =>
        {
            if (!desc.AttachedToDesktop)
                return false;
            string name = desc.DeviceName.ToString();
            // A hybrid laptop can list a display under both GPUs; the first is the one driving it.
            if (outputs.Any(o => o.DeviceName == name))
                return false;
            RECT r = desc.DesktopCoordinates;
            uint dpi = 96;
            if (PInvoke.GetDpiForMonitor(desc.Monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpiX, out _).Succeeded)
                dpi = dpiX;
            outputs.Add(new OutputInfo(name, Rectangle.FromLTRB(r.left, r.top, r.right, r.bottom), dpi / 96.0, r.left == 0 && r.top == 0));
            return false;
        });
        return outputs;
    }

    /// <summary>Finds a display's output and the GPU driving it. The caller releases both.</summary>
    public static bool TryFind(string deviceName, out IDXGIAdapter1* adapter, out IDXGIOutput1* output)
    {
        IDXGIAdapter1* foundAdapter = null;
        IDXGIOutput1* foundOutput = null;
        ForEach((a, o, desc) =>
        {
            if (!desc.AttachedToDesktop || !desc.DeviceName.Equals(deviceName))
                return false;
            foundOutput = Com.Query<IDXGIOutput1>(o, IDXGIOutput1.IID_Guid);
            a->AddRef();
            foundAdapter = a;
            return true;
        });
        adapter = foundAdapter;
        output = foundOutput;
        return foundOutput is not null;
    }

    /// <summary>Calls <paramref name="visit"/> for each output of each GPU until it returns true.</summary>
    private static void ForEach(OutputVisitor visit)
    {
        IDXGIFactory1* factory;
        Guid factoryId = IDXGIFactory1.IID_Guid;
        PInvoke.CreateDXGIFactory1(&factoryId, (void**)&factory).ThrowOnFailure();
        try
        {
            for (uint a = 0; ; a++)
            {
                IDXGIAdapter1* adapter;
                if (factory->EnumAdapters1(a, &adapter).Failed)
                    return;
                try
                {
                    for (uint o = 0; ; o++)
                    {
                        IDXGIOutput* output;
                        if (adapter->EnumOutputs(o, &output).Failed)
                            break;
                        try
                        {
                            if (visit(adapter, output, output->GetDesc()))
                                return;
                        }
                        finally
                        {
                            output->Release();
                        }
                    }
                }
                finally
                {
                    adapter->Release();
                }
            }
        }
        finally
        {
            factory->Release();
        }
    }

    private delegate bool OutputVisitor(IDXGIAdapter1* adapter, IDXGIOutput* output, DXGI_OUTPUT_DESC desc);
}

internal enum CaptureResult
{
    NewFrame,
    NoChange,
    Lost,
}

/// <summary>
/// Copies what's on one display into a texture with Desktop Duplication. Windows excluded from capture, such as the
/// effect overlay itself, aren't in the copy.
/// </summary>
internal sealed unsafe class ScreenCapture : IDisposable
{
    private readonly ID3D11Device* _device;
    private readonly Copy _a = new();
    private readonly Copy _b = new();
    private IDXGIOutputDuplication* _duplication;
    private bool _aIsLatest;
    private int _frames;

    private ScreenCapture(IDXGIOutputDuplication* duplication, ID3D11Device* device)
    {
        _duplication = duplication;
        _device = device;
        DXGI_OUTDUPL_DESC desc;
        duplication->GetDesc(&desc);
        Width = (int)desc.ModeDesc.Width;
        Height = (int)desc.ModeDesc.Height;
        DXGI_RATIONAL rate = desc.ModeDesc.RefreshRate;
        RefreshRate = rate.Denominator > 0 && rate.Numerator > 0 ? rate.Numerator / (double)rate.Denominator : 60;
        IsRotated = desc.Rotation is not (DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_IDENTITY or DXGI_MODE_ROTATION.DXGI_MODE_ROTATION_UNSPECIFIED);
    }

    public int Width { get; }

    public int Height { get; }

    public double RefreshRate { get; }

    /// <summary>The display is turned (portrait); the copy then isn't the right way up.</summary>
    public bool IsRotated { get; }

    /// <summary>The latest copy; null until the first frame arrives.</summary>
    public ID3D11ShaderResourceView* View => _frames > 0 ? Latest.View : null;

    /// <summary>The copy before the latest, to see whether anything changed; null before the second frame.</summary>
    public ID3D11ShaderResourceView* PreviousView => _frames > 1 ? Older.View : null;

    private Copy Latest => _aIsLatest ? _a : _b;

    private Copy Older => _aIsLatest ? _b : _a;

    /// <summary>
    /// Starts duplicating <paramref name="output"/>. Fails while the secure desktop (UAC, lock screen) is up, when
    /// another GPU drives the display, or when too many apps are duplicating it.
    /// </summary>
    public static HRESULT TryStart(IDXGIOutput1* output, ID3D11Device* device, out ScreenCapture? capture)
    {
        IDXGIOutputDuplication* duplication;
        HRESULT result = output->DuplicateOutput((Windows.Win32.System.Com.IUnknown*)device, &duplication);
        capture = result.Succeeded ? new ScreenCapture(duplication, device) : null;
        return result;
    }

    /// <summary>Waits up to <paramref name="timeoutMs"/> for the desktop to change and copies it.</summary>
    public CaptureResult Next(ID3D11DeviceContext* context, uint timeoutMs)
    {
        DXGI_OUTDUPL_FRAME_INFO info;
        IDXGIResource* resource = null;
        HRESULT result = _duplication->AcquireNextFrame(timeoutMs, &info, &resource);
        if (result == HRESULT.DXGI_ERROR_WAIT_TIMEOUT)
            return CaptureResult.NoChange;
        if (result.Failed)
            return CaptureResult.Lost;
        try
        {
            // Only the mouse moved: the picture is the same.
            if (info.LastPresentTime == 0 && _frames > 0)
                return CaptureResult.NoChange;
            ID3D11Texture2D* frame = Com.Query<ID3D11Texture2D>(resource, ID3D11Texture2D.IID_Guid);
            try
            {
                _aIsLatest = !_aIsLatest;
                Latest.CopyFrom(_device, context, frame);
                _frames++;
            }
            finally
            {
                frame->Release();
            }
            return CaptureResult.NewFrame;
        }
        finally
        {
            resource->Release();
            _duplication->ReleaseFrame();
        }
    }

    public void Dispose()
    {
        _a.Dispose();
        _b.Dispose();
        Com.Release(ref _duplication);
    }

    /// <summary>A texture the shaders can read, holding one captured frame.</summary>
    private sealed class Copy : IDisposable
    {
        private ID3D11Texture2D* _texture;
        private ID3D11ShaderResourceView* _view;

        public ID3D11ShaderResourceView* View => _view;

        public void CopyFrom(ID3D11Device* device, ID3D11DeviceContext* context, ID3D11Texture2D* frame)
        {
            D3D11_TEXTURE2D_DESC desc;
            frame->GetDesc(&desc);
            if (_texture is not null)
            {
                D3D11_TEXTURE2D_DESC current;
                _texture->GetDesc(&current);
                if (current.Width != desc.Width || current.Height != desc.Height || current.Format != desc.Format)
                    Dispose();
            }
            if (_texture is null)
            {
                desc.MipLevels = 1;
                desc.ArraySize = 1;
                desc.Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT;
                desc.BindFlags = D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE;
                desc.CPUAccessFlags = 0;
                desc.MiscFlags = 0;
                ID3D11Texture2D* texture;
                device->CreateTexture2D(&desc, null, &texture);
                _texture = texture;
                ID3D11ShaderResourceView* view;
                device->CreateShaderResourceView((ID3D11Resource*)texture, null, &view);
                _view = view;
            }
            context->CopyResource((ID3D11Resource*)_texture, (ID3D11Resource*)frame);
        }

        public void Dispose()
        {
            Com.Release(ref _view);
            Com.Release(ref _texture);
        }
    }
}
