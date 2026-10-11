using DispHub.Core.Color;
using DispHub.Core.Config;
using DispHub.Platform.Displays;
using DispHub.Platform.Gamma;
using Microsoft.Extensions.Logging;

namespace DispHub.Platform;

public enum MonitorColorState
{
    /// <summary>DispHub hasn't changed this monitor (or has restored it).</summary>
    Original,
    Applied,
    /// <summary>Windows refused the requested ramp; a milder version may be active.</summary>
    Limited,
    /// <summary>The monitor has no adjustable gamma ramp.</summary>
    Unsupported,
    /// <summary>HDR is on; Windows doesn't support gamma ramps in HDR, so only vibrance is applied.</summary>
    Hdr,
}

public sealed record MonitorStatus(MonitorInfo Monitor, MonitorColorState State, bool SupportsVibrance);

/// <summary>
/// Colours per monitor key; monitors not listed get <paramref name="Default"/>. Null settings mean
/// "restore the original colours".
/// </summary>
public sealed record MonitorTargets(IReadOnlyDictionary<string, ColorSettings?> PerMonitor, ColorSettings? Default)
{
    public static MonitorTargets None { get; } = new(new Dictionary<string, ColorSettings?>(), null);

    public ColorSettings? For(string monitorKey) =>
        PerMonitor.TryGetValue(monitorKey, out ColorSettings? settings) ? settings : Default;
}

public interface IColorApplier : IDisposable
{
    IReadOnlyList<MonitorStatus> Monitors { get; }

    /// <summary>Raised on the colour thread when the monitor list or a monitor's state changes.</summary>
    event EventHandler? StatusChanged;

    /// <summary>Raised on the colour thread when another program keeps resetting the display colours.</summary>
    event EventHandler? ExternalResetDetected;

    void Start();

    /// <summary>Requests these colours. Latest call wins.</summary>
    void Apply(MonitorTargets targets);

    /// <summary>Re-enumerates monitors and re-applies; call on display changes.</summary>
    void RefreshDisplays();

    /// <summary>Re-applies everything even if unchanged; call after resume or unlock.</summary>
    void Reapply();

    /// <summary>Restores every monitor DispHub touched and deletes the crash journal. Blocks up to <paramref name="timeout"/>.</summary>
    bool RestoreAll(TimeSpan timeout);
}

/// <summary>
/// Applies colours on one dedicated thread. Gamma calls can take up to 200 ms, so requests are coalesced:
/// only the newest target is applied. Before the first change to a monitor its original ramp and vibrance
/// are written to restore.json, so a crash can be undone on the next start.
/// </summary>
public sealed class ColorApplier : IColorApplier
{
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BackoffDuration = TimeSpan.FromMinutes(5);
    private const int MaxResetsPerMinute = 3;

    private readonly IDisplayBackend _backend;
    private readonly string _journalPath;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;

    // Guarded by _gate.
    private MonitorTargets _requested = MonitorTargets.None;
    private bool _pendingApply;
    private bool _pendingRefresh = true;
    private bool _pendingReapply;
    private TaskCompletionSource<bool>? _pendingRestore;
    private bool _stopping;

    // Colour thread only.
    private RestoreJournalData _journal = new();
    private List<MonitorInfo> _monitors = [];
    private readonly Dictionary<string, GammaRamp> _appliedRamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _appliedVibrance = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonitorColorState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<DateTimeOffset> _externalResets = new();
    private DateTimeOffset _backoffUntil;
    private RampLimits _limits = RampLimits.Default;

    private volatile IReadOnlyList<MonitorStatus> _status = [];

    public ColorApplier(IDisplayBackend backend, string journalPath, ILogger<ColorApplier> logger, TimeProvider? timeProvider = null)
    {
        _backend = backend;
        _journalPath = journalPath;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _thread = new Thread(Run) { IsBackground = true, Name = "DispHub colour" };
    }

    public IReadOnlyList<MonitorStatus> Monitors => _status;

    public event EventHandler? StatusChanged;
    public event EventHandler? ExternalResetDetected;

    public void Start() => _thread.Start();

    public void Apply(MonitorTargets targets)
    {
        lock (_gate)
        {
            _requested = targets;
            _pendingApply = true;
        }
        _wake.Set();
    }

    public void RefreshDisplays()
    {
        lock (_gate)
            _pendingRefresh = true;
        _wake.Set();
    }

    public void Reapply()
    {
        lock (_gate)
            _pendingReapply = true;
        _wake.Set();
    }

