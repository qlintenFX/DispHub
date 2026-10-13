using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DispHub.Core.Effects;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.DirectComposition;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.UI.WindowsAndMessaging;

namespace DispHub.Platform.Effects;

/// <summary>What stops a display from showing the full effect.</summary>
public enum EffectProblem
{
    None,

    /// <summary>The screen can't be captured right now (secure desktop, another app duplicating it); retrying.</summary>
    CaptureUnavailable,

    /// <summary>The screen can't be captured on this display (turned to portrait, or the system doesn't allow it).</summary>
    CaptureUnsupported,

    /// <summary>The GPU kept failing; effects stopped on this display.</summary>
    Failed,
}

/// <summary>
/// The effect on one display: a click-through, never-focused window covering it, filled by a DirectComposition swap
/// chain that a worker thread draws into. Looks that only add layers are drawn as transparency over the live
/// desktop. Looks that change the picture draw over it opaquely, from a captured copy of the screen that leaves out
/// the window itself.
/// </summary>
/// <remarks>The window belongs to the UI thread, so it can always be hidden even if the GPU hangs.</remarks>
internal sealed unsafe class EffectOutput : IDisposable
{
    private const string ClassName = "DispHubEffect";
    private const int MaxFailures = 5;
    private static readonly Lazy<bool> ClassRegistered = new(RegisterWindowClass);

    private readonly ILogger _logger;
    private readonly Thread _thread;
    private readonly AutoResetEvent _wake = new(false);
    private readonly HWND _window;
    private volatile Settings _settings;
    private volatile bool _stop;
    private volatile bool _captureAllowed;
    private int _problem;

