using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Color;
using DispHub.Core.Effects;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Localization;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>
/// The panel that opens from the notification area: switch profile, pause, nudge brightness and warmth of what's on
/// screen, and turn effects on or off, without opening the main window.
/// </summary>
public sealed partial class QuickPanelViewModel : ObservableObject
{
    private readonly AppController _app;
    private readonly EffectsService _effects;
    private bool _loading;
    private Guid? _adjusting;

    public QuickPanelViewModel(AppController app, EffectsService effects)
    {
        _app = app;
        _effects = effects;
        _app.StateChanged += (_, _) => Refresh();
        _app.ConfigChanged += (_, _) => Refresh();
        _app.ProfileEdited += (_, _) => Refresh();
    }

    /// <summary>Opens the main window; set by the tray.</summary>
    public Action? OpenWindow { get; set; }

    /// <summary>Quits DispHub; set by the tray.</summary>
    public Action? Exit { get; set; }

    /// <summary>Asks the panel to close.</summary>
    public event EventHandler? CloseRequested;

    public ObservableCollection<QuickProfile> Profiles { get; } = [];

    [ObservableProperty] public partial bool IsActive { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial bool CanAdjust { get; set; }
    [ObservableProperty] public partial string AdjustingText { get; set; } = "";
    [ObservableProperty] public partial double Brightness { get; set; }
    [ObservableProperty] public partial double Warmth { get; set; }
    [ObservableProperty] public partial bool EffectsOn { get; set; }
    [ObservableProperty] public partial string EffectsText { get; set; } = "";

    public void Refresh()
    {
        _loading = true;
        try
        {
            AppConfig config = _app.Config;
            RuleOutput? current = _app.Current;
            IsActive = !config.IsPaused;

            Profiles.Clear();
            foreach (Profile profile in config.Profiles.Take(8))
                Profiles.Add(new QuickProfile(this, profile.Id, profile.Name, Controls.ColorPreview.Render(profile.Color), !config.IsPaused && current?.ProfileId == profile.Id));

            Profile? active = current?.ProfileId is { } id ? _app.FindProfile(id) : null;
            Status = config.IsPaused || active is null
                ? Loc.Get("Quick_Paused")
                : Loc.Format(current!.Reason switch
                {
                    TargetReason.Schedule => "Quick_FromSchedule",
                    TargetReason.AppRule => "Quick_FromApp",
                    _ => "Quick_Active",
                }, active.Name);

            _adjusting = config.IsPaused ? null : active?.Id;
            CanAdjust = _adjusting is not null;
            AdjustingText = active is null ? "" : Loc.Format("Quick_Adjusting", active.Name);
            Brightness = active?.Color.Brightness ?? 0;
            Warmth = active?.Color.TemperatureK ?? ColorSettings.NeutralTemperatureK;

            EffectsOn = config.Effects.Enabled;
            EffectsText = config.Effects.Preset is { } preset ? Loc.Get($"EffectPreset_{preset}") : Loc.Get("Quick_EffectsCustom");
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (!_loading && value == _app.Config.IsPaused)
            _app.TogglePause(ChangeSource.Tray);
    }

    partial void OnBrightnessChanged(double value) => Adjust(c => c with { Brightness = (int)Math.Round(value) });

    partial void OnWarmthChanged(double value) => Adjust(c => c with { TemperatureK = (int)(Math.Round(value / 100) * 100) });

    partial void OnEffectsOnChanged(bool value)
    {
        if (_loading || value == _app.Config.Effects.Enabled)
            return;
        EffectsConfig effects = _app.Config.Effects;
        effects.Enabled = value;
        if (value && effects.Look.IsNeutral && effects.Overlay.IsEmpty && EffectPresets.All.Count > 0)
        {
            // Nothing set up yet: start from the first look so the switch does something visible.
            EffectPreset first = EffectPresets.All[0];
            (effects.Preset, effects.Look, effects.Overlay) = (first.Id, first.Look, first.Overlay);
        }
        _effects.Apply();
        _app.EffectsChanged();
        Refresh();
    }

    internal void Activate(Guid profileId) => _app.ActivateProfile(profileId, ChangeSource.Tray);

    [RelayCommand]
    private void Open()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        OpenWindow?.Invoke();
    }

    [RelayCommand]
    private void Quit()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        Exit?.Invoke();
    }

    private void Adjust(Func<ColorSettings, ColorSettings> change)
    {
        if (_loading || _adjusting is not { } id || _app.FindProfile(id) is not { } profile)
            return;
        _app.UpdateProfileColor(id, change(profile.Color).Clamp());
    }
}

public sealed partial class QuickProfile(QuickPanelViewModel owner, Guid id, string name, System.Windows.Media.ImageSource preview, bool isActive) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public System.Windows.Media.ImageSource Preview { get; } = preview;
    public bool IsActive { get; } = isActive;

    [RelayCommand]
    private void Activate() => owner.Activate(Id);
}
