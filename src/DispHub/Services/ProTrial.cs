// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Licensing;
using DispHub.Core.Rules;
using DispHub.Localization;
using DispHub.Platform.Store;

namespace DispHub.Services;

/// <summary>
/// The Pro trial of a Microsoft Store install: every Pro feature for a week from the first run. It counts what Pro
/// did in that week and, once the week is over, says so once with the offer to keep it.
/// </summary>
public sealed class ProTrial(AppController app, ILicenseService license, AppState state, IUpgradeService upgrade, TimeProvider time)
{
    /// <summary>Starts the trial clock. Call before the controller starts, so the first colours already follow it.</summary>
    /// <param name="fakeStore">
    /// The --fake-store mode: "free" is the free tier after the trial, "ended" a trial that just ended, "ending" one
    /// that ends 90 seconds after start (to see the switch happen).
    /// </param>
    public void Attach(string? fakeStore)
    {
        DateTimeOffset now = time.GetUtcNow();
        DateTimeOffset? ends = fakeStore switch
        {
            "free" => null,
            "ended" => now.AddSeconds(-1),
            "ending" => now.AddSeconds(90),
            _ => state.Data.FirstRunUtc is { } first ? FeatureGate.TrialEnds(first) : null,
        };
        app.SetTrial(ends);

        app.StateChanged += (_, source) =>
        {
            if (source != ChangeSource.Automation || !app.InTrial || app.Current is not { } current)
                return;
            if (current.Reason == TargetReason.AppRule)
                state.Data.TrialAppSwitches++;
            else if (current.Reason == TargetReason.Schedule)
                state.Data.TrialScheduleSwitches++;
            else
                return;
            state.Save();
        };
    }

    /// <summary>
    /// For people who only use the tray and hotkeys: once the trial has ended, one Windows notification says so, and
    /// selecting it opens DispHub with the offer. Windows holds it back during games and Do not disturb.
    /// </summary>
    /// <param name="notify">Shows a notification (title, text, what selecting it does); false when it couldn't.</param>
    public void NotifyWhenEnded(Func<string, string, Action, bool> notify, Action openWindow)
    {
        void Check()
        {
            if (!JustEnded || state.Data.TrialEndNotifiedUtc is not null)
                return;
            if (notify(Loc.Get("Trial_EndedTitle"), Loc.Get("Trial_EndedGeneric"), openWindow))
            {
                state.Data.TrialEndNotifiedUtc = time.GetUtcNow();
                state.Save();
            }
        }

        app.LicenceChanged += (_, _) => Check();
        Check();
    }

    /// <summary>The trial is over, Pro wasn't bought and the offer hasn't been shown yet.</summary>
    private bool JustEnded =>
        license.CanPurchase && !license.IsPro && state.Data.TrialEndShownUtc is null &&
        app.TrialEnds is { } end && time.GetUtcNow() >= end;

    /// <summary>Called whenever the main window is activated.</summary>
    public async Task OnWindowActivatedAsync()
    {
        if (!JustEnded)
            return;
        state.Data.TrialEndShownUtc = time.GetUtcNow();
        state.Save();
        await upgrade.ShowTrialEndedAsync(state.Data.TrialAppSwitches, state.Data.TrialScheduleSwitches);
    }
}
