using DispHub.Core.Effects;

namespace DispHub.Platform.Effects;

/// <summary>
/// The shader's noise (Effects.hlsl) for whatever is the same across a whole frame or a whole line, worked out once
/// here instead of for every pixel: gate weave, flicker, the dust scratch, and each line's sideways shift, tracking
/// band and tape colour error. The hashes match the shader's bit for bit.
/// </summary>
internal static class FrameNoise
{
    /// <summary>Floats per line in <see cref="Lines"/>: shift, tracking band, cos and sin of the colour error.</summary>
    public const int LineStride = 4;

    public static uint HashU(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }

    public static float Hash(uint x, uint y, uint seed) =>
        HashU((x * 1597334677u) ^ HashU((y * 3812015801u) ^ seed)) * (1f / 4294967295f);

    public static float Hash1(uint x) => HashU(x) * (1f / 4294967295f);

    /// <summary>Smooth noise along one axis: 0 to 1, changing gently every unit.</summary>
    public static float Noise1(float x, uint seed)
    {
        float i = MathF.Floor(x);
        float f = x - i;
        f = f * f * (3 - (2 * f));
        float a = Hash1(unchecked((uint)(int)i * 2654435761u) ^ seed);
        float b = Hash1(unchecked((uint)(int)(i + 1) * 2654435761u) ^ seed);
        return a + ((b - a) * f);
    }

    /// <summary>Whether <paramref name="look"/> moves or recolours whole lines, so needs <see cref="Lines"/>.</summary>
    public static bool NeedsLines(Look look) => look.Jitter > 0 || look.Tracking > 0 || look.ChromaBleed > 0;

    /// <summary>
    /// Fills <paramref name="lines"/> (<see cref="LineStride"/> floats per 96-dpi line) for one frame, after ntsc-rs:
    /// the edge wave, head switching at the bottom, the rolling tracking band, and the tape's colour phase error.
    /// </summary>
    /// <param name="height">The picture's height in 96-dpi lines.</param>
    public static void Lines(Look look, float time, uint seed, float height, Span<float> lines)
    {
        int count = lines.Length / LineStride;
        float jitter = (float)look.Jitter, tracking = (float)look.Tracking, bleed = (float)look.ChromaBleed;
        float bottomRows = height * 0.02f;
        float band = (Frac(time * 0.07f) * (height + 160)) - 80;
        for (int row = 0; row < count; row++)
        {
            float y = row + 0.5f;
            float shift = 0, inBand = 0;
            if (jitter > 0)
            {
                // Edge wave: every line wobbles a little, smoothly down the picture and over time.
                shift += (Noise1((y * 0.035f) + (time * 2.3f), 0x68e31da4u) - 0.5f) * jitter * 6;
                shift += (Noise1((y * 0.21f) + (time * 9f), 0x1b56c4e9u) - 0.5f) * jitter * 2;
            }
            if (tracking > 0)
            {
                // Head switching: the last lines before the bottom are pushed right, more the lower they are.
                float into = (y - (height - bottomRows)) / bottomRows;
                if (into > 0)
                    shift += tracking * ((MathF.Pow(into, 1.5f) * 26) + ((Hash((uint)row, 1, seed) - 0.5f) * 4));
                // The tracking band rolls down the picture; lines inside it shake sideways and fill with snow.
                float near = SmoothStep(48, 0, MathF.Abs(y - band));
                shift += near * tracking * (Noise1((y * 0.4f) + (time * 30), 0x2545f491u) - 0.5f) * 22;
                inBand = near + SmoothStep(height * 0.975f, height, y);
            }
            float phase = bleed > 0 ? (Hash((uint)row, 3, seed) - 0.5f) * bleed * 0.5f : 0;
            Span<float> line = lines.Slice(row * LineStride, LineStride);
            line[0] = shift;
            line[1] = inBand;
            line[2] = MathF.Cos(phase);
            line[3] = MathF.Sin(phase);
        }
    }

    /// <summary>
    /// Picks where each 32-pixel grain block reads the grain template this frame (a byte: column in the low four bits,
    /// row in the high four), as AV1 does with its random offsets.
    /// </summary>
    public static void GrainBlocks(uint seed, int columns, Span<uint> blocks)
    {
        for (int i = 0; i < blocks.Length; i++)
            blocks[i] = (uint)(Hash((uint)(i % columns), (uint)(i / columns), seed) * 255.99f);
    }

    private static float Frac(float x) => x - MathF.Floor(x);

    private static float SmoothStep(float from, float to, float x)
    {
        float t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - (2 * t));
    }
}
