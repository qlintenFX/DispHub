// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Controls;
using DispHub.Core.Color;
using DispHub.Core.Hotkeys;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform.Shell;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>
/// The first-run walkthrough: what DispHub does, where its icon lives and what a left or right click on it does,
/// the hotkeys, and the power button. Shown once; the choices in it are ordinary settings. It ends
/// with the offer of a tour of the main window.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    public const int StepCount = 4;

    private readonly AppController _app;
    private readonly IAutostart _autostart;
    private readonly bool _firstRun;
    private bool _loading = true;
    private bool _finished;

    public OnboardingViewModel(AppController app, IAutostart autostart)
    {
        _app = app;
        _autostart = autostart;
        _firstRun = !app.Config.Ui.OnboardingDone;
        TrayLeftClick = app.Config.Ui.TrayLeftClick;
        List<Profile> withHotkeys = [.. app.Config.Profiles.Where(p => p.Hotkey is not null)];
        foreach (Profile profile in withHotkeys.Take(4))
            Hotkeys.Add(new HotkeyLine(profile.Name, profile.Hotkey!));
        if (app.Config.Hotkeys.TogglePause is { } onOff)
            Hotkeys.Add(new HotkeyLine(Loc.Get("Settings_Pause"), onOff));

        // The picture shows one of the user's own hotkeys at work, so it matches their keyboard and language: the
        // second profile with one, which on a first run is Gaming, so the picture changes visibly.
        if ((withHotkeys.ElementAtOrDefault(1) ?? withHotkeys.FirstOrDefault()) is { } shown &&
            HotkeyGesture.TryParse(shown.Hotkey, out HotkeyGesture gesture))
        {
            ShownKeys = [.. gesture.Keys.Select((key, i) => new KeyCap(key, i == 0))];
            ShownName = shown.Name;
            _shownColor = shown.Color;
        }
        _loading = false;
        _ = LoadAutostartAsync();
    }

    /// <summary>The walkthrough is done (finished or closed); true when the tour was asked for.</summary>
    public event EventHandler<bool>? Finished;

    public IReadOnlyList<TrayClickAction> TrayActions { get; } = [TrayClickAction.QuickPanel, TrayClickAction.OpenWindow, TrayClickAction.ShowProfileMenu];

    public ObservableCollection<HotkeyLine> Hotkeys { get; } = [];

    private readonly ColorSettings _shownColor = ColorSettings.Neutral;

    /// <summary>The keys of the hotkey the picture shows; none when no profile has one.</summary>
    public IReadOnlyList<KeyCap> ShownKeys { get; } = [];

    /// <summary>The name of the profile that hotkey switches to, in its on-screen message.</summary>
    public string ShownName { get; } = "";

    public bool HasShownHotkey => ShownKeys.Count > 0;

    /// <summary>The wildflowers in that profile's colours (made when the step first shows).</summary>
    public ImageSource ShownPicture => field ??= ColorPreview.Photo("flowers", _shownColor);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirst), nameof(IsLast), nameof(IsWelcome), nameof(IsTray), nameof(IsHotkeys), nameof(IsReady), nameof(StepText))]
    public partial int Step { get; set; }

    [ObservableProperty] public partial TrayClickAction TrayLeftClick { get; set; }
    [ObservableProperty] public partial bool LaunchAtLogin { get; set; }

    public bool IsFirst => Step == 0;
    public bool IsLast => Step == StepCount - 1;
    public bool IsWelcome => Step == 0;
    public bool IsTray => Step == 1;
    public bool IsHotkeys => Step == 2;
    public bool IsReady => Step == 3;

    public string StepText => Loc.Format("Onboarding_Step", Step + 1, StepCount);

    partial void OnTrayLeftClickChanged(TrayClickAction value)
    {
        if (_loading)
            return;
        _app.Config.Ui.TrayLeftClick = value;
        _app.SettingsChanged();
    }

    async partial void OnLaunchAtLoginChanged(bool value)
    {
        if (_loading)
            return;
        try
        {
            await _autostart.SetEnabledAsync(value);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            // Settings shows why; the walkthrough just moves on.
        }
    }

    /// <summary>The next step; on the last one, the tour of the main window.</summary>
    [RelayCommand]
    private void Next()
    {
        if (IsLast)
            Complete(tour: true);
        else
            Step++;
    }

    [RelayCommand]
    private void Back()
    {
        if (!IsFirst)
            Step--;
    }

    /// <summary>Marks the walkthrough as seen, without the tour.</summary>
    [RelayCommand]
    private void Finish() => Complete(tour: false);

    /// <summary>Marks the walkthrough as seen, whether it was finished or closed.</summary>
    public void Complete(bool tour)
    {
        if (_finished)
            return;
        _finished = true;
        _app.Config.Ui.OnboardingDone = true;
        _app.SettingsChanged();
        Finished?.Invoke(this, tour);
    }

    private async Task LoadAutostartAsync()
    {
        try
        {
            AutostartState state = await _autostart.GetStateAsync();
            // A tray app is most use when it's already there: on by default for new users, with the switch showing
            // it. Off in Task Manager or by policy stays off.
            if (_firstRun && state == AutostartState.Disabled)
            {
                LaunchAtLogin = true;
                return;
            }
            _loading = true;
            LaunchAtLogin = state == AutostartState.Enabled;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
        }
        finally
        {
            _loading = false;
        }
    }
}

public sealed record HotkeyLine(string Name, string Keys);

/// <summary>A key on the walkthrough's picture; every key after the first has a plus before it.</summary>
public sealed record KeyCap(string Name, bool IsFirst);
