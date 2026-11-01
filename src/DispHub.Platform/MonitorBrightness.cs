// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Platform.Displays;
using Microsoft.Extensions.Logging;

namespace DispHub.Platform;

/// <summary>Screens' own brightness (a laptop's through Windows, monitors over DDC/CI), set on a thread of its own.</summary>
public interface IMonitorBrightness : IDisposable
{
    /// <summary>Monitors whose own brightness can be set, by monitor key, with the level last read or set (percent).</summary>
    IReadOnlyDictionary<string, int> Levels { get; }

    /// <summary>
    /// The levels monitors had before a profile changed them, by monitor key. Raised on the worker thread whenever
    /// it changes, so the controller can keep it in the settings and put them back after a crash.
    /// </summary>
    event EventHandler<IReadOnlyDictionary<string, int>>? OriginalsChanged;

    /// <summary>Raised on the worker thread when <see cref="Levels"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>Starts the worker, knowing the levels to put back that an earlier session left changed.</summary>
    void Start(IReadOnlyDictionary<string, int> originals);

    /// <summary>Finds which of these screens answer.</summary>
    void Refresh(IReadOnlyList<MonitorInfo> monitors);

    /// <summary>What profiles want, by monitor key: a percentage, or null to put back the level from before. Latest call wins.</summary>
    void Apply(IReadOnlyDictionary<string, int?> targets);

    /// <summary>Sets a monitor's brightness by hand, as its own buttons would (the Displays page).</summary>
    void SetLevel(string monitorKey, int percent);

    /// <summary>
    /// Reads every screen's level again, for changes made outside DispHub that the screen can't report (a monitor's
    /// own buttons). Laptop screens report theirs as they happen.
    /// </summary>
    void Reread();

    /// <summary>Puts back every level a profile changed. Blocks up to <paramref name="timeout"/>.</summary>
    bool RestoreAll(TimeSpan timeout);
}

public sealed class MonitorBrightness : IMonitorBrightness
{
    private readonly IHardwareBrightness _hardware;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;

    // Guarded by _gate.
    private IReadOnlyList<MonitorInfo>? _pendingMonitors;
    private IReadOnlyDictionary<string, int?>? _pendingTargets;
    private readonly Dictionary<string, int> _pendingManual = new(StringComparer.OrdinalIgnoreCase);
    private TaskCompletionSource<bool>? _pendingRestore;
    private bool _pendingReread;
    private bool _stopping;

    // Worker thread only.
    private readonly Dictionary<string, MonitorInfo> _devices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _levels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _originals = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, int?> _targets = new Dictionary<string, int?>();

    // The level a profile last set per screen: set once, so a change by hand afterwards stays until the profile changes.
    private readonly Dictionary<string, int> _applied = new(StringComparer.OrdinalIgnoreCase);
    private IDisposable? _watch;

    private volatile IReadOnlyDictionary<string, int> _published = new Dictionary<string, int>();

    public MonitorBrightness(IHardwareBrightness hardware, ILogger<MonitorBrightness> logger)
    {
        _hardware = hardware;
        _logger = logger;
        _thread = new Thread(Run) { IsBackground = true, Name = "DispHub monitor brightness" };
    }

    public IReadOnlyDictionary<string, int> Levels => _published;

    public event EventHandler<IReadOnlyDictionary<string, int>>? OriginalsChanged;
    public event EventHandler? Changed;

    public void Start(IReadOnlyDictionary<string, int> originals)
    {
        ArgumentNullException.ThrowIfNull(originals);
        foreach ((string key, int level) in originals)
            _originals[key] = level;
        _thread.Start();
    }

    public void Refresh(IReadOnlyList<MonitorInfo> monitors)
    {
        lock (_gate)
            _pendingMonitors = monitors;
        Wake();
    }

    public void Apply(IReadOnlyDictionary<string, int?> targets)
    {
        lock (_gate)
            _pendingTargets = targets;
        Wake();
    }

    public void SetLevel(string monitorKey, int percent)
    {
        lock (_gate)
            _pendingManual[monitorKey] = Math.Clamp(percent, 0, 100);
        Wake();
    }

    public void Reread()
    {
        lock (_gate)
            _pendingReread = true;
        Wake();
    }

