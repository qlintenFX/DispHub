// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO;
using System.Windows.Threading;
using DispHub.Core.Color;
using DispHub.Core.Config;
using DispHub.Core.Hotkeys;
using DispHub.Core.Licensing;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Platform.Shell;
using DispHub.Platform.Store;
using Microsoft.Extensions.Logging;

namespace DispHub.Services;

/// <summary>Why the applied profile changed, so the UI can decide whether to show the on-screen display.</summary>
public enum ChangeSource
{
    Startup,
    Editor,
    Hotkey,
    Tray,
    Automation,
}

/// <summary>
/// Owns the user's config and the runtime state (paused, manual picks, foreground app), decides what each monitor
/// should show via <see cref="RuleEngine"/>, and hands that to the colour thread. UI thread only.
/// </summary>
public sealed class AppController : IDisposable
{
    public const string PauseAction = "pause";
    public const string NextAction = "next";
    public const string PreviousAction = "previous";
    public const string QuickPanelAction = "panel";
    private const string ProfileActionPrefix = "profile:";
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly ConfigStore _store;
    private readonly IColorApplier _colors;
    private readonly ILicenseService _license;
    private readonly IForegroundWatcher _foreground;
    private readonly IMonitorBrightness? _brightness;
    private readonly IMonitorModes? _modes;
    private readonly TimeProvider _time;
    private readonly ILogger<AppController> _logger;
    private readonly ITimer _saveTimer;
    private readonly ITimer _ruleTimer;
    private readonly ITimer _trialTimer;
    private readonly Dispatcher _dispatcher;
    private DateTimeOffset? _trialEnds;
    private HashSet<string> _probedMonitors = new(StringComparer.OrdinalIgnoreCase);
    private IHotkeyRegistrar? _hotkeys;
    private ManualOverride? _manual;
    private bool _onBattery;
    private bool _savePending;
    private bool _disposed;

