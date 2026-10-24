// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Color;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Localization;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>
/// The panel that opens from the notification area: switch profile, pause, and nudge brightness and warmth of what's
/// on screen, without opening the main window.
/// </summary>
public sealed partial class QuickPanelViewModel : ObservableObject
{
    private readonly AppController _app;
    private bool _loading;
    private Guid? _adjusting;

    public QuickPanelViewModel(AppController app)
    {
        _app = app;
        _app.StateChanged += (_, _) => Refresh();
        _app.ConfigChanged += (_, _) => Refresh();
        _app.ProfileEdited += (_, _) => Refresh();
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

    /// <summary>How far the warmth slider goes: from neutral down to this colour temperature.</summary>
    public const double WarmestK = 2700;

    /// <summary>The warmth slider's range, from neutral (0) to <see cref="WarmestK"/>.</summary>
    public const double WarmthRange = ColorSettings.NeutralTemperatureK - WarmestK;

    /// <summary>
    /// The warmth slider's position, from 0 (neutral) to <see cref="WarmthRange"/> (warmest). The slider runs left to
    /// right and fills as the colour gets warmer; a reversed slider would fill from the right-hand end.
    /// </summary>
    public double WarmthLevel
    {
        get => ColorSettings.NeutralTemperatureK - Warmth;
        set => Warmth = ColorSettings.NeutralTemperatureK - value;
    }

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

    partial void OnWarmthChanged(double value)
    {
        OnPropertyChanged(nameof(WarmthLevel));
        Adjust(c => c with { TemperatureK = (int)(Math.Round(value / 100) * 100) });
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

public sealed partial class QuickProfile(QuickPanelViewModel owner, Guid id, string name, System.Windows.Media.ImageSource preview, bool isActive) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public System.Windows.Media.ImageSource Preview { get; } = preview;
    public bool IsActive { get; } = isActive;

    [RelayCommand]
    private void Activate() => owner.Activate(Id);
}