    public bool RestoreAll(TimeSpan timeout)
    {
        TaskCompletionSource<bool> done;
        lock (_gate)
        {
            if (!_thread.IsAlive || _stopping)
                return true;
            done = _pendingRestore ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        Wake();
        return done.Task.Wait(timeout) && done.Task.Result;
    }

    // Requests after Dispose are dropped: the thread has stopped and its event is gone (a window closing during
    // shut-down still asks for an update).
    private void Wake()
    {
        lock (_gate)
        {
            if (!_stopping)
                _wake.Set();
        }
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
        _watch?.Dispose();
        lock (_gate)
            _wake.Dispose();
    }

    private void Run()
    {
        while (true)
        {
            _wake.WaitOne();
            IReadOnlyList<MonitorInfo>? monitors;
            IReadOnlyDictionary<string, int?>? targets;
            Dictionary<string, int> manual;
            TaskCompletionSource<bool>? restore;
            bool stopping, reread;
            lock (_gate)
            {
                (monitors, targets, restore, stopping, reread) = (_pendingMonitors, _pendingTargets, _pendingRestore, _stopping, _pendingReread);
                manual = new Dictionary<string, int>(_pendingManual, StringComparer.OrdinalIgnoreCase);
                _pendingMonitors = null;
                _pendingTargets = null;
                _pendingRestore = null;
                _pendingReread = false;
                _pendingManual.Clear();
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

                bool changed = false;
                if (monitors is not null)
                    changed |= Probe(monitors);
                else if (reread)
                    changed |= ReadAgain();
                if (targets is not null)
                    _targets = targets;
                if (monitors is not null || targets is not null)
                    changed |= ApplyTargets();
                foreach ((string key, int percent) in manual)
                    changed |= Write(key, percent);
                if (changed)
                    Publish();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitor brightness update failed");
                restore?.TrySetResult(false);
            }
        }
    }

    private bool Probe(IReadOnlyList<MonitorInfo> monitors)
    {
        _devices.Clear();
        _levels.Clear();
        foreach (MonitorInfo monitor in monitors)
        {
            if (_hardware.ReadLevel(monitor) is not { } level)
                continue;
            _devices[monitor.Key] = monitor;
            _levels[monitor.Key] = level.Percent;
        }
        if (_devices.Count > 0)
            _logger.LogInformation("Screen brightness: {Monitors}", string.Join(", ", monitors.Where(m => _devices.ContainsKey(m.Key)).Select(m => m.Name)));
        // Screens may have come back with other levels: profiles set theirs again.
        _applied.Clear();
        // A laptop screen reports its changes, from its keys or Windows' slider, so DispHub shows them as they happen.
        if (_watch is null && _devices.Values.Any(m => m.IsInternal))
            _watch = _hardware.WatchChanges(Reread);
        return true;
    }

    private bool ReadAgain()
    {
        bool changed = false;
        foreach ((string key, MonitorInfo monitor) in _devices)
        {
            if (_hardware.ReadLevel(monitor) is { } level && _levels.GetValueOrDefault(key, -1) != level.Percent)
            {
                _levels[key] = level.Percent;
                changed = true;
            }
        }
        return changed;
    }

    private bool ApplyTargets()
    {
        bool changed = false;
        bool originalsChanged = false;
        foreach (string key in _devices.Keys)
        {
            int? target = _targets.GetValueOrDefault(key);
            if (target is { } percent)
            {
                // The level from before the first change is what comes back afterwards.
                if (!_originals.ContainsKey(key) && _levels.TryGetValue(key, out int current))
                {
                    _originals[key] = current;
                    originalsChanged = true;
                }
                if (_applied.GetValueOrDefault(key, -1) == percent)
                    continue;
                changed |= Write(key, percent);
                if (_levels.GetValueOrDefault(key, -1) == percent)
                    _applied[key] = percent;
            }
            else
            {
                _applied.Remove(key);
                if (_originals.Remove(key, out int original))
                {
                    originalsChanged = true;
                    changed |= Write(key, original);
                }
            }
        }
        if (originalsChanged)
            OriginalsChanged?.Invoke(this, new Dictionary<string, int>(_originals, StringComparer.OrdinalIgnoreCase));
        return changed;
    }

    private bool Write(string key, int percent)
    {
        if (!_devices.TryGetValue(key, out MonitorInfo? device) || _levels.GetValueOrDefault(key, -1) == percent)
            return false;
        if (!_hardware.WriteLevel(device, percent))
        {
            _logger.LogWarning("Could not set the brightness of {Device}", device.DeviceName);
            return false;
        }
        _levels[key] = percent;
        return true;
    }

    private bool RestoreOriginals()
    {
        bool ok = true;
        foreach ((string key, int original) in _originals)
        {
            if (_devices.ContainsKey(key) && _levels.GetValueOrDefault(key, -1) != original && !Write(key, original))
                ok = false;
        }
        _applied.Clear();
        if (_originals.Count > 0)
        {
            _originals.Clear();
            OriginalsChanged?.Invoke(this, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));
        }
        Publish();
        return ok;
    }

    private void Publish()
    {
        _published = new Dictionary<string, int>(_levels, StringComparer.OrdinalIgnoreCase);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
