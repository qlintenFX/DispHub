using System.Drawing;
using System.Windows.Threading;
using DispHub.Core.Effects;
using DispHub.Localization;
using DispHub.Platform.Effects;
using Microsoft.Extensions.Logging;

namespace DispHub.Services;

/// <summary>
/// Applies <see cref="EffectsConfig"/>: the colour grade through Windows' colour matrix and the grain and other
/// layers through <see cref="ScreenOverlay"/>. Runs on the UI thread; the GPU does the drawing.
/// </summary>
public sealed class EffectsService : IDisposable
{
    private readonly AppController _app;
    private readonly ILogger<EffectsService> _logger;
    private readonly ScreenColorEffect _color = new();
    private readonly DispatcherTimer _frames = new(DispatcherPriority.Render);
    private readonly DispatcherTimer _fullScreenCheck = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private readonly Random _random = new();
    private ScreenOverlay? _overlay;
    private uint _seed;

    public EffectsService(AppController app, ILogger<EffectsService> logger)
    {
        _app = app;
        _logger = logger;
        _frames.Tick += (_, _) => RenderFrame();
        _fullScreenCheck.Tick += (_, _) => UpdateVisibility();
        _app.MonitorsChanged += (_, _) =>
        {
            _overlay?.SyncMonitors();
            UpdateVisibility();
            RenderFrame();
        };
    }

    /// <summary>Why the last change couldn't be applied, for the Effects page; null when all is well.</summary>
    public string? Problem { get; private set; }

    public void Apply()
    {
        EffectsConfig effects = _app.Config.Effects;
        Problem = null;
        try
        {
            if (!effects.Enabled || effects.Look.IsNeutral)
                _color.Reset();
            else if (!_color.Apply(LookMatrix.ToFloats(LookMatrix.Build(effects.Look))))
                Problem = Loc.Get("Effects_ProblemColour");

            if (!effects.Enabled || effects.Overlay.IsEmpty)
            {
                StopOverlay();
                return;
            }

            _overlay ??= new ScreenOverlay();
            _overlay.SyncMonitors();
            _overlay.SetHiddenFromCapture(effects.HideOverlayFromCapture);
            UpdateVisibility();
            RenderFrame();

            OverlaySettings overlay = effects.Overlay;
            _frames.Interval = TimeSpan.FromSeconds(1.0 / Math.Max(overlay.GrainFps, 12));
            if (overlay.IsAnimated)
                _frames.Start();
            else
                _frames.Stop();
            if (effects.HideOverFullScreen)
                _fullScreenCheck.Start();
            else
                _fullScreenCheck.Stop();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            _logger.LogError(ex, "Couldn't start screen effects");
            Problem = Loc.Get("Effects_ProblemOverlay");
            StopOverlay();
        }
    }

    public void Dispose()
    {
        StopOverlay();
        _color.Dispose();
    }

    private void StopOverlay()
    {
        _frames.Stop();
        _fullScreenCheck.Stop();
        _overlay?.Dispose();
        _overlay = null;
    }

    private void UpdateVisibility()
    {
        if (_overlay is null)
            return;
        bool hideAll = _app.Config.Effects.HideOverFullScreen && FullScreenApps.IsGameOrPresentationRunning();
        Rectangle? covered = _app.Config.Effects.HideOverFullScreen ? FullScreenApps.ForegroundFullScreenBounds(_overlay.Monitors) : null;
        foreach (Rectangle monitor in _overlay.Monitors)
            _overlay.SetVisible(monitor, !hideAll && monitor != covered);
    }

    private void RenderFrame()
    {
        if (_overlay is null)
            return;
        OverlaySettings overlay = _app.Config.Effects.Overlay;
        if (overlay.Grain > 0 && overlay.GrainFps > 0)
            _seed++;
        // Film flicker: a small, random dip in brightness each frame.
        double flicker = overlay.Flicker * _random.NextDouble() * 0.12;
        _overlay.Render(overlay, _seed, flicker);
    }
}
