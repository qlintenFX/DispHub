using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Effects;
using DispHub.Localization;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>
/// Screen effects (prototype). Every value applies live. Amounts are shown in percent; the config keeps 0-1.
/// </summary>
public sealed partial class EffectsViewModel : PageViewModel
{
    private readonly AppController _app;
    private readonly EffectsService _effects;
    private bool _loading;

    public EffectsViewModel(AppController app, EffectsService effects)
        : base(Loc.Get("Nav_Effects"), Wpf.Ui.Controls.SymbolRegular.Sparkle24)
    {
        _app = app;
        _effects = effects;
        Load();
    }

    public IReadOnlyList<EffectPresetChoice> Presets { get; } =
        [.. EffectPresets.All.Select(p => new EffectPresetChoice(p.Id, Loc.Get($"EffectPreset_{p.Id}")))];

    public IReadOnlyList<LetterboxChoice> Letterboxes { get; } =
    [
        new(0, Loc.Get("Effects_LetterboxOff")),
        new(1.85, "1.85 : 1"),
        new(2.0, "2.00 : 1"),
        new(2.39, "2.39 : 1"),
    ];

    [ObservableProperty] public partial bool Enabled { get; set; }
    [ObservableProperty] public partial EffectPresetChoice? Preset { get; set; }
    [ObservableProperty] public partial string? Problem { get; set; }

    [ObservableProperty] public partial double Saturation { get; set; }
    [ObservableProperty] public partial double Contrast { get; set; }
    [ObservableProperty] public partial double Fade { get; set; }
    [ObservableProperty] public partial double Sepia { get; set; }
    [ObservableProperty] public partial double TintHue { get; set; }
    [ObservableProperty] public partial double TintStrength { get; set; }

    [ObservableProperty] public partial double Grain { get; set; }
    [ObservableProperty] public partial double GrainSize { get; set; }
    [ObservableProperty] public partial double GrainFps { get; set; }
    [ObservableProperty] public partial double Vignette { get; set; }
    [ObservableProperty] public partial double VignetteSize { get; set; }
    [ObservableProperty] public partial double VignetteSoftness { get; set; }
    [ObservableProperty] public partial double Scanlines { get; set; }
    [ObservableProperty] public partial double ScanlineSpacing { get; set; }
    [ObservableProperty] public partial double Flicker { get; set; }
    [ObservableProperty] public partial LetterboxChoice? Letterbox { get; set; }

    [ObservableProperty] public partial bool HideOverFullScreen { get; set; }
    [ObservableProperty] public partial bool HideOverlayFromCapture { get; set; }

    partial void OnPresetChanged(EffectPresetChoice? value)
    {
        if (_loading || value is null || EffectPresets.All.FirstOrDefault(p => p.Id == value.Id) is not { } preset)
            return;
        EffectsConfig config = _app.Config.Effects;
        config.Preset = preset.Id;
        config.Look = preset.Look;
        config.Overlay = preset.Overlay;
        config.Enabled = true;
        Load();
        Commit();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPropertyChanged(e);
        if (_loading || e.PropertyName is nameof(Preset) or nameof(Problem))
            return;

        EffectsConfig config = _app.Config.Effects;
        config.Enabled = Enabled;
        config.HideOverFullScreen = HideOverFullScreen;
        config.HideOverlayFromCapture = HideOverlayFromCapture;
        if (e.PropertyName is not (nameof(Enabled) or nameof(HideOverFullScreen) or nameof(HideOverlayFromCapture)))
        {
            config.Look = new LookSettings(Saturation / 100, Contrast / 100, Fade / 100, Sepia / 100, TintHue, TintStrength / 100).Clamp();
            config.Overlay = new OverlaySettings(
                Grain / 100, GrainSize, (int)GrainFps, Vignette / 100, VignetteSize / 100, VignetteSoftness / 100,
                Scanlines / 100, (int)ScanlineSpacing, Flicker / 100, Letterbox?.Aspect ?? 0).Clamp();
            config.Preset = null;
            SetPresetSilently(null);
        }
        Commit();
    }

    [RelayCommand]
    private void Reset()
    {
        EffectsConfig config = _app.Config.Effects;
        config.Preset = null;
        config.Look = new LookSettings();
        config.Overlay = new OverlaySettings();
        Load();
        Commit();
    }

    private void Commit()
    {
        _effects.Apply();
        Problem = _effects.Problem;
        _app.EffectsChanged();
    }

    private void Load()
    {
        _loading = true;
        try
        {
            EffectsConfig config = _app.Config.Effects;
            LookSettings look = config.Look;
            OverlaySettings overlay = config.Overlay;
            Enabled = config.Enabled;
            Preset = Presets.FirstOrDefault(p => p.Id == config.Preset);
            Saturation = look.Saturation * 100;
            Contrast = look.Contrast * 100;
            Fade = look.Fade * 100;
            Sepia = look.Sepia * 100;
            TintHue = look.TintHue;
            TintStrength = look.TintStrength * 100;
            Grain = overlay.Grain * 100;
            GrainSize = overlay.GrainSize;
            GrainFps = overlay.GrainFps;
            Vignette = overlay.Vignette * 100;
            VignetteSize = overlay.VignetteSize * 100;
            VignetteSoftness = overlay.VignetteSoftness * 100;
            Scanlines = overlay.Scanlines * 100;
            ScanlineSpacing = overlay.ScanlineSpacing;
            Flicker = overlay.Flicker * 100;
            Letterbox = Letterboxes.MinBy(l => Math.Abs(l.Aspect - overlay.LetterboxAspect));
            HideOverFullScreen = config.HideOverFullScreen;
            HideOverlayFromCapture = config.HideOverlayFromCapture;
        }
        finally
        {
            _loading = false;
        }
    }

    private void SetPresetSilently(EffectPresetChoice? value)
    {
        bool wasLoading = _loading;
        _loading = true;
        Preset = value;
        _loading = wasLoading;
    }
}

public sealed record EffectPresetChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record LetterboxChoice(double Aspect, string Name)
{
    public override string ToString() => Name;
}
