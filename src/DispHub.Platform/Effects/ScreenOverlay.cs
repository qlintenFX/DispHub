using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DispHub.Core.Effects;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.DirectComposition;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DispHub.Platform.Effects;

/// <summary>
/// Draws grain, vignette, scanlines, flicker and letterbox bars over every monitor.
/// </summary>
/// <remarks>
/// Each monitor gets a click-through, never-focused window whose content is a DirectComposition swap chain, drawn by
/// one small pixel shader on the GPU. Nothing is copied back to the CPU, so a 4K frame costs microseconds of CPU time.
/// Call everything from one thread.
/// </remarks>
public sealed unsafe class ScreenOverlay : IDisposable
{
    private const string ClassName = "DispHubEffectOverlay";
    private const uint OptimizationLevel3 = 1 << 15; // D3DCOMPILE_OPTIMIZATION_LEVEL3

    private readonly List<Surface> _surfaces = [];
    private ID3D11Device* _device;
    private ID3D11DeviceContext* _context;
    private ID3D11VertexShader* _vertexShader;
    private ID3D11PixelShader* _pixelShader;
    private ID3D11Buffer* _constants;
    private IDCompositionDevice* _composition;
    private IDXGIFactory2* _factory;
    private bool _hideFromCapture;
    private bool _disposed;

    public ScreenOverlay()
    {
        RegisterWindowClass();
        CreateDevice();
    }

    /// <summary>Monitor bounds in physical pixels, one per overlay window.</summary>
    public IReadOnlyList<Rectangle> Monitors => [.. _surfaces.Select(s => s.Bounds)];

    /// <summary>Makes sure there is one overlay per monitor; call after display changes.</summary>
    public void SyncMonitors()
    {
        List<Rectangle> monitors = EnumerateMonitors();
        if (monitors.SequenceEqual(_surfaces.Select(s => s.Bounds)))
            return;
        foreach (Surface surface in _surfaces)
            surface.Dispose();
        _surfaces.Clear();
        foreach (Rectangle bounds in monitors)
            _surfaces.Add(new Surface(this, bounds));
        SetHiddenFromCapture(_hideFromCapture);
        _composition->Commit();
    }

    public void SetHiddenFromCapture(bool hidden)
    {
        _hideFromCapture = hidden;
        foreach (Surface surface in _surfaces)
            PInvoke.SetWindowDisplayAffinity(surface.Window, hidden ? WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE : WINDOW_DISPLAY_AFFINITY.WDA_NONE);
    }

    /// <summary>Shows or hides the overlay on one monitor, e.g. while a game covers it.</summary>
    public void SetVisible(Rectangle monitor, bool visible)
    {
        foreach (Surface surface in _surfaces.Where(s => s.Bounds == monitor))
            surface.SetVisible(visible);
    }

    public void Hide()
    {
        foreach (Surface surface in _surfaces)
            surface.SetVisible(false);
    }

    /// <summary>Draws one frame on every visible monitor. <paramref name="seed"/> changes the grain pattern.</summary>
    public void Render(OverlaySettings settings, uint seed, double flicker)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (Surface surface in _surfaces.Where(s => s.IsVisible))
        {
            var constants = new Constants
            {
                Width = surface.Bounds.Width,
                Height = surface.Bounds.Height,
                Seed = seed,
                Grain = (float)settings.Grain,
                GrainSize = (float)settings.GrainSize,
                Vignette = (float)settings.Vignette,
                VignetteSize = (float)settings.VignetteSize,
                VignetteSoftness = (float)settings.VignetteSoftness,
                Scanlines = (float)settings.Scanlines,
                ScanlineSpacing = settings.ScanlineSpacing,
                Flicker = (float)flicker,
                Letterbox = (float)settings.LetterboxAspect,
            };
            surface.Draw(&constants);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (Surface surface in _surfaces)
            surface.Dispose();
        _surfaces.Clear();
        Release(ref _factory);
        Release(ref _composition);
        Release(ref _constants);
        Release(ref _pixelShader);
        Release(ref _vertexShader);
        Release(ref _context);
        Release(ref _device);
    }

