using System.Drawing;
using System.Windows.Threading;
using DispHub.Core.Effects;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform.Effects;
using Microsoft.Extensions.Logging;

namespace DispHub.Services;

/// <summary>What the screen effect is doing, for the Effects page and the quick panel.</summary>
public enum EffectsState
{
    Off,
    On,

    /// <summary>The chosen look needs Pro; nothing is shown.</summary>
    NeedsPro,

    /// <summary>On, but some displays only show the layers (the screen can't be captured there).</summary>
    LayersOnly,

    /// <summary>On, but waiting for the screen to be capturable again (e.g. after the lock screen).</summary>
    Waiting,

    Failed,
}

/// <summary>
/// Shows the right look on screen: the active profile's choice, or the Effects page's. Hides it over full-screen
/// games and apps when asked. UI thread only; the drawing happens on GPU threads in <see cref="EffectsEngine"/>.
/// </summary>
public sealed class EffectsService : IDisposable
{
    private readonly AppController _app;
    private readonly OsdService _osd;
    private readonly EffectsEngine _engine;
    private readonly DispatcherTimer _fullScreenCheck = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    // A profile's look hidden with the hotkey; it comes back with the hotkey or when another profile takes over.
    private Guid? _hiddenProfile;

    public EffectsService(AppController app, OsdService osd, ILogger<EffectsService> logger)
    {
        _app = app;
        _osd = osd;
        _engine = new EffectsEngine(logger);
        _engine.ProblemChanged += (_, _) => _dispatcher.BeginInvoke(UpdateState);
        _fullScreenCheck.Tick += (_, _) => UpdateFullScreen();
        _app.StateChanged += (_, _) => Apply();
        _app.ConfigChanged += (_, _) => Apply();
        _app.MonitorsChanged += (_, _) =>
        {
            _engine.RefreshDisplays();
            UpdateFullScreen();
        };
        _app.EffectsToggleRequested += (_, _) => Toggle(fromHotkey: true);
    }

    public EffectsState State { get; private set; }

    /// <summary>The look on screen (or that would be, for <see cref="EffectsState.NeedsPro"/>) and its id, if any.</summary>
    public Look? Current { get; private set; }

    public string? CurrentId { get; private set; }

    /// <summary>True when the active profile, not the Effects page, picks the look.</summary>
    public bool FromProfile { get; private set; }

    /// <summary>The state or look changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Shows what the config and the active profile ask for. Call after changing effect settings.</summary>
    public void Apply()
    {
        EffectsConfig effects = _app.Config.Effects;
        Profile? profile = _app.Current?.ProfileId is { } id ? _app.FindProfile(id) : null;
        if (_hiddenProfile is not null && _hiddenProfile != profile?.Id)
            _hiddenProfile = null;
        (Look? look, string? lookId) = _app.Config.Ui.ShowEffects
            ? LookLibrary.Effective(effects, profile, _app.Config.IsPaused)
            : (null, null);
        FromProfile = profile?.Effect is not null && !_app.Config.IsPaused && _app.Config.Ui.ShowEffects;
        if (FromProfile && _hiddenProfile == profile?.Id)
            (look, lookId) = (null, null);
        Current = look;
        CurrentId = lookId;

        bool allowed = look is not null && _app.Features.ScreenEffects;
        if (look is null || !allowed || look.IsEmpty)
        {
            _fullScreenCheck.Stop();
            _engine.Stop();
        }
        else
        {
            _engine.Apply(look, new EffectOptions(effects.MaxFps, effects.HideFromCapture, effects.MainDisplayOnly));
            if (effects.HideOverFullScreen)
            {
                _fullScreenCheck.Start();
                UpdateFullScreen();
            }
            else
            {
                _fullScreenCheck.Stop();
                foreach (Rectangle monitor in _engine.Monitors)
                    _engine.SetHidden(monitor, false);
            }
        }
        UpdateState(look is not null && !allowed);
    }

    /// <summary>
    /// Shows or hides effects (a hotkey or the quick panel). A look picked by the active profile is hidden until it's
    /// toggled back or another profile is on; otherwise the Effects page's look is turned on or off.
    /// </summary>
    public void Toggle(bool fromHotkey = false)
    {
        EffectsConfig effects = _app.Config.Effects;
        Profile? profile = _app.Current?.ProfileId is { } id ? _app.FindProfile(id) : null;
        if (profile?.Effect is not null and not LookLibrary.None && !_app.Config.IsPaused)
        {
            _hiddenProfile = _hiddenProfile == profile.Id ? null : profile.Id;
            Apply();
            if (fromHotkey)
                _osd.ShowEffects(_hiddenProfile is null ? NameOf(CurrentId) : null);
            return;
        }
        effects.Enabled = !effects.Enabled;
        if (effects.Enabled && effects.Look.IsEmpty)
        {
            // Nothing set up yet: start from a gentle look that does something visible.
            LookPreset first = Looks.Find("film")!;
            (effects.Preset, effects.Look) = (first.Id, first.Look);
        }
        _app.EffectsChanged();
        Apply();
        if (fromHotkey)
            _osd.ShowEffects(effects.Enabled ? NameOf(CurrentId) : null);
    }

    /// <summary>The display name of a look id: a built-in look's name, a saved look's name, or "Custom".</summary>
    public string NameOf(string? id)
    {
        if (Looks.Find(id) is { } preset)
            return Loc.Get($"Look_{preset.Id}");
        return _app.Config.Effects.SavedLooks.FirstOrDefault(s => s.Key == id)?.Name ?? Loc.Get("Effects_Custom");
    }

    public void Dispose()
    {
        _fullScreenCheck.Stop();
        _engine.Dispose();
    }

    private void UpdateFullScreen()
    {
        if (!_engine.IsRunning || !_app.Config.Effects.HideOverFullScreen)
            return;
        IReadOnlyList<Rectangle> monitors = _engine.Monitors;
        bool hideAll = FullScreenApps.IsGameOrPresentationRunning();
        Rectangle? covered = FullScreenApps.ForegroundFullScreenBounds(monitors);
        foreach (Rectangle monitor in monitors)
            _engine.SetHidden(monitor, hideAll || monitor == covered);
    }

    private void UpdateState() => UpdateState(State == EffectsState.NeedsPro);

    private void UpdateState(bool needsPro)
    {
        EffectsState state = needsPro ? EffectsState.NeedsPro
            : !_engine.IsRunning ? EffectsState.Off
            : _engine.Problem switch
            {
                EffectProblem.Failed => EffectsState.Failed,
                EffectProblem.CaptureUnsupported => EffectsState.LayersOnly,
                EffectProblem.CaptureUnavailable => EffectsState.Waiting,
                _ => EffectsState.On,
            };
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
