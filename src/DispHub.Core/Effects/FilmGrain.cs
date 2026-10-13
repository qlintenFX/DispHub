namespace DispHub.Core.Effects;

/// <summary>
/// Film grain made the way the AV1 video format makes it (ported from dav1d, BSD-2, and libaom). Gaussian noise is
/// run through an auto-regressive filter, which clumps it like the silver crystals of real film. The shader lays
/// it on the picture in 32-pixel blocks, each at a new random place in this template every frame, stronger or
/// weaker by brightness. The filter and the brightness curve are libaom's first film grain test vector.
/// </summary>
public static class FilmGrain
{
    /// <summary>AV1's grain template size.</summary>
    public const int Width = 82;

    public const int Height = 73;

    private const int Pad = 3;
    private const int Lag = 2;
    private const int CoeffShift = 8;
    private const ushort Seed = 45231;

    // libaom film_grain_test_vectors[0]: luma, then Cb and Cr (whose last coefficient weighs the luma grain).
    private static readonly int[] LumaCoeffs = [0, 0, -58, 0, 0, 0, -76, 100, -43, 0, -51, 82];
    private static readonly int[] CbCoeffs = [0, 0, -49, 0, 0, 0, -36, 22, -30, 0, -38, 7, 39];
    private static readonly int[] CrCoeffs = [0, 0, -47, 0, 0, 0, -31, 31, -25, 0, -32, 13, -100];

    /// <summary>The same vector's grain strength by brightness (0 to 255 each), for the shader.</summary>
    public static IReadOnlyList<(int Luma, int Scale)> LumaScaling { get; } =
    [
        (16, 0), (25, 136), (33, 144), (41, 160), (48, 168), (56, 136), (67, 128),
        (82, 144), (97, 152), (113, 144), (128, 176), (143, 168), (158, 176), (178, 184),
    ];

    /// <summary>
    /// The grain template, row by row, four floats per texel: luma grain, Cb grain, Cr grain, 0. Each plane has
    /// unit variance.
    /// </summary>
    public static float[] Template()
    {
        int[,] luma = Generate(Seed, LumaCoeffs, null);
        int[,] cb = Generate((ushort)(Seed ^ 0xb524), CbCoeffs, luma);
        int[,] cr = Generate((ushort)(Seed ^ 0x49d8), CrCoeffs, luma);
        double sy = Deviation(luma), sb = Deviation(cb), sr = Deviation(cr);
        var texels = new float[Width * Height * 4];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = ((y * Width) + x) * 4;
                texels[i] = (float)(luma[y, x] / sy);
                texels[i + 1] = (float)(cb[y, x] / sb);
                texels[i + 2] = (float)(cr[y, x] / sr);
            }
        }
        return texels;
    }

    /// <summary>One plane: Gaussian noise, then the auto-regressive filter (dav1d's generate_grain_y/uv).</summary>
    private static int[,] Generate(ushort seed, int[] coeffs, int[,]? luma)
    {
        var buf = new int[Height, Width];
        uint state = seed;
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
                buf[y, x] = Gaussian(ref state);
        }

        for (int y = Pad; y < Height; y++)
        {
            for (int x = Pad; x < Width - Pad; x++)
            {
                int sum = 0, c = 0;
                for (int dy = -Lag; dy <= 0; dy++)
                {
                    for (int dx = -Lag; dx <= Lag; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            if (luma is not null)
                                sum += luma[y, x] * coeffs[c];
                            break;
                        }
                        sum += coeffs[c++] * buf[y + dy, x + dx];
                    }
                }
                buf[y, x] = Math.Clamp(buf[y, x] + Round2(sum, CoeffShift), -128, 127);
            }
        }
        return buf;
    }

    /// <summary>
    /// A Gaussian sample like AV1's table (standard deviation about 512, then shifted down by 4), from two draws
    /// of AV1's 16-bit random number generator.
    /// </summary>
    private static int Gaussian(ref uint state)
    {
        double u1 = (Random(11, ref state) + 0.5) / 2048.0;
        double u2 = (Random(11, ref state) + 0.5) / 2048.0;
        double g = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2) * 512;
        return Round2((int)Math.Round(g), 4);
    }

    /// <summary>AV1's linear feedback shift register (dav1d's get_random_number).</summary>
    private static int Random(int bits, ref uint state)
    {
        uint r = state;
        uint bit = ((r >> 0) ^ (r >> 1) ^ (r >> 3) ^ (r >> 12)) & 1;
        state = (r >> 1) | (bit << 15);
        return (int)((state >> (16 - bits)) & ((1u << bits) - 1));
    }

    private static int Round2(int x, int shift) => (x + ((1 << shift) >> 1)) >> shift;

    private static double Deviation(int[,] plane)
    {
        double sum = 0, squares = 0;
        int n = 0;
        // Only the part the shader reads (AV1 offsets start at 9 and blocks are 32 wide).
        for (int y = 9; y < Height; y++)
        {
            for (int x = 9; x < Width - Pad; x++)
            {
                sum += plane[y, x];
                squares += (double)plane[y, x] * plane[y, x];
                n++;
            }
        }
        double mean = sum / n;
        return Math.Max(Math.Sqrt((squares / n) - (mean * mean)), 1e-6);
    }
}
