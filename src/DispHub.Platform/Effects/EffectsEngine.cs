using System.Drawing;
using System.Runtime.InteropServices;
using DispHub.Core.Effects;
using Microsoft.Extensions.Logging;

namespace DispHub.Platform.Effects;

/// <param name="MaxFps">Most frames per second for processed looks; 0 follows the display's refresh rate.</param>
/// <param name="MainDisplayOnly">Only the main display; otherwise all of them.</param>
public sealed record EffectOptions(int MaxFps = 0, bool MainDisplayOnly = false);

/// <summary>
/// Shows a <see cref="Look"/> on the displays, each drawn by its own GPU thread (see <see cref="EffectOutput"/>).
/// Call from the UI thread.
/// </summary>
public sealed class EffectsEngine : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<EffectOutput> _outputs = [];
    private readonly HashSet<Rectangle> _hidden = [];
    private Look? _look;
    private EffectOptions _options = new();

    public EffectsEngine(ILogger logger)
    {
        _logger = logger;
    }

    public bool IsRunning => _outputs.Count > 0;

    /// <summary>The displays the effect is on, in physical pixels.</summary>
    public IReadOnlyList<Rectangle> Monitors => [.. _outputs.Select(o => o.Info.Bounds)];

    /// <summary>The most serious problem on any display.</summary>
    public EffectProblem Problem => _outputs.Count == 0 ? EffectProblem.None : _outputs.Max(o => o.Problem);

    /// <summary>Raised on a worker thread when <see cref="Problem"/> may have changed.</summary>
    public event EventHandler? ProblemChanged;

    /// <summary>Shows <paramref name="look"/>, or stops when it does nothing.</summary>
    public void Apply(Look look, EffectOptions options)
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(options);
        look = look.Clamp();
        if (look.IsEmpty)
        {
            Stop();
            return;
        }
        bool displaysChanged = _options.MainDisplayOnly != options.MainDisplayOnly;
        (_look, _options) = (look, options);
        if (displaysChanged || _outputs.Count == 0)
            SyncDisplays();
        foreach (EffectOutput output in _outputs)
            output.Update(look, options);
    }

    /// <summary>Call after displays are added, removed or changed.</summary>
    public void RefreshDisplays()
    {
        if (_look is not null && _outputs.Count > 0)
            SyncDisplays();
    }

    /// <summary>Hides the effect on one display, e.g. while a full-screen game covers it.</summary>
    public void SetHidden(Rectangle monitor, bool hidden)
    {
        if (hidden ? !_hidden.Add(monitor) : !_hidden.Remove(monitor))
            return;
        foreach (EffectOutput output in _outputs.Where(o => o.Info.Bounds == monitor))
            output.SetHidden(hidden);
    }

    public void Stop()
    {
        _look = null;
        foreach (EffectOutput output in _outputs)
            output.Dispose();
        _outputs.Clear();
        _hidden.Clear();
        ProblemChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();

    private void SyncDisplays()
    {
        List<OutputInfo> wanted;
        try
        {
            wanted = DxgiOutputs.Enumerate();
        }
        catch (COMException ex)
        {
            _logger.LogWarning(ex, "Couldn't list displays for effects");
            return;
        }
        if (_options.MainDisplayOnly)
            wanted = [.. wanted.Where(o => o.IsPrimary).Take(1)];

        foreach (EffectOutput gone in _outputs.Where(o => !wanted.Contains(o.Info)).ToList())
        {
            gone.Dispose();
            _outputs.Remove(gone);
        }
        foreach (OutputInfo info in wanted.Where(w => _outputs.All(o => o.Info != w)))
        {
            try
            {
                var output = new EffectOutput(info, _look!, _options, _logger);
                output.ProblemChanged += (_, _) => ProblemChanged?.Invoke(this, EventArgs.Empty);
                if (_hidden.Contains(info.Bounds))
                    output.SetHidden(true);
                _outputs.Add(output);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Couldn't start effects on {Display}", info.DeviceName);
            }
        }
    }
}
