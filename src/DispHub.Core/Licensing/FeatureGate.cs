// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;

namespace DispHub.Core.Licensing;

/// <summary>
/// What the free tier includes. Builds from source and sideloaded builds are always Pro; only the Microsoft
/// Store build checks the add-on, and gives every new install <see cref="TrialLength"/> of Pro first (a reverse
/// trial: people get used to the whole app, then choose what to keep). All limits live here so pricing decisions
/// stay in one place.
/// </summary>
public sealed record FeatureGate(bool IsPro, bool InTrial = false)
{
    public const int FreeProfileLimit = 3;
    public const int FreeAppRuleLimit = 1;

    /// <summary>The most profiles anyone keeps. Pro is "unlimited"; this only stops runaway lists.</summary>
    public const int ProfileCeiling = 50;

    public static readonly TimeSpan TrialLength = TimeSpan.FromDays(7);

    /// <summary>Pro features are on: bought, or still within the trial.</summary>
    public bool HasPro => IsPro || InTrial;

    public int MaxProfiles => HasPro ? ProfileCeiling : FreeProfileLimit;
    public int MaxAppRules => HasPro ? int.MaxValue : FreeAppRuleLimit;
    public bool Schedules => HasPro;
    public bool PerMonitorProfiles => HasPro;

    /// <summary>Choosing how see-through the quick panel and on-screen messages are.</summary>
    public bool PanelOpacity => HasPro;

    /// <summary>Profiles that set external monitors' own brightness. Setting it by hand is free.</summary>
    public bool MonitorBrightnessInProfiles => HasPro;

    /// <summary>Profiles that switch refresh rate or HDR, and the lower refresh rate on battery.</summary>
    public bool DisplayModes => HasPro;

    /// <summary>Lifting dark tones (<see cref="ColorSettings.ShadowBoost"/>).</summary>
    public bool ShadowBoost => HasPro;

    /// <summary>The settings as they can go on screen: without Pro, shadow boost stays off.</summary>
    public ColorSettings Allowed(ColorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return ShadowBoost || settings.ShadowBoost == 0 ? settings : settings with { ShadowBoost = 0 };
    }

    public bool CanAddProfile(int currentCount) => currentCount < MaxProfiles;

    public bool CanAddAppRule(int currentCount) => currentCount < MaxAppRules;

    /// <summary>
    /// Whether the profile at <paramref name="index"/> can be switched to. Profiles made during the trial stay, but
    /// those past the free ones wait for Pro.
    /// </summary>
    public bool CanUseProfile(int index) => index >= 0 && index < MaxProfiles;

    /// <summary>Whether the app rule at <paramref name="index"/> works: on the free tier only the first one does.</summary>
    public bool CanUseAppRule(int index) => index >= 0 && index < MaxAppRules;

    /// <summary>When the trial of an install first started on <paramref name="firstRunUtc"/> ends.</summary>
    public static DateTimeOffset TrialEnds(DateTimeOffset firstRunUtc) => firstRunUtc + TrialLength;

    /// <summary>Whole days left in a trial ending at <paramref name="endsUtc"/>, counting a part day as one.</summary>
    public static int TrialDaysLeft(DateTimeOffset endsUtc, DateTimeOffset nowUtc) =>
        endsUtc <= nowUtc ? 0 : (int)Math.Ceiling((endsUtc - nowUtc).TotalDays);
}
