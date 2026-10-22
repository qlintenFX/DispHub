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
        _effects.Changed += (_, _) => Refresh();
        _app.PowerChanged += (_, _) => Refresh();
    }

    /// <summary>Opens the main window; set by the tray.</summary>
    public Action? OpenWindow { get; set; }

    /// <summary>Asks the panel to close.</summary>
    public event EventHandler? CloseRequested;

    public ObservableCollection<QuickProfile> Profiles { get; } = [];

    /// <summary>Profiles on (the switch at the top).</summary>
    [ObservableProperty] public partial bool IsActive { get; set; }

    /// <summary>DispHub's master switch (the power button).</summary>
    [ObservableProperty] public partial bool IsOn { get; set; }
    [ObservableProperty] public partial string Status { get; set; } = "";
    [ObservableProperty] public partial bool CanAdjust { get; set; }
    [ObservableProperty] public partial string AdjustingText { get; set; } = "";
    [ObservableProperty] public partial double Brightness { get; set; }
    [ObservableProperty] public partial double Warmth { get; set; }
    /// <summary>Looks to pick from: off, then the Effects page's own look, saved looks and the built-in ones.</summary>
    public ObservableCollection<QuickLook> Looks { get; } = [];

    [ObservableProperty] public partial bool ShowEffects { get; set; }
    [ObservableProperty] public partial QuickLook? Look { get; set; }

    public void Refresh()
    {
        _loading = true;
        try
        {
            AppConfig config = _app.Config;
            RuleOutput? current = _app.Current;
            IsActive = _app.ProfilesOn;
            IsOn = !config.IsOff;

            Profiles.Clear();
            foreach (Profile profile in config.Profiles.Take(8))
                Profiles.Add(new QuickProfile(this, profile.Id, profile.Name, Controls.ColorPreview.Render(profile.Color), !config.IsPaused && !config.IsOff && current?.ProfileId == profile.Id));

            Profile? active = current?.ProfileId is { } id ? _app.FindProfile(id) : null;
            Status = config.IsOff ? Loc.Get("Quick_Off")
                : config.IsPaused || active is null ? Loc.Get("Quick_Paused")
                : Loc.Format(current!.Reason switch
                {
                    TargetReason.Schedule => "Quick_FromSchedule",
                    TargetReason.AppRule => "Quick_FromApp",
                    _ => "Quick_Active",
                }, active.Name);

            _adjusting = config.IsPaused || config.IsOff ? null : active?.Id;
            CanAdjust = _adjusting is not null;
            // Always a line here, so the sliders don't move when there's nothing to adjust.
            AdjustingText = _adjusting is null ? Loc.Get("Quick_NothingToAdjust") : Loc.Format("Quick_Adjusting", active!.Name);
            Brightness = active?.Color.Brightness ?? 0;
            Warmth = active?.Color.TemperatureK ?? ColorSettings.NeutralTemperatureK;

            ShowEffects = config.Ui.ShowEffects && _app.Features.ScreenEffects;
            RefreshLooks(config.Effects);
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (!_loading && value != _app.ProfilesOn)
            _app.SetProfilesOn(value, ChangeSource.Tray);
    }

    partial void OnBrightnessChanged(double value) => Adjust(c => c with { Brightness = (int)Math.Round(value) });

    partial void OnWarmthChanged(double value) => Adjust(c => c with { TemperatureK = (int)(Math.Round(value / 100) * 100) });

    partial void OnLookChanged(QuickLook? value)
    {
        if (_loading || value is null)
            return;
        EffectsConfig effects = _app.Config.Effects;
        if (value.Id is not null && !value.IsCurrent && LookLibrary.Find(effects, value.Id) is { } look)
        {
            (effects.Preset, effects.Look, effects.Strength) = (value.Id, look, 1);
            _app.EffectsChanged();
        }
        // Picking a look while DispHub is off turns it on, like picking a profile.
        _effects.SetEnabled(value.Id is not null, ChangeSource.Tray);
    }

    private void RefreshLooks(EffectsConfig effects)
    {
        Looks.Clear();
        Looks.Add(new QuickLook(null, Loc.Get("Quick_EffectOff")));
        // A look changed on the Effects page shows first, as it is now.
        bool changed = !effects.Look.IsEmpty &&
            (LookLibrary.Find(effects, effects.Preset) != effects.Look || Math.Abs(effects.Strength - 1) > 0.005);
        QuickLook? current = changed ? new QuickLook("", Loc.Format("Quick_EffectChanged", _effects.NameOf(effects.Preset)), IsCurrent: true) : null;
        if (current is not null)
            Looks.Add(current);
        foreach (SavedLook saved in effects.SavedLooks)
            Looks.Add(new QuickLook(saved.Key, saved.Name));
        foreach (LookPreset preset in Core.Effects.Looks.All)
            Looks.Add(new QuickLook(preset.Id, Loc.Get($"Look_{preset.Id}")));
        Look = !effects.Enabled || effects.Look.IsEmpty || _app.Config.IsOff ? Looks[0]
            : current ?? Looks.FirstOrDefault(l => l.Id is not null && l.Id == effects.Preset) ?? Looks[0];
    }

    internal void Activate(Guid profileId) => _app.ActivateProfile(profileId, ChangeSource.Tray);

    [RelayCommand]
    private void Open()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        OpenWindow?.Invoke();
    }

    [RelayCommand]
    private void TogglePower() => _app.ToggleOff(ChangeSource.Tray);

    private void Adjust(Func<ColorSettings, ColorSettings> change)
    {
        if (_loading || _adjusting is not { } id || _app.FindProfile(id) is not { } profile)
            return;
        _app.UpdateProfileColor(id, change(profile.Color).Clamp());
    }
}

/// <param name="Id">The look's id; null for off.</param>
/// <param name="IsCurrent">The Effects page's look as changed there, rather than a stored look.</param>
public sealed record QuickLook(string? Id, string Name, bool IsCurrent = false)
{
    public override string ToString() => Name;
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
