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
    public const string EffectsAction = "effects";
    public const string QuickPanelAction = "panel";
    private const string ProfileActionPrefix = "profile:";
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private readonly ConfigStore _store;
    private readonly IColorApplier _colors;
    private readonly ILicenseService _license;
    private readonly IForegroundWatcher _foreground;
    private readonly TimeProvider _time;
    private readonly ILogger<AppController> _logger;
    private readonly ITimer _saveTimer;
    private readonly ITimer _ruleTimer;
    private readonly Dispatcher _dispatcher;
    private IHotkeyRegistrar? _hotkeys;
    private ManualOverride? _manual;
    private bool _savePending;
    private bool _disposed;

    public AppController(
        ConfigStore store,
        IColorApplier colors,
        ILicenseService license,
        IForegroundWatcher foreground,
        ILogger<AppController> logger,
        TimeProvider? time = null)
    {
        _store = store;
        _colors = colors;
        _license = license;
        _foreground = foreground;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _saveTimer = _time.CreateTimer(_ => _dispatcher.BeginInvoke(FlushSave), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _ruleTimer = _time.CreateTimer(_ => _dispatcher.BeginInvoke(() => Evaluate(ChangeSource.Automation)), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        ConfigLoadResult loaded = store.Load();
        Config = loaded.Config;
        LoadResult = loaded;
        foreach (string fix in loaded.Fixes)
            _logger.LogWarning("Config fix: {Fix}", fix);

        _colors.StatusChanged += (_, _) => _dispatcher.BeginInvoke(OnMonitorsChanged);
        _colors.ExternalResetDetected += (_, _) => _dispatcher.BeginInvoke(() => ExternalResetDetected?.Invoke(this, EventArgs.Empty));
        _foreground.Changed += (_, _) => _dispatcher.BeginInvoke(OnForegroundChanged);
        _license.Changed += (_, _) => _dispatcher.BeginInvoke(() =>
        {
            ConfigChanged?.Invoke(this, EventArgs.Empty);
            SyncHotkeys();
            Evaluate(ChangeSource.Automation);
        });
    }

    public AppConfig Config { get; }

    public ConfigLoadResult LoadResult { get; }

    public FeatureGate Features => new(_license.IsPro);

    public RuleOutput? Current { get; private set; }

    public IReadOnlyList<MonitorStatus> Monitors => _colors.Monitors;

    /// <summary>Full path of the program in front, if known.</summary>
    public string? ForegroundExecutable => _foreground.Current;

    /// <summary>The program in front changed (on the UI thread).</summary>
    public event EventHandler? ForegroundChanged;

    /// <summary>Hotkeys that couldn't be registered, keyed by action ID.</summary>
    public IReadOnlyDictionary<string, string> HotkeyFailures { get; private set; } = new Dictionary<string, string>();

    /// <summary>Profiles, rules or settings changed.</summary>
    public event EventHandler? ConfigChanged;

    /// <summary>The applied profile or paused state changed.</summary>
    public event EventHandler<ChangeSource>? StateChanged;

    public event EventHandler? MonitorsChanged;

    /// <summary>A profile's colours changed in the editor.</summary>
    public event EventHandler<Guid>? ProfileEdited;

    public event EventHandler? ExternalResetDetected;

    /// <summary>The effects hotkey was pressed.</summary>
    public event EventHandler? EffectsToggleRequested;

    /// <summary>The quick panel hotkey was pressed.</summary>
    public event EventHandler? QuickPanelRequested;

    public void Start(IHotkeyRegistrar hotkeys)
    {
        _hotkeys = hotkeys;
        _hotkeys.Pressed += (_, action) => OnHotkey(action);
        _colors.Start();
        _foreground.Start();
        SyncHotkeys();
        Evaluate(ChangeSource.Startup);
    }

    public Profile? FindProfile(Guid id) => RuleEngine.FindProfile(Config, id);

    /// <summary>Switches to a profile by hand. While a rule is active this lasts until that rule ends.</summary>
    public void ActivateProfile(Guid profileId, ChangeSource source)
    {
        if (FindProfile(profileId) is null)
            return;

        if (Config.IsPaused)
            Config.IsPaused = false;

        string context = Current?.ContextKey ?? RuleEngine.BaseContext;
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
    }

    public void TogglePause(ChangeSource source)
    {
        Config.IsPaused = !Config.IsPaused;
        ScheduleSave();
        Evaluate(source);
    }

    public void CycleProfile(int direction, ChangeSource source)
    {
        if (Config.Profiles.Count == 0)
            return;
        int index = Config.Profiles.FindIndex(p => p.Id == Current?.ProfileId);
        int next = ((index < 0 ? 0 : index + direction) % Config.Profiles.Count + Config.Profiles.Count) % Config.Profiles.Count;
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

    /// <summary>Makes a display use the profile's main settings again.</summary>
    public void ClearMonitorOverride(Guid profileId, string monitorKey)
    {
        if (FindProfile(profileId) is not { } profile || !profile.MonitorOverrides.Remove(monitorKey))
            return;
        ScheduleSave();
        Evaluate(ChangeSource.Editor);
    }

    /// <summary>Sets the look shown while a profile is on (see <see cref="Profile.Effect"/>).</summary>
    public void SetProfileEffect(Guid profileId, string? effect)
    {
        if (FindProfile(profileId) is not { } profile || profile.Effect == effect)
            return;
        profile.Effect = effect;
        ScheduleSave();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
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

    public Profile? DuplicateProfile(Guid profileId) =>
        FindProfile(profileId) is { } source ? AddProfile(source.Color, Loc.Format("Profiles_CopyName", source.Name)) : null;

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
            case EffectsAction:
                Config.Hotkeys.ToggleEffects = text;
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

    /// <summary>Registers hotkeys again after a setting that decides which exist (screen effects) changed.</summary>
    public void RefreshHotkeys() => SyncHotkeys();

    /// <summary>For settings changed directly on <see cref="Config"/> by a view model.</summary>
    public void SettingsChanged()
    {
        ScheduleSave();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
        Evaluate(ChangeSource.Editor);
    }

    /// <summary>Saves <see cref="AppConfig.Effects"/>. Effects don't affect profiles, so nothing else is redrawn.</summary>
    public void EffectsChanged() => ScheduleSave();

    public void OnDisplayChanged() => _colors.RefreshDisplays();

    internal void OnForegroundChanged()
    {
        // Only re-evaluate when it can matter, so switching windows costs nothing without app rules.
        if (Config.AppRules.Count > 0 || _manual is not null)
            Evaluate(ChangeSource.Automation);
        ForegroundChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMonitorsChanged()
    {
        // The colour thread finds monitors after startup and after display changes; rules may differ per monitor.
        var keys = _colors.Monitors.Select(m => m.Monitor.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (Current is null || !keys.SetEquals(Current.Monitors.Keys))
            Evaluate(ChangeSource.Automation);
        MonitorsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void OnResumed() => _colors.Reapply();

    /// <summary>Restores original colours and saves. Call before exiting.</summary>
    public void Shutdown()
    {
        FlushSave();
        if (!_colors.RestoreAll(TimeSpan.FromSeconds(3)))
            _logger.LogWarning("Not every monitor could be restored on exit");
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _saveTimer.Dispose();
        _ruleTimer.Dispose();
        _foreground.Dispose();
        _colors.Dispose();
    }

    private string? CommitHotkeyChange(string action)
    {
        ScheduleSave();
        SyncHotkeys();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
        return HotkeyFailures.GetValueOrDefault(action);
    }

    private void Evaluate(ChangeSource source)
    {
        var monitorKeys = _colors.Monitors.Select(m => m.Monitor.Key).ToList();
        var input = new RuleInput(Config, monitorKeys, _time.GetLocalNow().DateTime, _foreground.Current, _manual, _license.IsPro, _time.LocalTimeZone);
        RuleOutput output = RuleEngine.Resolve(input);

        // A manual pick from another context has ended.
        if (_manual is not null && _manual.ContextKey != output.ContextKey)
            _manual = null;

        // Monitors can appear later (the colour thread enumerates them); send a wildcard for the default.
        var targets = new Dictionary<string, ColorSettings?>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, MonitorTarget target) in output.Monitors)
            targets[key] = target.Settings;
        _colors.Apply(new MonitorTargets(targets, DefaultTarget(output)));

        bool profileChanged = Current?.ProfileId != output.ProfileId || Current?.Reason != output.Reason;
        Current = output;
        _ruleTimer.Change(DelayUntil(output.NextEvaluation), Timeout.InfiniteTimeSpan);

        // Live slider edits don't change which profile is on; they're reported through ProfileEdited instead,
        // so dragging a slider doesn't rebuild the tray menu and every page.
        if (profileChanged || source is ChangeSource.Startup or ChangeSource.Hotkey or ChangeSource.Tray)
            StateChanged?.Invoke(this, source);
    }

    private ColorSettings? DefaultTarget(RuleOutput output)
    {
        if (Config.IsPaused || output.ProfileId is not { } id || FindProfile(id) is not { } profile)
            return null;
        return profile.Color;
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
                TogglePause(ChangeSource.Hotkey);
                break;
            case NextAction:
                CycleProfile(1, ChangeSource.Hotkey);
                break;
            case PreviousAction:
                CycleProfile(-1, ChangeSource.Hotkey);
                break;
            case EffectsAction:
                EffectsToggleRequested?.Invoke(this, EventArgs.Empty);
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

        var desired = new Dictionary<string, HotkeyGesture>(StringComparer.Ordinal);
        foreach (Profile profile in Config.Profiles)
        {
            if (HotkeyGesture.TryParse(profile.Hotkey, out HotkeyGesture gesture))
                desired[ProfileActionPrefix + profile.Id] = gesture;
        }
        AddAction(desired, PauseAction, Config.Hotkeys.TogglePause);
        AddAction(desired, NextAction, Config.Hotkeys.NextProfile);
        AddAction(desired, PreviousAction, Config.Hotkeys.PreviousProfile);
        if (Config.Ui.ShowEffects)
            AddAction(desired, EffectsAction, Config.Hotkeys.ToggleEffects);
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