    private void CreateDevice()
    {
        ID3D11Device* device;
        ID3D11DeviceContext* context;
        PInvoke.D3D11CreateDevice(null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, HMODULE.Null,
            D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT, null, 0, PInvoke.D3D11_SDK_VERSION, &device, null, &context)
            .ThrowOnFailure();
        _device = device;
        _context = context;

        using (var vs = new Blob(Compile("VS", "vs_4_0")))
        {
            ID3D11VertexShader* shader;
            _device->CreateVertexShader(vs.Pointer, vs.Size, null, &shader);
            _vertexShader = shader;
        }
        using (var ps = new Blob(Compile("PS", "ps_4_0")))
        {
            ID3D11PixelShader* shader;
            _device->CreatePixelShader(ps.Pointer, ps.Size, null, &shader);
            _pixelShader = shader;
        }

        var bufferDesc = new D3D11_BUFFER_DESC
        {
            ByteWidth = (uint)sizeof(Constants),
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = D3D11_BIND_FLAG.D3D11_BIND_CONSTANT_BUFFER,
        };
        ID3D11Buffer* buffer;
        _device->CreateBuffer(&bufferDesc, null, &buffer);
        _constants = buffer;

        IDXGIDevice* dxgiDevice;
        Guid dxgiDeviceId = IDXGIDevice.IID_Guid;
        _device->QueryInterface(&dxgiDeviceId, (void**)&dxgiDevice).ThrowOnFailure();
        try
        {
            IDXGIAdapter* adapter;
            dxgiDevice->GetAdapter(&adapter);
            IDXGIFactory2* factory;
            Guid factoryId = IDXGIFactory2.IID_Guid;
            adapter->GetParent(&factoryId, (void**)&factory);
            adapter->Release();
            _factory = factory;

            IDCompositionDevice* composition;
            Guid compositionId = IDCompositionDevice.IID_Guid;
            PInvoke.DCompositionCreateDevice(dxgiDevice, &compositionId, (void**)&composition).ThrowOnFailure();
            _composition = composition;
        }
        finally
        {
            dxgiDevice->Release();
        }
    }

    private static ID3DBlob* Compile(string entryPoint, string target)
    {
        byte[] source = Encoding.ASCII.GetBytes(OverlayShader.Source);
        ID3DBlob* code;
        ID3DBlob* errors = null;
        HRESULT result = PInvoke.D3DCompile(source, "overlay.hlsl", null, null, entryPoint, target, OptimizationLevel3, 0, &code, &errors);
        if (result.Failed)
        {
            string message = errors is null ? result.ToString() : Marshal.PtrToStringAnsi((nint)errors->GetBufferPointer()) ?? result.ToString();
            if (errors is not null)
                errors->Release();
            throw new InvalidOperationException($"Overlay shader didn't compile: {message}");
        }
        if (errors is not null)
            errors->Release();
        return code;
    }