    public AppController(
        ConfigStore store,
        IColorApplier colors,
        ILicenseService license,
        IForegroundWatcher foreground,
        ILogger<AppController> logger,
        TimeProvider? time = null,
        IMonitorBrightness? brightness = null,
        IMonitorModes? modes = null)
    {
        _store = store;
        _colors = colors;
        _license = license;
        _foreground = foreground;
        _brightness = brightness;
        _modes = modes;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _saveTimer = _time.CreateTimer(_ => _dispatcher.BeginInvoke(FlushSave), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _ruleTimer = _time.CreateTimer(_ => _dispatcher.BeginInvoke(() => Evaluate(ChangeSource.Automation)), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _trialTimer = _time.CreateTimer(_ => _dispatcher.BeginInvoke(OnLicenceChanged), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        ConfigLoadResult loaded = store.Load();
        Config = loaded.Config;
        LoadResult = loaded;
        foreach (string fix in loaded.Fixes)
            _logger.LogWarning("Config fix: {Fix}", fix);

        _colors.StatusChanged += (_, _) => _dispatcher.BeginInvoke(OnMonitorsChanged);
        _colors.ExternalResetDetected += (_, _) => _dispatcher.BeginInvoke(() => ExternalResetDetected?.Invoke(this, EventArgs.Empty));
        if (_brightness is not null)
        {
            _brightness.Changed += (_, _) => _dispatcher.BeginInvoke(() => MonitorLevelsChanged?.Invoke(this, EventArgs.Empty));
            _brightness.OriginalsChanged += (_, originals) => _dispatcher.BeginInvoke(() => KeepOriginalLevels(originals));
        }
        if (_modes is not null)
        {
            // Raised after a switch too: the colour thread looks at the displays again, as HDR pauses gamma changes.
            _modes.Changed += (_, _) => _dispatcher.BeginInvoke(() =>
            {
                _colors.RefreshDisplays();
                ModeAbilitiesChanged?.Invoke(this, EventArgs.Empty);
            });
            _modes.OriginalsChanged += (_, originals) => _dispatcher.BeginInvoke(() => KeepOriginalModes(originals));
        }
        _foreground.Changed += (_, _) => _dispatcher.BeginInvoke(OnForegroundChanged);
        _license.Changed += (_, _) => _dispatcher.BeginInvoke(OnLicenceChanged);
    }

    public AppConfig Config { get; }

    public ConfigLoadResult LoadResult { get; }

    public FeatureGate Features => new(_license.IsPro, InTrial);

    /// <summary>When the Pro trial ends; null when there is none (Pro bought, or a build that isn't from the Store).</summary>
    public DateTimeOffset? TrialEnds => _license.CanPurchase && !_license.IsPro ? _trialEnds : null;

    /// <summary>The Pro trial is running: every Pro feature is on until <see cref="TrialEnds"/>.</summary>
    public bool InTrial => TrialEnds is { } end && _time.GetUtcNow() < end;

    /// <summary>The trial ended, or Pro was bought or lost: what's allowed changed (on the UI thread).</summary>
    public event EventHandler? LicenceChanged;

    /// <summary>Starts or clears the Pro trial. Everything is checked again the moment it ends.</summary>
    public void SetTrial(DateTimeOffset? endsUtc)
    {
        bool before = InTrial;
        _trialEnds = endsUtc;
        TimeSpan left = endsUtc is { } end ? end - _time.GetUtcNow() : Timeout.InfiniteTimeSpan;
        _trialTimer.Change(left > TimeSpan.Zero ? left : Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        if (_hotkeys is not null && before != InTrial)
            OnLicenceChanged();
    }

    /// <summary>Whether a profile can go on screen now; profiles past the free ones wait for Pro.</summary>
    public bool CanUseProfile(Guid profileId) => Features.CanUseProfile(Config.Profiles.FindIndex(p => p.Id == profileId));

    private void OnLicenceChanged()
    {
        ConfigChanged?.Invoke(this, EventArgs.Empty);
        SyncHotkeys();
        Evaluate(ChangeSource.Automation);
        LicenceChanged?.Invoke(this, EventArgs.Empty);
    }

    public RuleOutput? Current { get; private set; }

    public IReadOnlyList<MonitorStatus> Monitors => _colors.Monitors;

    /// <summary>Full path of the program in front, if known.</summary>
    public string? ForegroundExecutable => _foreground.Current;

    /// <summary>The program in front changed (on the UI thread).</summary>
    public event EventHandler? ForegroundChanged;

    /// <summary>DispHub, or its profiles, went on or off (<see cref="AppConfig.IsOff"/> or <see cref="AppConfig.IsPaused"/>).</summary>
    public event EventHandler? PowerChanged;

    /// <summary>Profiles are on screen: DispHub is on and its profiles are on.</summary>
    public bool ProfilesOn => !Config.IsOff && !Config.IsPaused;

    /// <summary>Hotkeys that couldn't be registered, keyed by action ID.</summary>
    public IReadOnlyDictionary<string, string> HotkeyFailures { get; private set; } = new Dictionary<string, string>();

    /// <summary>Profiles, rules or settings changed.</summary>
    public event EventHandler? ConfigChanged;

    /// <summary>The applied profile or paused state changed.</summary>
    public event EventHandler<ChangeSource>? StateChanged;

    public event EventHandler? MonitorsChanged;

    /// <summary>External monitors' own brightness changed or became known (on the UI thread).</summary>
    public event EventHandler? MonitorLevelsChanged;

    /// <summary>External monitors whose own brightness can be set (DDC/CI), by monitor key, in percent.</summary>
    public IReadOnlyDictionary<string, int> MonitorLevels => _brightness?.Levels ?? new Dictionary<string, int>();

    /// <summary>Sets a monitor's own brightness now, as its buttons would.</summary>
    public void SetMonitorLevel(string monitorKey, int percent) => _brightness?.SetLevel(monitorKey, percent);

    /// <summary>Reads screens' brightness again, as a monitor's own buttons may have changed it.</summary>
    public void RereadMonitorLevels() => _brightness?.Reread();

    /// <summary>Displays that can switch refresh rate or HDR became known (on the UI thread).</summary>
    public event EventHandler? ModeAbilitiesChanged;

    /// <summary>Displays with more than one refresh rate, or with HDR, by monitor key.</summary>
    public IReadOnlyDictionary<string, ModeAbilities> ModeAbilities => _modes?.Abilities ?? new Dictionary<string, ModeAbilities>();

    /// <summary>A profile's colours changed in the editor.</summary>
    public event EventHandler<Guid>? ProfileEdited;

    public event EventHandler? ExternalResetDetected;

    /// <summary>The quick panel hotkey was pressed.</summary>
    public event EventHandler? QuickPanelRequested;

    public void Start(IHotkeyRegistrar hotkeys)
    {
        _hotkeys = hotkeys;
        _hotkeys.Pressed += (_, action) => OnHotkey(action);
        _colors.Start();
        _brightness?.Start(Config.Monitors
            .Where(m => m.Value.BrightnessBeforeProfile is not null)
            .ToDictionary(m => m.Key, m => m.Value.BrightnessBeforeProfile!.Value, StringComparer.OrdinalIgnoreCase));
        _modes?.Start(Config.Monitors
            .Where(m => m.Value.RefreshBeforeProfile is not null || m.Value.HdrBeforeProfile is not null)
            .ToDictionary(m => m.Key, m => new SavedMode(m.Value.RefreshBeforeProfile, m.Value.HdrBeforeProfile), StringComparer.OrdinalIgnoreCase));
        _foreground.Start();
        SyncHotkeys();
        Evaluate(ChangeSource.Startup);
    }

    public Profile? FindProfile(Guid id) => RuleEngine.FindProfile(Config, id);

    /// <summary>Switches to a profile by hand. While a rule is active this lasts until that rule ends.</summary>
    public void ActivateProfile(Guid profileId, ChangeSource source)
    {
        if (FindProfile(profileId) is null || !CanUseProfile(profileId))
            return;

        // Picking a profile means wanting to see it: DispHub and its profiles come on.
        bool powerChanged = SetPowerState(off: false, paused: false, source);

        // The rule in force now, with DispHub on: while it was off, every rule looked like the everyday profile, and
        // a pick saved there would lose to a schedule or app rule straight away.
        string context = Resolve().ContextKey;
        if (context == RuleEngine.BaseContext)
        {
            Config.ActiveProfileId = profileId;
            _manual = null;
            ScheduleSave();
        }
        else
        {
            _manual = new ManualOverride(profileId, context);
        }
        Evaluate(source);
        if (powerChanged)
            PowerChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>DispHub on or off (the power button): off is original colours and no automation.</summary>
    public void SetOn(bool on, ChangeSource source) => SetPower(off: !on, paused: Config.IsPaused, source);

    /// <summary>The power button, the on/off hotkey and the tray menu.</summary>
    public void ToggleOff(ChangeSource source) => SetOn(Config.IsOff, source);

    /// <summary>
    /// Profiles on or off (the Profiles switch). Turning them on turns DispHub on too, so the switch always does
    /// what it shows.
    /// </summary>
    public void SetProfilesOn(bool on, ChangeSource source) => SetPower(off: !on && Config.IsOff, paused: !on, source);

    private void SetPower(bool off, bool paused, ChangeSource source)
    {
        if (!SetPowerState(off, paused, source))
            return;
        Evaluate(source);
        PowerChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Changes the on/off state and saves it; returns whether anything changed.</summary>
    private bool SetPowerState(bool off, bool paused, ChangeSource source)
    {
        if (off == Config.IsOff && paused == Config.IsPaused)
            return false;
        if (off != Config.IsOff)
            _logger.LogInformation("Turned {State} from the {Source}", off ? "off" : "on", source);
        (Config.IsOff, Config.IsPaused) = (off, paused);
        ScheduleSave();
        return true;
    }

    public void CycleProfile(int direction, ChangeSource source)
    {
        // Only the profiles that can go on screen take part.
        int count = Math.Min(Config.Profiles.Count, Features.MaxProfiles);
        if (count == 0)
            return;
        int index = Config.Profiles.FindIndex(p => p.Id == Current?.ProfileId);
        int next = ((index < 0 || index >= count ? 0 : index + direction) % count + count) % count;
        ActivateProfile(Config.Profiles[next].Id, source);
    }

    /// <summary>Live edit from the profile editor. The edited profile becomes the one on screen.</summary>
    public void UpdateProfileColor(Guid profileId, ColorSettings color)
    {
        if (FindProfile(profileId) is not { } profile)
            return;
        profile.Color = color.Clamp();
        ScheduleSave();
        ProfileEdited?.Invoke(this, profileId);
        if (Current?.ProfileId != profileId || Config.IsPaused)
            ActivateProfile(profileId, ChangeSource.Editor);
        else
            Evaluate(ChangeSource.Editor);
    }

    /// <summary>Live edit of one display's settings within a profile (Pro).</summary>
    public void UpdateProfileMonitorColor(Guid profileId, string monitorKey, ColorSettings color)
    {
        if (!Features.PerMonitorProfiles || FindProfile(profileId) is not { } profile)
            return;
        profile.MonitorOverrides[monitorKey] = color.Clamp();
        ScheduleSave();
        ProfileEdited?.Invoke(this, profileId);
        if (Current?.ProfileId != profileId || Config.IsPaused)
            ActivateProfile(profileId, ChangeSource.Editor);
        else
            Evaluate(ChangeSource.Editor);
    }

    /// <summary>The own brightness external monitors get while the profile is on (Pro); null leaves it alone.</summary>
    public void SetProfileMonitorBrightness(Guid profileId, int? percent)
    {
        if (FindProfile(profileId) is not { } profile || (percent is not null && !Features.MonitorBrightnessInProfiles))
            return;
        profile.MonitorBrightness = percent is { } p ? Math.Clamp(p, 0, 100) : null;
        ScheduleSave();
        ProfileEdited?.Invoke(this, profileId);
        if (Current?.ProfileId != profileId || Config.IsPaused)
            ActivateProfile(profileId, ChangeSource.Editor);
        else
            Evaluate(ChangeSource.Editor);
    }

    /// <summary>The refresh rate and HDR displays switch to while the profile is on (Pro).</summary>
    public void SetProfileDisplayMode(Guid profileId, RefreshChoice refresh, bool? hdr)
    {
        bool changes = refresh != RefreshChoice.Unchanged || hdr is not null;
        if (FindProfile(profileId) is not { } profile || (changes && !Features.DisplayModes))
            return;
        profile.RefreshRate = refresh;
        profile.Hdr = hdr;
        ScheduleSave();
        ProfileEdited?.Invoke(this, profileId);
        if (Current?.ProfileId != profileId || Config.IsPaused)
            ActivateProfile(profileId, ChangeSource.Editor);
        else
            Evaluate(ChangeSource.Editor);
    }

    /// <summary>Whether a display drops to a lower refresh rate on battery (Pro).</summary>
    public void SetLowRefreshOnBattery(string monitorKey, bool on)
    {
        if (on && !Features.DisplayModes)
            return;
        if (!Config.Monitors.TryGetValue(monitorKey, out MonitorConfig? monitor))
            Config.Monitors[monitorKey] = monitor = new MonitorConfig();
        monitor.LowRefreshOnBattery = on;
        ScheduleSave();
        Evaluate(ChangeSource.Editor);
    }

    /// <summary>Makes a display use the profile's main settings again.</summary>
    public void ClearMonitorOverride(Guid profileId, string monitorKey)
    {
        if (FindProfile(profileId) is not { } profile || !profile.MonitorOverrides.Remove(monitorKey))
            return;
        ScheduleSave();
        Evaluate(ChangeSource.Editor);
    }

    public void RenameProfile(Guid profileId, string name)
    {
        if (FindProfile(profileId) is not { } profile)
            return;
        string trimmed = name.Trim();
        profile.Name = trimmed.Length == 0 ? profile.Name : trimmed[..Math.Min(trimmed.Length, ConfigNormalizer.MaxNameLength)];
        ScheduleSave();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
        StateChanged?.Invoke(this, ChangeSource.Editor);
    }

    public Profile? AddProfile(ColorSettings? color = null, string? name = null)
    {
        if (!Features.CanAddProfile(Config.Profiles.Count))
            return null;
        var profile = new Profile { Name = UniqueName(name ?? Loc.Get("Profiles_NewName")), Color = color ?? ColorSettings.Neutral };
        Config.Profiles.Add(profile);
        ScheduleSave();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
        return profile;
    }

    /// <summary>A copy with everything but the name and hotkey, which must stay unique.</summary>
    public Profile? DuplicateProfile(Guid profileId)
    {
        if (FindProfile(profileId) is not { } source || AddProfile(source.Color, Loc.Format("Profiles_CopyName", source.Name)) is not { } copy)
            return null;
        copy.MonitorBrightness = source.MonitorBrightness;
        copy.RefreshRate = source.RefreshRate;
        copy.Hdr = source.Hdr;
        foreach ((string key, ColorSettings color) in source.MonitorOverrides)
            copy.MonitorOverrides[key] = color;
        return copy;
    }

    public bool DeleteProfile(Guid profileId)
    {
        if (Config.Profiles.Count <= 1 || FindProfile(profileId) is not { } profile)
            return false;

        Config.Profiles.Remove(profile);
        Config.AppRules.RemoveAll(r => r.ProfileId == profileId);
        Config.Schedule.Slots.RemoveAll(s => s.ProfileId == profileId);
        if (Config.ActiveProfileId == profileId)
            Config.ActiveProfileId = Config.Profiles[0].Id;
        if (_manual?.ProfileId == profileId)
            _manual = null;

        ScheduleSave();
        SyncHotkeys();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
        Evaluate(ChangeSource.Editor);
        return true;
    }

    public void MoveProfile(Guid profileId, int offset)
    {
        int index = Config.Profiles.FindIndex(p => p.Id == profileId);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= Config.Profiles.Count)
            return;
        (Config.Profiles[index], Config.Profiles[target]) = (Config.Profiles[target], Config.Profiles[index]);
        ScheduleSave();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets a profile's hotkey. Returns an error message, or null on success.</summary>
    public string? SetProfileHotkey(Guid profileId, HotkeyGesture? gesture)
    {
        if (FindProfile(profileId) is not { } profile)
            return null;
        profile.Hotkey = gesture?.ToString();
        return CommitHotkeyChange(ProfileActionPrefix + profileId);
    }

    /// <summary>Sets one of the global action hotkeys. Returns an error message, or null on success.</summary>
    public string? SetActionHotkey(string action, HotkeyGesture? gesture)
    {
        string? text = gesture?.ToString();
        switch (action)
        {
            case PauseAction:
                Config.Hotkeys.TogglePause = text;
                break;
            case NextAction:
                Config.Hotkeys.NextProfile = text;
                break;
            case PreviousAction:
                Config.Hotkeys.PreviousProfile = text;
                break;
            case QuickPanelAction:
                Config.Hotkeys.QuickPanel = text;
                break;
            default:
                return null;
        }
        return CommitHotkeyChange(action);
    }

    public string? HotkeyFailure(Guid profileId) => HotkeyFailures.GetValueOrDefault(ProfileActionPrefix + profileId);

    public void SuspendHotkeys() => _hotkeys?.SuspendAll();

    public void ResumeHotkeys()
    {
        _hotkeys?.ResumeAll();
        SyncHotkeys();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Keeps where the main window is, so it opens there next time.</summary>
    public void RememberWindow(WindowBounds window)
    {
        if (Config.Ui.Window == window)
            return;
        Config.Ui.Window = window;
        ScheduleSave();
    }

    /// <summary>For settings changed directly on <see cref="Config"/> by a view model.</summary>
    public void SettingsChanged()
    {
        ScheduleSave();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
        Evaluate(ChangeSource.Editor);
    }

    public void OnDisplayChanged() => _colors.RefreshDisplays();

    internal void OnForegroundChanged()
    {
        LearnRulePath();
        // Only re-evaluate when it can matter, so switching windows costs nothing without app rules.
        if (Config.AppRules.Count > 0 || _manual is not null)
            Evaluate(ChangeSource.Automation);
        ForegroundChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A rule that knows only its program's name, or a folder the program has moved from, takes the program's real
    /// path the first time it's in front, so the Apps page shows its icon and name from then on.
    /// </summary>
    private void LearnRulePath()
    {
        if (ForegroundExecutable is not { } path || !Path.IsPathRooted(path))
            return;
        AppRule? rule = Config.AppRules.Find(r => RuleEngine.Matches(r, path));
        if (rule is null || string.Equals(rule.Path, path, StringComparison.OrdinalIgnoreCase) ||
            (rule.Path is { } known && Path.IsPathRooted(known) && File.Exists(known)))
        {
            return;
        }
        rule.Path = path;
        ScheduleSave();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMonitorsChanged()
    {
        // The colour thread finds monitors after startup and after display changes; rules may differ per monitor.
        var keys = _colors.Monitors.Select(m => m.Monitor.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!keys.SetEquals(_probedMonitors))
        {
            _probedMonitors = keys;
            var monitors = _colors.Monitors.Select(m => m.Monitor).ToList();
            _brightness?.Refresh(monitors);
            _modes?.Refresh(monitors);
        }
        if (Current is null || !keys.SetEquals(Current.Monitors.Keys))
            Evaluate(ChangeSource.Automation);
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OnResumed() => _colors.Reapply();

    /// <summary>Restores original colours and monitor brightness, and saves. Call before exiting.</summary>
    public void Shutdown()
    {
        if (_brightness is not null && !_brightness.RestoreAll(TimeSpan.FromSeconds(3)))
            _logger.LogWarning("Not every monitor's brightness could be put back on exit");
        if (_modes is not null && !_modes.RestoreAll(TimeSpan.FromSeconds(5)))
            _logger.LogWarning("Not every display's refresh rate or HDR could be put back on exit");
        // The workers report what is left to put back through the dispatcher: let that land before the last save,
        // or the next start would put back levels and modes that are already back.
        _dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        FlushSave();
        if (!_colors.RestoreAll(TimeSpan.FromSeconds(3)))
            _logger.LogWarning("Not every monitor could be restored on exit");
    }

    /// <summary>The worker found or put back levels from before a profile: keep them, so a crash can be undone.</summary>
    private void KeepOriginalLevels(IReadOnlyDictionary<string, int> originals)
    {
        foreach ((string key, MonitorConfig monitor) in Config.Monitors)
            monitor.BrightnessBeforeProfile = originals.TryGetValue(key, out int level) ? level : null;
        foreach ((string key, int level) in originals)
        {
            if (!Config.Monitors.ContainsKey(key))
                Config.Monitors[key] = new MonitorConfig { BrightnessBeforeProfile = level };
        }
        ScheduleSave();
    }

    /// <summary>The worker changed or put back display modes: keep what to put back, so a crash can be undone.</summary>
    private void KeepOriginalModes(IReadOnlyDictionary<string, SavedMode> originals)
    {
        foreach ((string key, MonitorConfig monitor) in Config.Monitors)
        {
            SavedMode saved = originals.GetValueOrDefault(key);
            (monitor.RefreshBeforeProfile, monitor.HdrBeforeProfile) = (saved.RefreshHz, saved.Hdr);
        }
        foreach ((string key, SavedMode saved) in originals)
        {
            if (!Config.Monitors.ContainsKey(key))
                Config.Monitors[key] = new MonitorConfig { RefreshBeforeProfile = saved.RefreshHz, HdrBeforeProfile = saved.Hdr };
        }
        ScheduleSave();
    }

    /// <summary>The PC switched between battery and the charger.</summary>
    public void OnPowerSourceChanged(bool onBattery)
    {
        if (_onBattery == onBattery)
            return;
        _onBattery = onBattery;
        if (Current is not null && Config.Monitors.Values.Any(m => m.LowRefreshOnBattery))
            Evaluate(ChangeSource.Automation);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _saveTimer.Dispose();
        _ruleTimer.Dispose();
        _trialTimer.Dispose();
        _foreground.Dispose();
        _brightness?.Dispose();
        _modes?.Dispose();
        _colors.Dispose();
    }

    private string? CommitHotkeyChange(string action)
    {
        ScheduleSave();
        SyncHotkeys();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
        return HotkeyFailures.GetValueOrDefault(action);
    }

    /// <summary>What the rules say right now, without applying it.</summary>
    private RuleOutput Resolve()
    {
        var monitorKeys = _colors.Monitors.Select(m => m.Monitor.Key).ToList();
        return RuleEngine.Resolve(new RuleInput(Config, monitorKeys, _time.GetLocalNow().DateTime, _foreground.Current, _manual, Features.HasPro, _time.LocalTimeZone));
    }

    private void Evaluate(ChangeSource source)
    {
        RuleOutput output = Resolve();

        // A pick by hand lasts as long as what it was made in: an app's pick until that app leaves the front, a pick
        // under the schedule until the schedule's next change, like Windows' Night light. A visit to an app with
        // its own rule doesn't end a schedule pick: back from the app, the pick comes back. Being off isn't a
        // context either: turned back on, the pick carries on.
        if (_manual is not null && output.Reason != TargetReason.Paused && _manual.ContextKey != output.ContextKey &&
            (RuleEngine.IsAppContext(_manual.ContextKey) || !RuleEngine.IsAppContext(output.ContextKey)))
            _manual = null;

        // Monitors can appear later (the colour thread enumerates them); send a wildcard for the default.
        var targets = new Dictionary<string, ColorSettings?>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, MonitorTarget target) in output.Monitors)
            targets[key] = target.Settings;
        _colors.Apply(new MonitorTargets(targets, DefaultTarget(output)));
        _brightness?.Apply(MonitorLevelTargets(output));
        _modes?.Apply(ModeTargets(output), byHand: source != ChangeSource.Automation);

        bool profileChanged = Current?.ProfileId != output.ProfileId || Current?.Reason != output.Reason;
        Current = output;
        _ruleTimer.Change(DelayUntil(output.NextEvaluation), Timeout.InfiniteTimeSpan);

        // Live slider edits don't change which profile is on; they're reported through ProfileEdited instead,
        // so dragging a slider doesn't rebuild the tray menu and every page.
        if (profileChanged || source is ChangeSource.Startup or ChangeSource.Hotkey or ChangeSource.Tray)
            StateChanged?.Invoke(this, source);
    }

    /// <summary>The own brightness each monitor's profile asks for (Pro), or null to leave or put back the usual level.</summary>
    private Dictionary<string, int?> MonitorLevelTargets(RuleOutput output)
    {
        var levels = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        bool allowed = Features.MonitorBrightnessInProfiles;
        foreach ((string key, MonitorTarget target) in output.Monitors)
        {
            levels[key] = allowed && target.Settings is not null && target.ProfileId is { } id
                ? FindProfile(id)?.MonitorBrightness
                : null;
        }
        return levels;
    }

    /// <summary>
    /// The refresh rate and HDR each display's profile asks for (Pro). Without one, a laptop screen can drop to a
    /// lower refresh rate on battery.
    /// </summary>
    private Dictionary<string, ModeTarget> ModeTargets(RuleOutput output)
    {
        var modes = new Dictionary<string, ModeTarget>(StringComparer.OrdinalIgnoreCase);
        if (!Features.DisplayModes || Config.IsOff)
            return modes;
        foreach ((string key, MonitorTarget target) in output.Monitors)
        {
            if (target.Reason == TargetReason.Excluded)
                continue;
            Profile? profile = target.Settings is not null && target.ProfileId is { } id ? FindProfile(id) : null;
            RefreshChoice refresh = profile?.RefreshRate ?? RefreshChoice.Unchanged;
            if (refresh == RefreshChoice.Unchanged && _onBattery && Config.Monitors.GetValueOrDefault(key)?.LowRefreshOnBattery == true)
                refresh = RefreshChoice.Lowest;
            modes[key] = new ModeTarget(refresh, profile?.Hdr);
        }
        return modes;
    }

    private ColorSettings? DefaultTarget(RuleOutput output)
    {
        if (Config.IsPaused || Config.IsOff || output.ProfileId is not { } id || FindProfile(id) is not { } profile)
            return null;
        return Features.Allowed(profile.Color);
    }

    private TimeSpan DelayUntil(DateTime? next)
    {
        if (next is null)
            return Timeout.InfiniteTimeSpan;
        TimeSpan delay = next.Value - _time.GetLocalNow().DateTime;
        return delay < TimeSpan.FromMilliseconds(100) ? TimeSpan.FromMilliseconds(100) : delay;
    }

    private void OnHotkey(string action)
    {
        switch (action)
        {
            case PauseAction:
                ToggleOff(ChangeSource.Hotkey);
                break;
            case NextAction:
                CycleProfile(1, ChangeSource.Hotkey);
                break;
            case PreviousAction:
                CycleProfile(-1, ChangeSource.Hotkey);
                break;
            case QuickPanelAction:
                QuickPanelRequested?.Invoke(this, EventArgs.Empty);
                break;
            default:
                if (action.StartsWith(ProfileActionPrefix, StringComparison.Ordinal) &&
                    Guid.TryParse(action.AsSpan(ProfileActionPrefix.Length), out Guid id))
                {
                    ActivateProfile(id, ChangeSource.Hotkey);
                }
                break;
        }
    }

    private void SyncHotkeys()
    {
        if (_hotkeys is null)
            return;

        // Profiles waiting for Pro keep their shortcuts but don't take the keys.
        var desired = new Dictionary<string, HotkeyGesture>(StringComparer.Ordinal);
        foreach (Profile profile in Config.Profiles.Take(Features.MaxProfiles))
        {
            if (HotkeyGesture.TryParse(profile.Hotkey, out HotkeyGesture gesture))
                desired[ProfileActionPrefix + profile.Id] = gesture;
        }
        AddAction(desired, PauseAction, Config.Hotkeys.TogglePause);
        AddAction(desired, NextAction, Config.Hotkeys.NextProfile);
        AddAction(desired, PreviousAction, Config.Hotkeys.PreviousProfile);
        AddAction(desired, QuickPanelAction, Config.Hotkeys.QuickPanel);

        HotkeyFailures = _hotkeys.Sync(desired);
        foreach ((string action, string reason) in HotkeyFailures)
            _logger.LogWarning("Hotkey for {Action} not registered: {Reason}", action, reason);

        static void AddAction(Dictionary<string, HotkeyGesture> map, string action, string? text)
        {
            if (HotkeyGesture.TryParse(text, out HotkeyGesture gesture))
                map[action] = gesture;
        }
    }

    private string UniqueName(string name)
    {
        string candidate = name;
        for (int n = 2; Config.Profiles.Exists(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)); n++)
            candidate = $"{name} {n}";
        return candidate;
    }

    private void ScheduleSave()
    {
        _savePending = true;
        _saveTimer.Change(SaveDelay, Timeout.InfiniteTimeSpan);
    }

    private void FlushSave()
    {
        if (!_savePending)
            return;
        _savePending = false;
        string json = ConfigStore.Serialize(Config);
        try
        {
            _store.SaveSerialized(json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Saving settings failed");
        }
    }
}