    public EffectOutput(OutputInfo info, Look look, EffectOptions options, ILogger logger)
    {
        Info = info;
        _logger = logger;
        _ = ClassRegistered.Value;
        _window = CreateWindow(info);
        _settings = new Settings(look, options, Hidden: false);
        ApplyCaptureExclusion();
        _thread = new Thread(Run) { IsBackground = true, Name = $"Effects {info.DeviceName}", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    public OutputInfo Info { get; }

    public EffectProblem Problem => (EffectProblem)Volatile.Read(ref _problem);

    /// <summary>Raised on the worker thread when <see cref="Problem"/> changes.</summary>
    public event EventHandler? ProblemChanged;

    public void Update(Look look, EffectOptions options)
    {
        _settings = _settings with { Look = look, Options = options };
        _wake.Set();
    }

    /// <summary>Hides the effect on this display, e.g. while a game covers it.</summary>
    public void SetHidden(bool hidden)
    {
        if (_settings.Hidden == hidden)
            return;
        if (hidden)
            PInvoke.ShowWindow(_window, SHOW_WINDOW_CMD.SW_HIDE);
        _settings = _settings with { Hidden = hidden };
        _wake.Set();
    }

    public void Dispose()
    {
        PInvoke.ShowWindow(_window, SHOW_WINDOW_CMD.SW_HIDE);
        _stop = true;
        _wake.Set();
        PInvoke.DestroyWindow(_window);
        if (_thread.Join(TimeSpan.FromSeconds(3)))
            _wake.Dispose();
        else
            _logger.LogWarning("The effect thread for {Display} didn't stop", Info.DeviceName);
    }

    /// <summary>
    /// The effect window is always left out of screen capture: a window that changes the picture would otherwise
    /// capture itself, and Windows has one switch for that and for screenshots, recordings and screen sharing.
    /// If Windows can't do it (before version 2004), only layers are drawn.
    /// </summary>
    private void ApplyCaptureExclusion()
    {
        _captureAllowed = PInvoke.SetWindowDisplayAffinity(_window, WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);
    }

    private void Run()
    {
        int failures = 0;
        while (!_stop)
        {
            try
            {
                if (RunOnDevice())
                    failures = 0;
            }
            // Anything thrown here would end the app; an effect failing must only end the effect.
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Screen effect on {Display} failed", Info.DeviceName);
                Hide();
                if (++failures >= MaxFailures)
                {
                    SetProblem(EffectProblem.Failed);
                    return;
                }
                // Often a driver update or a GPU reset: give it a moment and start again.
                _wake.WaitOne(TimeSpan.FromSeconds(failures));
            }
        }
    }

    /// <summary>Draws until stopped (returns true) or until the GPU fails (throws).</summary>
    private bool RunOnDevice()
    {
        if (!DxgiOutputs.TryFind(Info.DeviceName, out IDXGIAdapter1* adapter, out IDXGIOutput1* output))
            throw new InvalidOperationException($"Display {Info.DeviceName} isn't there any more.");
        Surface? surface = null;
        ScreenCapture? capture = null;
        bool shown = false;
        try
        {
            surface = new Surface((IDXGIAdapter*)adapter, _window, Info.Bounds.Width, Info.Bounds.Height);
            var clock = Stopwatch.StartNew();
            Settings? drawn = null;
            long drawnFrame = -1;
            long retryCaptureAt = 0;
            bool captureUnsupported = false;
            int framesDrawn = 0;
            int framesUnchanged = 0;
            long statsAt = Environment.TickCount64 + 10_000;

            while (!_stop)
            {
                Settings settings = _settings;
                Look look = settings.Look;
                if (settings.Hidden || look.IsEmpty)
                {
                    Hide(ref shown);
                    Release(ref capture);
                    drawn = null;
                    _wake.WaitOne();
                    continue;
                }

                bool processing = look.NeedsCapture && _captureAllowed && !captureUnsupported;
                if (surface.EnsureSwapChain(opaque: processing))
                    drawn = null;
                int fps = look.IsAnimated ? look.GrainFps : 0;
                double now = clock.Elapsed.TotalSeconds;
                long frame = fps > 0 ? (long)(now * fps) : 0;
                bool due = !ReferenceEquals(settings, drawn) || (fps > 0 && frame != drawnFrame);
                int untilNextFrame = fps > 0 ? Math.Max(1, (int)Math.Ceiling((((frame + 1) / (double)fps) - now) * 1000)) : Timeout.Infinite;

                if (processing)
                {
                    if (capture is null)
                    {
                        if (Environment.TickCount64 < retryCaptureAt)
                        {
                            Hide(ref shown);
                            _wake.WaitOne(250);
                            continue;
                        }
                        HRESULT started = ScreenCapture.TryStart(output, surface.Device, out capture);
                        if (capture is null || capture.IsRotated)
                        {
                            Release(ref capture);
                            if (started == HRESULT.DXGI_ERROR_UNSUPPORTED || started.Succeeded)
                            {
                                // Not on this display: carry on with the layers only.
                                captureUnsupported = true;
                                SetProblem(EffectProblem.CaptureUnsupported);
                                _logger.LogInformation("Can't capture {Display} ({Result}); drawing layers only", Info.DeviceName, started);
                            }
                            else
                            {
                                SetProblem(EffectProblem.CaptureUnavailable);
                                retryCaptureAt = Environment.TickCount64 + 1000;
                            }
                            continue;
                        }
                        SetProblem(EffectProblem.None);
                        due = true;
                    }

                    // Wait for the desktop to change, but no longer than the next animation frame (or 100 ms, so
                    // new settings show up promptly). When a redraw is due anyway, just take the newest picture.
                    uint timeout = due ? 0u : (uint)Math.Min(fps > 0 ? untilNextFrame : 100, 100);
                    CaptureResult result = capture.Next(surface.Context, timeout);
                    // Presenting makes Windows report a new frame even when nothing beneath changed. Comparing the
                    // two latest captures on the GPU stops that loop, so a still desktop costs nothing.
                    if (result == CaptureResult.NewFrame && !due && !surface.Differs(capture))
                    {
                        result = CaptureResult.NoChange;
                        framesUnchanged++;
                    }
                    if (result == CaptureResult.Lost)
                    {
                        // Display mode change, secure desktop or a full-screen game: start a new duplication.
                        Hide(ref shown);
                        Release(ref capture);
                        retryCaptureAt = Environment.TickCount64 + 100;
                        continue;
                    }
                    if (capture.View is null || !(due || result == CaptureResult.NewFrame))
                        continue;
                }
                else
                {
                    Release(ref capture);
                    // Only reached with a look that needs capture when this display can't do it.
                    SetProblem(look.NeedsCapture ? EffectProblem.CaptureUnsupported : EffectProblem.None);
                    if (!due)
                    {
                        _wake.WaitOne(untilNextFrame);
                        continue;
                    }
                }

                surface.Draw(look, new FrameClock(now, frame), Info.Scale, capture);
                uint syncInterval = processing ? SyncInterval(capture!.RefreshRate, settings.Options.MaxFps) : 1;
                // Throws when the GPU was reset or removed; the outer loop then starts again.
                surface.Present(syncInterval).ThrowOnFailure();
                drawn = settings;
                drawnFrame = frame;
                framesDrawn++;
                if (Environment.TickCount64 >= statsAt)
                {
                    _logger.LogDebug("Effect on {Display}: {Frames} frames in 10 s ({Unchanged} unchanged captures skipped), processing {Processing}",
                        Info.DeviceName, framesDrawn, framesUnchanged, processing);
                    framesDrawn = 0;
                    framesUnchanged = 0;
                    statsAt = Environment.TickCount64 + 10_000;
                }
                if (!shown)
                {
                    PInvoke.ShowWindowAsync(_window, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
                    shown = true;
                }
            }
            return true;
        }
        finally
        {
            Hide(ref shown);
            Release(ref capture);
            surface?.Dispose();
            output->Release();
            adapter->Release();
        }
    }

    /// <summary>Presents every n-th refresh so a capped frame rate needs no timers.</summary>
    private static uint SyncInterval(double refreshRate, int maxFps) =>
        maxFps <= 0 ? 1u : (uint)Math.Clamp((int)Math.Round(refreshRate / maxFps), 1, 4);

    private void Hide()
    {
        bool shown = true;
        Hide(ref shown);
    }

    private void Hide(ref bool shown)
    {
        if (!shown)
            return;
        PInvoke.ShowWindowAsync(_window, SHOW_WINDOW_CMD.SW_HIDE);
        shown = false;
    }

    private static void Release(ref ScreenCapture? capture)
    {
        capture?.Dispose();
        capture = null;
    }

    private void SetProblem(EffectProblem problem)
    {
        if (Interlocked.Exchange(ref _problem, (int)problem) != (int)problem)
            ProblemChanged?.Invoke(this, EventArgs.Empty);
    }

    private static HWND CreateWindow(OutputInfo info)
    {
        HWND window;
        fixed (char* name = ClassName)
        {
            window = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP | WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
                WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
                name, name, WINDOW_STYLE.WS_POPUP, info.Bounds.X, info.Bounds.Y, info.Bounds.Width, info.Bounds.Height,
                HWND.Null, HMENU.Null, HINSTANCE.Null, null);
        }
        if (window.IsNull)
            throw new InvalidOperationException("Couldn't create the effect window.");
        PInvoke.SetLayeredWindowAttributes(window, default, 255, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
        return window;
    }

    private static bool RegisterWindowClass()
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
            return PInvoke.RegisterClassEx(in windowClass) != 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WindowProc(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        const int HTTRANSPARENT = -1;
        const int MA_NOACTIVATE = 3;
        return message switch
        {
            PInvoke.WM_NCHITTEST => (LRESULT)HTTRANSPARENT,
            PInvoke.WM_MOUSEACTIVATE => (LRESULT)MA_NOACTIVATE,
            _ => PInvoke.DefWindowProc(window, message, wParam, lParam),
        };
    }

    private sealed record Settings(Look Look, EffectOptions Options, bool Hidden);

    /// <summary>The GPU side of one display: renderer, composition and swap chain.</summary>
    private sealed class Surface : IDisposable
    {
        private readonly EffectRenderer _renderer;
        private readonly int _width;
        private readonly int _height;
        private IDXGIFactory2* _factory;
        private IDCompositionDevice* _composition;
        private IDCompositionTarget* _target;
        private IDCompositionVisual* _visual;
        private IDXGISwapChain1* _swapChain;
        private ID3D11RenderTargetView* _backBuffer;
        private bool? _opaque;
        private bool _contentPending;

        public Surface(IDXGIAdapter* adapter, HWND window, int width, int height)
        {
            (_width, _height) = (width, height);
            _renderer = new EffectRenderer(adapter, software: false);
            try
            {
                IDXGIDevice1* device = Com.Query<IDXGIDevice1>(_renderer.Device, IDXGIDevice1.IID_Guid);
                try
                {
                    // One frame queued at most: the effect lags the desktop as little as possible.
                    device->SetMaximumFrameLatency(1);
                    IDCompositionDevice* composition;
                    Guid compositionId = IDCompositionDevice.IID_Guid;
                    PInvoke.DCompositionCreateDevice((IDXGIDevice*)device, &compositionId, (void**)&composition).ThrowOnFailure();
                    _composition = composition;
                }
                finally
                {
                    device->Release();
                }
                IDXGIFactory2* factory;
                Guid factoryId = IDXGIFactory2.IID_Guid;
                adapter->GetParent(&factoryId, (void**)&factory);
                _factory = factory;

                IDCompositionTarget* target;
                _composition->CreateTargetForHwnd(window, true, &target);
                _target = target;
                IDCompositionVisual* visual;
                _composition->CreateVisual(&visual);
                _visual = visual;
                _target->SetRoot(_visual);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public ID3D11Device* Device => _renderer.Device;

        public ID3D11DeviceContext* Context => _renderer.Context;

        /// <summary>
        /// Makes the swap chain opaque (a processed picture) or transparent (layers only). Returns true when it was
        /// (re)created, so the caller redraws.
        /// </summary>
        public bool EnsureSwapChain(bool opaque)
        {
            if (_opaque == opaque)
                return false;
            Com.Release(ref _backBuffer);
            Com.Release(ref _swapChain);
            var desc = new DXGI_SWAP_CHAIN_DESC1
            {
                Width = (uint)_width,
                Height = (uint)_height,
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
                BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = 2,
                Scaling = DXGI_SCALING.DXGI_SCALING_STRETCH,
                SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
                AlphaMode = opaque ? DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_IGNORE : DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_PREMULTIPLIED,
            };
            IDXGISwapChain1* swapChain;
            _factory->CreateSwapChainForComposition((Windows.Win32.System.Com.IUnknown*)_renderer.Device, &desc, null, &swapChain);
            _swapChain = swapChain;
            ID3D11Texture2D* buffer;
            Guid textureId = ID3D11Texture2D.IID_Guid;
            _swapChain->GetBuffer(0, &textureId, (void**)&buffer);
            try
            {
                ID3D11RenderTargetView* view;
                _renderer.Device->CreateRenderTargetView((ID3D11Resource*)buffer, null, &view);
                _backBuffer = view;
            }
            finally
            {
                buffer->Release();
            }
            // The window switches to the new swap chain once it has a frame (see Present), so it never flashes.
            _contentPending = true;
            _opaque = opaque;
            return true;
        }

        public void Draw(Look look, FrameClock frame, double scale, ScreenCapture? capture)
        {
            if (capture is not null && capture.View is not null)
                _renderer.Draw(look, frame, scale, capture.View, capture.Width, capture.Height, _backBuffer, _width, _height);
            else
                _renderer.Draw(look, frame, scale, null, 0, 0, _backBuffer, _width, _height);
        }

        /// <summary>Compares the two latest captures, using the back buffer as scratch (it's redrawn before presenting).</summary>
        public bool Differs(ScreenCapture capture) =>
            capture.PreviousView is null || capture.Width != _width || capture.Height != _height ||
            _renderer.Differs(capture.View, capture.PreviousView, _backBuffer, _width, _height);

        public HRESULT Present(uint syncInterval)
        {
            HRESULT result = _swapChain->Present(syncInterval, 0);
            if (result.Succeeded && _contentPending)
            {
                _visual->SetContent((Windows.Win32.System.Com.IUnknown*)_swapChain);
                _composition->Commit();
                _contentPending = false;
            }
            return result;
        }

        public void Dispose()
        {
            Com.Release(ref _backBuffer);
            Com.Release(ref _swapChain);
            Com.Release(ref _visual);
            Com.Release(ref _target);
            if (_composition is not null)
                _composition->Commit();
            Com.Release(ref _composition);
            Com.Release(ref _factory);
            _renderer.Dispose();
        }
    }
}
