namespace DispHub.Core.Color;

/// <summary>A 256-entry-per-channel gamma ramp in the layout SetDeviceGammaRamp expects (R, G, B).</summary>
public sealed class GammaRamp
{
    public const int Size = 256;

    private readonly ushort[] _values;

    public GammaRamp() => _values = new ushort[Size * 3];

    private GammaRamp(ushort[] values) => _values = values;

    public Span<ushort> Red => _values.AsSpan(0, Size);
    public Span<ushort> Green => _values.AsSpan(Size, Size);
    public Span<ushort> Blue => _values.AsSpan(Size * 2, Size);

    /// <summary>All three channels back to back, 768 values.</summary>
    public ReadOnlySpan<ushort> Values => _values;

    public Span<ushort> Channel(int index) => _values.AsSpan(index * Size, Size);

    /// <summary>Lets native code pin all 768 values with <c>fixed (ushort* p = ramp)</c>.</summary>
    public ref ushort GetPinnableReference() => ref _values[0];

    public static GammaRamp Identity()
    {
        var ramp = new GammaRamp();
        for (int c = 0; c < 3; c++)
        {
            Span<ushort> channel = ramp.Channel(c);
            for (int i = 0; i < Size; i++)
                channel[i] = (ushort)(i * 257);
        }
        return ramp;
    }

    public static GammaRamp FromValues(ReadOnlySpan<ushort> values)
    {
        if (values.Length != Size * 3)
            throw new ArgumentException($"A gamma ramp needs {Size * 3} values.", nameof(values));
        return new GammaRamp(values.ToArray());
    }

    public GammaRamp Clone() => new((ushort[])_values.Clone());

    /// <summary>The same ramp at <paramref name="factor"/> of its brightness, e.g. to dim a profile.</summary>
    public GammaRamp Scaled(double factor)
    {
        var scaled = new ushort[_values.Length];
        for (int i = 0; i < _values.Length; i++)
            scaled[i] = (ushort)Math.Clamp(Math.Round(_values[i] * factor), 0, ushort.MaxValue);
        return new GammaRamp(scaled);
    }

    /// <summary>
    /// When this ramp is a plain, uncoloured ramp turned down (what laptop power-saving dimmers write, such as
    /// Lenovo's OLED dimming), how far down: 0.6 for 60 %. Null for anything else.
    /// </summary>
    public double? DimFactor()
    {
        double factor = Math.Max(Red[^1], Math.Max(Green[^1], Blue[^1])) / (double)Identity().Red[^1];
        if (factor is < 0.05 or > 0.97)
            return null;
        for (int c = 0; c < 3; c++)
        {
            ReadOnlySpan<ushort> channel = Channel(c);
            for (int i = 0; i < Size; i += 17)
            {
                if (Math.Abs(channel[i] - (i * 257 * factor)) > 0.03 * ushort.MaxValue)
                    return null;
            }
        }
        return factor;
    }

    public bool ApproximatelyEquals(GammaRamp other, int tolerance)
    {
        ArgumentNullException.ThrowIfNull(other);
        for (int i = 0; i < _values.Length; i++)
        {
            if (Math.Abs(_values[i] - other._values[i]) > tolerance)
                return false;
        }
        return true;
    }

    public string ToBase64()
    {
        byte[] bytes = new byte[_values.Length * 2];
        Buffer.BlockCopy(_values, 0, bytes, 0, bytes.Length);
        return Convert.ToBase64String(bytes);
    }

    public static GammaRamp? FromBase64(string? base64)
    {
        if (string.IsNullOrEmpty(base64))
            return null;
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            if (bytes.Length != Size * 3 * 2)
                return null;
            ushort[] values = new ushort[Size * 3];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return new GammaRamp(values);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
