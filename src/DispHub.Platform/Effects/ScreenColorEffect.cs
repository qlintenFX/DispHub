using Windows.Win32;
using Windows.Win32.UI.Magnification;

namespace DispHub.Platform.Effects;

/// <summary>
/// The colour grade: Windows' full-screen colour matrix (the one the Magnifier's colour filters use). It applies to
/// the whole desktop at no cost, shows in screenshots and recordings, and Windows removes it when DispHub exits,
/// even after a crash. Call from one thread only.
/// </summary>
public sealed class ScreenColorEffect : IDisposable
{
    private static readonly float[] Identity =
    [
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 1, 0,
        0, 0, 0, 0, 1,
    ];

    private bool _initialized;

    public bool IsActive { get; private set; }

    /// <summary>Applies a row-major 5x5 matrix. Returns false if Windows refused it.</summary>
    public bool Apply(ReadOnlySpan<float> matrix)
    {
        if (matrix.Length != 25)
            throw new ArgumentException("A colour matrix has 25 values.", nameof(matrix));
        if (!_initialized)
        {
            if (!PInvoke.MagInitialize())
                return false;
            _initialized = true;
        }
        var effect = default(MAGCOLOREFFECT);
        matrix.CopyTo(effect.transform.AsSpan());
        IsActive = PInvoke.MagSetFullscreenColorEffect(in effect);
        return IsActive;
    }

    public void Reset()
    {
        if (!_initialized)
            return;
        Apply(Identity);
        PInvoke.MagUninitialize();
        _initialized = false;
        IsActive = false;
    }

    public void Dispose() => Reset();
}
