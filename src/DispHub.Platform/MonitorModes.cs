// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Model;
using DispHub.Platform.Displays;
using Microsoft.Extensions.Logging;

namespace DispHub.Platform;

/// <summary>What a display can switch between: its refresh rates (lowest first) and whether it does HDR.</summary>
public sealed record ModeAbilities(IReadOnlyList<int> RefreshRates, bool Hdr);

/// <summary>What profiles want of one display. The default leaves it as it was.</summary>
public readonly record struct ModeTarget(RefreshChoice Refresh, bool? Hdr);

/// <summary>A display's refresh rate and HDR from before a profile changed them; a null part wasn't changed.</summary>
public readonly record struct SavedMode(int? RefreshHz, bool? Hdr)
{
    public bool IsEmpty => RefreshHz is null && Hdr is null;
}

/// <summary>Displays' refresh rate and Windows HDR, switched on a thread of their own: each switch blanks the screen for a moment.</summary>
public interface IMonitorModes : IDisposable
{
    /// <summary>Displays with more than one refresh rate, or with HDR, by monitor key.</summary>
    IReadOnlyDictionary<string, ModeAbilities> Abilities { get; }

    /// <summary>
    /// The modes displays had before a profile changed them, by monitor key. Raised on the worker thread whenever it
    /// changes, so the controller can keep it in the settings and put them back after a crash.
    /// </summary>
    event EventHandler<IReadOnlyDictionary<string, SavedMode>>? OriginalsChanged;

    /// <summary>Raised on the worker thread when <see cref="Abilities"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>Starts the worker, knowing the modes to put back that an earlier session left changed.</summary>
    void Start(IReadOnlyDictionary<string, SavedMode> originals);

    /// <summary>Finds what these displays can switch between.</summary>
    void Refresh(IReadOnlyList<MonitorInfo> monitors);

    /// <summary>
    /// What profiles want, by monitor key; a missing key puts back the mode from before. A change by hand happens
    /// right away. Automatic ones wait until they have held for a moment, so a quick look at another app doesn't
    /// blank the screen twice.
    /// </summary>
    void Apply(IReadOnlyDictionary<string, ModeTarget> targets, bool byHand);

    /// <summary>Puts back every mode a profile changed. Blocks up to <paramref name="timeout"/>.</summary>
    bool RestoreAll(TimeSpan timeout);
}

public sealed class MonitorModes : IMonitorModes
{
    /// <summary>How long an automatic change must hold before the screen switches.</summary>
    public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(1.5);

    /// <summary>How long the usual mode waits to come back, say after leaving a game for a moment.</summary>
    public static readonly TimeSpan PutBackTime = TimeSpan.FromSeconds(6);

    private static readonly Dictionary<string, ModeTarget> NoTargets = [];

    private readonly IDisplayModeBackend _backend;
    private readonly ILogger _logger;
    private readonly TimeSpan _settle;
    private readonly TimeSpan _putBack;
    private readonly Lock _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;

    // Guarded by _gate.
    private IReadOnlyList<MonitorInfo>? _pendingMonitors;
    private IReadOnlyDictionary<string, ModeTarget>? _pendingTargets;
    private bool _pendingByHand;
    private TaskCompletionSource<bool>? _pendingRestore;
    private bool _stopping;

    // Worker thread only.
    private readonly Dictionary<string, MonitorInfo> _monitors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ModeAbilities> _abilities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SavedMode> _originals = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, ModeTarget> _applied = NoTargets;
    private IReadOnlyDictionary<string, ModeTarget>? _waiting;
    private long _dueAt;

    private volatile IReadOnlyDictionary<string, ModeAbilities> _published = new Dictionary<string, ModeAbilities>();

    public MonitorModes(IDisplayModeBackend backend, ILogger<MonitorModes> logger)
        : this(backend, logger, SettleTime, PutBackTime)
    {
    }

