// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Licensing;
using DispHub.Core.Rules;
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

    /// <summary>Called whenever the main window is activated.</summary>
    public async Task OnWindowActivatedAsync()
    {
        if (!license.CanPurchase || license.IsPro || state.Data.TrialEndShownUtc is not null ||
            app.TrialEnds is not { } end || time.GetUtcNow() < end)
        {
            return;
        }
        state.Data.TrialEndShownUtc = time.GetUtcNow();
        state.Save();
        await upgrade.ShowTrialEndedAsync(state.Data.TrialAppSwitches, state.Data.TrialScheduleSwitches);
    }
}