    public bool RestoreAll(TimeSpan timeout)
    {
        TaskCompletionSource<bool> done;
        lock (_gate)
        {
            if (!_thread.IsAlive)
                return true;
            done = _pendingRestore ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        _wake.Set();
        return done.Task.Wait(timeout) && done.Task.Result;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_stopping)
                return;
            _stopping = true;
        }
        _wake.Set();
        if (_thread.IsAlive)
            _thread.Join(TimeSpan.FromSeconds(3));
        _wake.Dispose();
        _backend.Dispose();
    }

    private void Run()
    {
        try
        {
            _backend.Initialize();
            _journal = JsonFiles.ReadJournal(_journalPath) ?? new RestoreJournalData();
            if (_journal.Monitors.Count > 0)
                _logger.LogWarning("Found colours from a previous session that didn't exit cleanly; they will be restored on exit.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Colour backend failed to initialise");
        }

        while (true)
        {
            _wake.WaitOne(WatchdogInterval);

            MonitorTargets requested;
            bool apply, refresh, reapply, stopping;
            TaskCompletionSource<bool>? restore;
            lock (_gate)
            {
                (requested, apply, refresh, reapply, restore, stopping) =
                    (_requested, _pendingApply, _pendingRefresh, _pendingReapply, _pendingRestore, _stopping);
                _pendingApply = _pendingRefresh = _pendingReapply = false;
                _pendingRestore = null;
            }

            try
            {
                if (restore is not null)
                {
                    restore.TrySetResult(RestoreOriginals());
                    continue;
                }

                if (stopping)
                    return;

                if (refresh)
                    RefreshMonitors();

                if (apply || refresh || reapply)
                    ApplyAll(requested, force: refresh || reapply);
                else
                    Watchdog(requested);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Colour update failed");
                restore?.TrySetResult(false);
            }
        }
    }

    private void RefreshMonitors()
    {
        _limits = _backend.GetRampLimits();
        _monitors = _backend.GetMonitors().ToList();
        _backend.Vibrance?.Refresh(_monitors);
        _appliedRamps.Clear();
        _appliedVibrance.Clear();
        _logger.LogInformation("Monitors: {Monitors}", string.Join(", ", _monitors.Select(m => $"{m.Name} ({m.DeviceName})")));
    }

    private void ApplyAll(MonitorTargets requested, bool force)
    {
        if (force)
        {
            _appliedRamps.Clear();
            _appliedVibrance.Clear();
        }

        var seenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MonitorInfo monitor in _monitors)
        {
            // In clone mode several monitors share one GDI device; the first one decides.
            if (!seenDevices.Add(monitor.DeviceName))
                continue;

            ColorSettings? settings = requested.For(monitor.Key);
            ApplyGamma(monitor, settings);
            ApplyVibrance(monitor, settings);
        }
        PublishStatus();
    }

    private void ApplyGamma(MonitorInfo monitor, ColorSettings? settings)
    {
        RestoreEntry? original = _journal.Monitors.GetValueOrDefault(monitor.Key);

        if (monitor.IsHdr)
        {
            // Gamma ramps are undefined in HDR. Put back the original if we changed it before HDR was enabled.
            if (original?.Ramp is not null && _appliedRamps.Remove(monitor.Key))
                SetOriginalRamp(monitor, original);
            _states[monitor.Key] = MonitorColorState.Hdr;
            return;
        }

        if (settings is null)
        {
            if (original?.Ramp is not null && (!_appliedRamps.TryGetValue(monitor.Key, out GammaRamp? last) || !IsOriginal(last, original)))
                SetOriginalRamp(monitor, original);
            _states[monitor.Key] = _states.GetValueOrDefault(monitor.Key) == MonitorColorState.Unsupported
                ? MonitorColorState.Unsupported
                : MonitorColorState.Original;
            return;
        }

        GammaRamp requestedRamp = RampBuilder.Build(settings);
        GammaRamp target = RampBuilder.FitToLimits(requestedRamp, _limits);
        if (_appliedRamps.TryGetValue(monitor.Key, out GammaRamp? applied) && applied.ApproximatelyEquals(target, 0))
            return;

        if (!EnsureOriginalCaptured(monitor))
        {
            _states[monitor.Key] = MonitorColorState.Unsupported;
            return;
        }

        GammaResult result = _backend.SetRamp(monitor.DeviceName, target);
        _appliedRamps[monitor.Key] = target;
        _states[monitor.Key] = result switch
        {
            GammaResult.Applied when ReferenceEquals(target, requestedRamp) => MonitorColorState.Applied,
            GammaResult.Applied or GammaResult.Rejected => MonitorColorState.Limited,
            _ => MonitorColorState.Unsupported,
        };
    }

    private void ApplyVibrance(MonitorInfo monitor, ColorSettings? settings)
    {
        var vibrance = _backend.Vibrance;
        if (vibrance?.GetRange(monitor.DeviceName) is not { } range)
            return;

        RestoreEntry? original = _journal.Monitors.GetValueOrDefault(monitor.Key);
        int level;
        if (settings is null)
        {
            if (original?.VibranceLevel is not { } originalLevel)
                return;
            level = originalLevel;
        }
        else
        {
            level = VibranceMapping.ToDriverLevel(settings.Vibrance, range);
        }

        if (_appliedVibrance.TryGetValue(monitor.Key, out int applied) && applied == level)
            return;

        if (settings is not null && original?.VibranceLevel is null)
        {
            int? current = vibrance.GetLevel(monitor.DeviceName);
            if (current is null)
                return;
            JournalEntry(monitor).VibranceLevel = current;
            SaveJournal();
        }

        if (vibrance.SetLevel(monitor.DeviceName, level))
            _appliedVibrance[monitor.Key] = level;
        else
            _logger.LogWarning("{Vendor} vibrance could not be set on {Monitor}", vibrance.Vendor, monitor.Name);
    }

    private bool EnsureOriginalCaptured(MonitorInfo monitor)
    {
        RestoreEntry entry = JournalEntry(monitor);
        if (entry.Ramp is not null)
            return true;

        GammaRamp? current = _backend.GetRamp(monitor.DeviceName);
        if (current is null)
            return false;

        // A broken ramp (e.g. left flat or inverted by a crashed tool) is replaced by identity.
        entry.Ramp = (RampLimits.Extended.Accepts(current) && current.Red[^1] > current.Red[0] ? current : GammaRamp.Identity()).ToBase64();
        SaveJournal();
        return true;
    }

    private RestoreEntry JournalEntry(MonitorInfo monitor)
    {
        if (!_journal.Monitors.TryGetValue(monitor.Key, out RestoreEntry? entry))
        {
            entry = new RestoreEntry { DeviceName = monitor.DeviceName };
            _journal.Monitors[monitor.Key] = entry;
        }
        return entry;
    }

    private void SetOriginalRamp(MonitorInfo monitor, RestoreEntry original)
    {
        GammaRamp ramp = GammaRamp.FromBase64(original.Ramp) ?? GammaRamp.Identity();
        _backend.SetRamp(monitor.DeviceName, ramp);
        _appliedRamps[monitor.Key] = ramp;
    }

    private static bool IsOriginal(GammaRamp ramp, RestoreEntry original) =>
        GammaRamp.FromBase64(original.Ramp) is { } originalRamp && originalRamp.ApproximatelyEquals(ramp, 0);

    private bool RestoreOriginals()
    {
        bool ok = true;
        foreach ((string key, RestoreEntry entry) in _journal.Monitors)
        {
            MonitorInfo? monitor = _monitors.Find(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));
            string? deviceName = monitor?.DeviceName ?? entry.DeviceName;
            if (deviceName is null)
                continue;

            if (entry.Ramp is not null && monitor is not { IsHdr: true })
            {
                GammaRamp ramp = GammaRamp.FromBase64(entry.Ramp) ?? GammaRamp.Identity();
                ok &= _backend.SetRamp(deviceName, ramp) != GammaResult.Failed;
            }

            if (entry.VibranceLevel is { } level && _backend.Vibrance is { } vibrance)
                ok &= vibrance.SetLevel(deviceName, level);
        }

        _journal = new RestoreJournalData();
        _appliedRamps.Clear();
        _appliedVibrance.Clear();
        foreach (string key in _states.Keys.ToList())
        {
            if (_states[key] is MonitorColorState.Applied or MonitorColorState.Limited)
                _states[key] = MonitorColorState.Original;
        }

        try
        {
            File.Delete(_journalPath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not delete the restore journal");
        }

        PublishStatus();
        return ok;
    }

    /// <summary>Other programs, games and display events can reset the ramp. Put ours back, unless it keeps happening.</summary>
    private void Watchdog(MonitorTargets requested)
    {
        DateTimeOffset now = _time.GetUtcNow();
        if (now < _backoffUntil || _appliedRamps.Count == 0)
            return;

        bool reset = false;
        foreach (MonitorInfo monitor in _monitors)
        {
            if (!_appliedRamps.TryGetValue(monitor.Key, out GammaRamp? ramp) || _states.GetValueOrDefault(monitor.Key) != MonitorColorState.Applied)
                continue;
            GammaRamp? current = _backend.GetRamp(monitor.DeviceName);
            if (current is not null && !current.ApproximatelyEquals(ramp, 512))
                reset = true;
        }

        if (!reset)
            return;

        while (_externalResets.Count > 0 && now - _externalResets.Peek() > TimeSpan.FromMinutes(1))
            _externalResets.Dequeue();
        _externalResets.Enqueue(now);

        if (_externalResets.Count > MaxResetsPerMinute)
        {
            _backoffUntil = now + BackoffDuration;
            _externalResets.Clear();
            _logger.LogWarning("Another program keeps changing the display colours; pausing re-apply for {Minutes} minutes", BackoffDuration.TotalMinutes);
            ExternalResetDetected?.Invoke(this, EventArgs.Empty);
            return;
        }

        _logger.LogInformation("Display colours were reset externally; re-applying");
        ApplyAll(requested, force: true);
    }

    private void SaveJournal()
    {
        try
        {
            JsonFiles.WriteJournal(_journalPath, _journal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Could not write the restore journal");
        }
    }

    private void PublishStatus()
    {
        bool vibrance(MonitorInfo m) => _backend.Vibrance?.GetRange(m.DeviceName) is not null;
        List<MonitorStatus> status = _monitors
            .Select(m => new MonitorStatus(m, _states.GetValueOrDefault(m.Key, MonitorColorState.Original), vibrance(m)))
            .ToList();
        if (status.SequenceEqual(_status))
            return;
        _status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
