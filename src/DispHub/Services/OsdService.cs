// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Config;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Platform.Store;
using DispHub.Views;
using Wpf.Ui.Controls;

namespace DispHub.Services;

/// <summary>Shows the on-screen message when the profile changes from a hotkey or the tray.</summary>
public sealed class OsdService(AppController app, ThemeService theme)
{
    private OsdWindow? _window;
    private bool _hintShown;

    public void Attach() => app.StateChanged += (_, source) => OnStateChanged(source);

    /// <summary>A one-off message, shown at most once per session.</summary>
    public void ShowHint(string title)
    {
        if (_hintShown)
            return;
        _hintShown = true;
        GetWindow().Flash(title, SymbolRegular.Info24, TimeSpan.FromSeconds(3), app.Config.Ui.OsdPosition);
    }

    private void OnStateChanged(ChangeSource source)
    {
        if (source is not (ChangeSource.Hotkey or ChangeSource.Tray) || !app.Config.Ui.ShowOsd)
            return;

        // Just the name: it's read at a glance, often mid-game.
        TimeSpan duration = TimeSpan.FromMilliseconds(app.Config.Ui.OsdDurationMs);
        if (app.Config.IsOff || app.Config.IsPaused)
        {
            GetWindow().Flash(Loc.Get(app.Config.IsOff ? "Osd_Paused" : "Osd_ProfilesOff"), SymbolRegular.Power24, duration, app.Config.Ui.OsdPosition);
            return;
        }

        Profile? profile = app.Current?.ProfileId is { } id ? app.FindProfile(id) : null;
        if (profile is not null)
            GetWindow().Flash(profile.Name, SymbolRegular.Color24, duration, app.Config.Ui.OsdPosition);
    }

    private OsdWindow GetWindow()
    {
        if (_window is null)
        {
            _window = new OsdWindow();
            _window.SourceInitialized += (_, _) => theme.Attach(_window, flyout: true);
        }
        return _window;
    }
}

/// <summary>
/// Asks for a Store rating once, after DispHub has proven useful (a week of use and real profile switching).
/// Only in the Store build, and only while the main window is open so the dialog isn't a surprise.
/// </summary>
public sealed class ReviewPrompt(AppController app, ILicenseService license, AppState state)
{
    private static readonly TimeSpan MinimumUse = TimeSpan.FromDays(7);
    private const int MinimumSwitches = 10;

    // The Pro trial also ends after a week: someone who just lost Pro isn't asked until a few days after hearing so.
    private static readonly TimeSpan AfterTrialEnded = TimeSpan.FromDays(3);

    public void Attach()
    {
        app.StateChanged += (_, source) =>
        {
            // Only the first few switches matter for the rating prompt; stop writing the file after that.
            if (source is ChangeSource.Hotkey or ChangeSource.Tray && state.Data.ProfileSwitchCount < MinimumSwitches)
            {
                state.Data.ProfileSwitchCount++;
                state.Save();
            }
        };
    }

    /// <summary>Called whenever the main window is activated.</summary>
    public async Task OnWindowActivatedAsync(MainWindow window)
    {
        if (!license.CanPurchase || state.Data.ReviewPromptedUtc is not null
            || !ShouldAsk(DateTimeOffset.UtcNow, state.Data.FirstRunUtc, state.Data.ProfileSwitchCount, license.IsPro, state.Data.TrialEndShownUtc))
        {
            return;
        }

        state.Data.ReviewPromptedUtc = DateTimeOffset.UtcNow;
        state.Save();
        await license.RequestReviewAsync(new System.Windows.Interop.WindowInteropHelper(window).Handle);
    }

    /// <summary>
    /// A week of use and real switching; without Pro, also a few days since the trial-ended message (never with it).
    /// </summary>
    public static bool ShouldAsk(DateTimeOffset now, DateTimeOffset? firstRun, int switches, bool isPro, DateTimeOffset? trialEndShown)
    {
        if (firstRun is not { } first || now - first < MinimumUse || switches < MinimumSwitches)
            return false;
        return isPro || (trialEndShown is { } shown && now - shown >= AfterTrialEnded);
    }
}