    internal MonitorModes(IDisplayModeBackend backend, ILogger<MonitorModes> logger, TimeSpan settle, TimeSpan putBack)
    {
        _backend = backend;
        _logger = logger;
        _settle = settle;
        _putBack = putBack;
        _thread = new Thread(Run) { IsBackground = true, Name = "DispHub display modes" };
    }

    public IReadOnlyDictionary<string, ModeAbilities> Abilities => _published;

    public event EventHandler<IReadOnlyDictionary<string, SavedMode>>? OriginalsChanged;
    public event EventHandler? Changed;

    public void Start(IReadOnlyDictionary<string, SavedMode> originals)
    {
        ArgumentNullException.ThrowIfNull(originals);
        foreach ((string key, SavedMode mode) in originals)
            _originals[key] = mode;
        _thread.Start();
    }

    public void Refresh(IReadOnlyList<MonitorInfo> monitors)
    {
        lock (_gate)
            _pendingMonitors = monitors;
        _wake.Set();
    }

    public void Apply(IReadOnlyDictionary<string, ModeTarget> targets, bool byHand)
    {
        lock (_gate)
        {
            _pendingTargets = targets;
            _pendingByHand = byHand;
        }
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
    }

    private void Run()
    {
        while (true)
        {
            _wake.WaitOne(_waiting is null ? Timeout.Infinite : (int)Math.Max(0, _dueAt - Environment.TickCount64));
            IReadOnlyList<MonitorInfo>? monitors;
            IReadOnlyDictionary<string, ModeTarget>? targets;
            bool byHand;
            TaskCompletionSource<bool>? restore;
            bool stopping;
            lock (_gate)
            {
                (monitors, targets, byHand, restore, stopping) = (_pendingMonitors, _pendingTargets, _pendingByHand, _pendingRestore, _stopping);
                _pendingMonitors = null;
                _pendingTargets = null;
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

                if (monitors is not null)
                {
                    Probe(monitors);
                    Carry(_applied);
                }
                if (targets is not null)
                    Plan(targets, byHand);
                if (_waiting is { } due && Environment.TickCount64 >= _dueAt)
                {
                    _waiting = null;
                    Carry(due);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Display mode update failed");
                restore?.TrySetResult(false);
            }
        }
    }

    /// <summary>Carries out new targets now, or once they have held long enough.</summary>
    private void Plan(IReadOnlyDictionary<string, ModeTarget> targets, bool byHand)
    {
        // Back to what's on screen (a quick look at another app), or a fade step that changes nothing here.
        if (Same(targets, _applied))
        {
            _waiting = null;
            return;
        }
        if (_waiting is not null && Same(targets, _waiting))
            return;
        if (byHand)
        {
            _waiting = null;
            Carry(targets);
            return;
        }
        _waiting = targets;
        bool putsBackOnly = targets.Values.All(t => t == default);
        _dueAt = Environment.TickCount64 + (long)(putsBackOnly ? _putBack : _settle).TotalMilliseconds;
    }

    private void Probe(IReadOnlyList<MonitorInfo> monitors)
    {
        _monitors.Clear();
        _abilities.Clear();
        foreach (MonitorInfo monitor in monitors)
        {
            _monitors[monitor.Key] = monitor;
            Measure(monitor);
        }
        Publish();
    }

    private void Measure(MonitorInfo monitor)
    {
        IReadOnlyList<int> rates = _backend.RefreshRates(monitor);
        bool hdr = _backend.Hdr(monitor) is not null;
        if (rates.Count > 1 || hdr)
            _abilities[monitor.Key] = new ModeAbilities(rates, hdr);
        else
            _abilities.Remove(monitor.Key);
    }

    private void Carry(IReadOnlyDictionary<string, ModeTarget> targets)
    {
        _applied = targets;
        bool originalsChanged = false;
        bool abilitiesChanged = false;
        foreach ((string key, MonitorInfo monitor) in _monitors)
        {
            ModeTarget target = targets.GetValueOrDefault(key);
            ModeAbilities abilities = _abilities.GetValueOrDefault(key) ?? new ModeAbilities([], false);
            SavedMode before = _originals.GetValueOrDefault(key);
            SavedMode saved = before;

            // Both are kept before anything switches: on some displays HDR lowers the highest refresh rate.
            bool wantsHdr = abilities.Hdr && target.Hdr is not null;
            bool wantsRate = RefreshRates.Pick(abilities.RefreshRates, target.Refresh) is not null;
            if (wantsHdr && saved.Hdr is null)
                saved = saved with { Hdr = _backend.Hdr(monitor) };
            if ((wantsHdr || wantsRate) && saved.RefreshHz is null)
                saved = saved with { RefreshHz = _backend.RefreshRate(monitor) };

            // HDR first, as it changes which refresh rates there are.
            bool hdrSwitched = false;
            if (wantsHdr)
            {
                hdrSwitched = SetHdr(monitor, target.Hdr!.Value);
            }
            else if (saved.Hdr is { } was)
            {
                saved = saved with { Hdr = null };
                hdrSwitched = SetHdr(monitor, was);
            }
            if (hdrSwitched)
            {
                Measure(monitor);
                abilities = _abilities.GetValueOrDefault(key) ?? abilities;
                abilitiesChanged = true;
            }

            if (RefreshRates.Pick(abilities.RefreshRates, target.Refresh) is { } hz)
            {
                SetRefresh(monitor, hz);
            }
            else if (saved.RefreshHz is { } was && saved.Hdr is null)
            {
                // While HDR is held the rate it moved stays as it is; it comes back together with HDR.
                saved = saved with { RefreshHz = null };
                SetRefresh(monitor, was);
            }

            if (saved != before)
            {
                originalsChanged = true;
                if (saved.IsEmpty)
                    _originals.Remove(key);
                else
                    _originals[key] = saved;
            }
        }
        if (originalsChanged)
            OriginalsChanged?.Invoke(this, new Dictionary<string, SavedMode>(_originals, StringComparer.OrdinalIgnoreCase));
        if (abilitiesChanged)
            Publish();
    }

    /// <summary>Switches HDR unless it already is; true when the screen switched.</summary>
    private bool SetHdr(MonitorInfo monitor, bool on)
    {
        if (_backend.Hdr(monitor) is not { } current || current == on)
            return false;
        if (_backend.SetHdr(monitor, on))
            return true;
        _logger.LogWarning("Could not turn HDR {State} on {Device}", on ? "on" : "off", monitor.DeviceName);
        return false;
    }

    private bool SetRefresh(MonitorInfo monitor, int hz)
    {
        if (_backend.RefreshRate(monitor) == hz)
            return true;
        if (_backend.SetRefreshRate(monitor, hz))
            return true;
        _logger.LogWarning("Could not set {Device} to {Rate} Hz", monitor.DeviceName, hz);
        return false;
    }

    private bool RestoreOriginals()
    {
        bool ok = true;
        bool changed = false;
        // Displays that aren't connected keep theirs for next time.
        foreach ((string key, SavedMode saved) in _originals.Where(o => _monitors.ContainsKey(o.Key)).ToList())
        {
            MonitorInfo monitor = _monitors[key];
            if (saved.Hdr is { } hdr && _backend.Hdr(monitor) != hdr)
                ok &= SetHdr(monitor, hdr);
            if (saved.RefreshHz is { } hz)
                ok &= SetRefresh(monitor, hz);
            _originals.Remove(key);
            changed = true;
        }
        _applied = NoTargets;
        _waiting = null;
        if (changed)
            OriginalsChanged?.Invoke(this, new Dictionary<string, SavedMode>(_originals, StringComparer.OrdinalIgnoreCase));
        return ok;
    }

    private void Publish()
    {
        _published = new Dictionary<string, ModeAbilities>(_abilities, StringComparer.OrdinalIgnoreCase);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool Same(IReadOnlyDictionary<string, ModeTarget> a, IReadOnlyDictionary<string, ModeTarget> b) =>
        a.Keys.Concat(b.Keys).All(key => a.GetValueOrDefault(key) == b.GetValueOrDefault(key));
}