    private static List<Rectangle> EnumerateMonitors()
    {
        var monitors = new List<Rectangle>();
        GCHandle handle = GCHandle.Alloc(monitors);
        try
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, null, &OnMonitor, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return monitors;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL OnMonitor(HMONITOR monitor, HDC hdc, RECT* bounds, LPARAM data)
    {
        var list = (List<Rectangle>)GCHandle.FromIntPtr(data).Target!;
        list.Add(Rectangle.FromLTRB(bounds->left, bounds->top, bounds->right, bounds->bottom));
        return true;
    }

    private static void RegisterWindowClass()
    {
        fixed (char* name = ClassName)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WindowProc,
                hInstance = (HINSTANCE)(nint)PInvoke.GetModuleHandle(default(PCWSTR)).Value,
                lpszClassName = name,
            };
            // Registering twice (a second ScreenOverlay) fails harmlessly with "class already exists".
            _ = PInvoke.RegisterClassEx(in windowClass);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProc(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        const uint WM_NCHITTEST = 0x0084;
        const int HTTRANSPARENT = -1;
        return message == WM_NCHITTEST ? (LRESULT)HTTRANSPARENT : PInvoke.DefWindowProc(window, message, wParam, lParam);
    }

    private static void Release<T>(ref T* pointer)
        where T : unmanaged
    {
        if (pointer is null)
            return;
        ((IUnknown*)pointer)->Release();
        pointer = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Constants
    {
        public float Width, Height, Seed, Grain;
        public float GrainSize, Vignette, VignetteSize, VignetteSoftness;
        public float Scanlines, ScanlineSpacing, Flicker, Letterbox;
    }

    private readonly ref struct Blob(ID3DBlob* blob)
    {
        public void* Pointer => blob->GetBufferPointer();
        public nuint Size => blob->GetBufferSize();
        public void Dispose() => blob->Release();
    }

    /// <summary>One monitor: its window, swap chain and composition target.</summary>
    private sealed class Surface : IDisposable
    {
        private readonly ScreenOverlay _owner;
        private IDXGISwapChain1* _swapChain;
        private ID3D11RenderTargetView* _target;
        private IDCompositionTarget* _compositionTarget;
        private IDCompositionVisual* _visual;

        public Surface(ScreenOverlay owner, Rectangle bounds)
        {
            _owner = owner;
            Bounds = bounds;
            fixed (char* name = ClassName)
            {
                Window = PInvoke.CreateWindowEx(
                    WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP | WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
                    WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
                    name, name, WINDOW_STYLE.WS_POPUP, bounds.X, bounds.Y, bounds.Width, bounds.Height,
                    HWND.Null, HMENU.Null, HINSTANCE.Null, null);
            }
            if (Window.IsNull)
                throw new InvalidOperationException("Couldn't create the effect overlay window.");
            PInvoke.SetLayeredWindowAttributes(Window, default, 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);

            var desc = new DXGI_SWAP_CHAIN_DESC1
            {
                Width = (uint)bounds.Width,
                Height = (uint)bounds.Height,
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
                BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = 2,
                Scaling = DXGI_SCALING.DXGI_SCALING_STRETCH,
                SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
                AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_PREMULTIPLIED,
            };
            IDXGISwapChain1* swapChain;
            owner._factory->CreateSwapChainForComposition((IUnknown*)owner._device, &desc, null, &swapChain);
            _swapChain = swapChain;

            ID3D11Texture2D* buffer;
            Guid textureId = ID3D11Texture2D.IID_Guid;
            _swapChain->GetBuffer(0, &textureId, (void**)&buffer);
            ID3D11RenderTargetView* target;
            owner._device->CreateRenderTargetView((ID3D11Resource*)buffer, null, &target);
            buffer->Release();
            _target = target;

            IDCompositionTarget* compositionTarget;
            owner._composition->CreateTargetForHwnd(Window, true, &compositionTarget);
            _compositionTarget = compositionTarget;
            IDCompositionVisual* visual;
            owner._composition->CreateVisual(&visual);
            _visual = visual;
            _visual->SetContent((IUnknown*)_swapChain);
            _compositionTarget->SetRoot(_visual);
        }

        public HWND Window { get; }
        public Rectangle Bounds { get; }
        public bool IsVisible { get; private set; }

        public void SetVisible(bool visible)
        {
            if (visible == IsVisible)
                return;
            IsVisible = visible;
            PInvoke.ShowWindow(Window, visible ? SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE : SHOW_WINDOW_CMD.SW_HIDE);
        }

        public void Draw(Constants* constants)
        {
            ID3D11DeviceContext* context = _owner._context;
            context->UpdateSubresource((ID3D11Resource*)_owner._constants, 0, null, constants, 0, 0);
            ID3D11RenderTargetView* target = _target;
            context->OMSetRenderTargets(1, &target, null);
            var viewport = new D3D11_VIEWPORT { Width = Bounds.Width, Height = Bounds.Height, MaxDepth = 1 };
            context->RSSetViewports(1, &viewport);
            context->IASetPrimitiveTopology(D3D_PRIMITIVE_TOPOLOGY.D3D_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            context->VSSetShader(_owner._vertexShader, null, 0);
            context->PSSetShader(_owner._pixelShader, null, 0);
            ID3D11Buffer* buffer = _owner._constants;
            context->PSSetConstantBuffers(0, 1, &buffer);
            context->Draw(3, 0);
            _swapChain->Present(1, 0);
        }

        public void Dispose()
        {
            Release(ref _visual);
            Release(ref _compositionTarget);
            Release(ref _target);
            Release(ref _swapChain);
            PInvoke.DestroyWindow(Window);
        }
    }
}
