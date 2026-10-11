namespace DispHub.Core.Effects;

/// <summary>
/// Builds the 5x5 colour matrix for a <see cref="LookSettings"/>. Rows are inputs (red, green, blue, alpha, 1) and
/// columns outputs, so a colour [r g b a 1] times the matrix gives the new colour; the last row adds offsets.
/// This is the layout Windows' full-screen colour effect expects.
/// </summary>
public static class LookMatrix
{
    public const int Size = 5;

    // Rec. 709 luminance, the weights sRGB screens use.
    private const double Wr = 0.2126, Wg = 0.7152, Wb = 0.0722;

    public static double[,] Identity()
    {
        var m = new double[Size, Size];
        for (int i = 0; i < Size; i++)
            m[i, i] = 1;
        return m;
    }

    /// <summary>Contrast, then saturation, sepia, tint and fade, in that order.</summary>
    public static double[,] Build(LookSettings look)
    {
        ArgumentNullException.ThrowIfNull(look);
        look = look.Clamp();
        double[,] m = Identity();
        m = Multiply(m, Contrast(look.Contrast));
        m = Multiply(m, Saturation(look.Saturation));
        m = Multiply(m, Blend(Identity(), Sepia(), look.Sepia));
        m = Multiply(m, Tint(look.TintHue, look.TintStrength));
        m = Multiply(m, Fade(look.Fade));
        return m;
    }

    /// <summary>Row-major floats, ready for MAGCOLOREFFECT.</summary>
    public static float[] ToFloats(double[,] m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var result = new float[Size * Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                result[(i * Size) + j] = (float)m[i, j];
        return result;
    }

    /// <summary>Applies the matrix to an RGB colour in 0-1, as Windows does, clamping the result.</summary>
    public static (double R, double G, double B) Apply(double[,] m, double r, double g, double b)
    {
        ArgumentNullException.ThrowIfNull(m);
        double Out(int j) => Math.Clamp((r * m[0, j]) + (g * m[1, j]) + (b * m[2, j]) + m[4, j], 0, 1);
        return (Out(0), Out(1), Out(2));
    }

    public static double[,] Multiply(double[,] a, double[,] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var m = new double[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                for (int k = 0; k < Size; k++)
                    m[i, j] += a[i, k] * b[k, j];
        return m;
    }

    private static double[,] Contrast(double c)
    {
        double[,] m = Identity();
        for (int i = 0; i < 3; i++)
        {
            m[i, i] = c;
            m[4, i] = 0.5 * (1 - c);
        }
        return m;
    }

    private static double[,] Saturation(double s)
    {
        double[,] m = Identity();
        double[] w = [Wr, Wg, Wb];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                m[i, j] = ((1 - s) * w[i]) + (i == j ? s : 0);
        return m;
    }

    /// <summary>The classic sepia tone, scaled so white stays at full brightness instead of clipping.</summary>
    private static double[,] Sepia()
    {
        double[,] m = Identity();
        double[,] tone =
        {
            { 0.393, 0.349, 0.272 },
            { 0.769, 0.686, 0.534 },
            { 0.189, 0.168, 0.131 },
        };
        const double White = 0.393 + 0.769 + 0.189;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                m[i, j] = tone[i, j] / White;
        return m;
    }

    /// <summary>Moves colours towards a tinted monochrome of the same brightness; 1 is a full duotone.</summary>
    private static double[,] Tint(double hue, double strength)
    {
        (double r, double g, double b) = HueToRgb(hue);
        double luma = (Wr * r) + (Wg * g) + (Wb * b);
        double[] k = [r / luma, g / luma, b / luma];
        double[] w = [Wr, Wg, Wb];
        double[,] m = Identity();
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                m[i, j] = ((1 - strength) * (i == j ? 1 : 0)) + (strength * w[i] * k[j]);
        return m;
    }

    /// <summary>Lifts the blacks, like faded print film.</summary>
    private static double[,] Fade(double f)
    {
        double[,] m = Identity();
        for (int i = 0; i < 3; i++)
        {
            m[i, i] = 1 - f;
            m[4, i] = f;
        }
        return m;
    }

    private static double[,] Blend(double[,] a, double[,] b, double t)
    {
        var m = new double[Size, Size];
        for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
                m[i, j] = (a[i, j] * (1 - t)) + (b[i, j] * t);
        return m;
    }

    private static (double R, double G, double B) HueToRgb(double hue)
    {
        double h = (((hue % 360) + 360) % 360) / 60;
        double x = 1 - Math.Abs((h % 2) - 1);
        return (int)h switch
        {
            0 => (1, x, 0),
            1 => (x, 1, 0),
            2 => (0, 1, x),
            3 => (0, x, 1),
            4 => (x, 0, 1),
            _ => (1, 0, x),
        };
    }
}
