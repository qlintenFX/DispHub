namespace DispHub.Platform.Effects;

/// <summary>The overlay's HLSL: one full-screen triangle and a pixel shader that draws every layer at once.</summary>
internal static class OverlayShader
{
    public const string Source = """
        cbuffer Overlay : register(b0)
        {
            float2 Size; float Seed; float Grain;
            float GrainSize; float Vignette; float VignetteSize; float VignetteSoftness;
            float Scanlines; float ScanlineSpacing; float Flicker; float Letterbox;
        };

        float4 VS(uint id : SV_VertexID) : SV_Position
        {
            float2 uv = float2((id << 1) & 2, id & 2);
            return float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
        }

        // Integer hash: the same pattern on every GPU, no sin() precision issues.
        float Hash(uint2 p, uint seed)
        {
            uint h = (p.x * 1597334677u) ^ (p.y * 3812015801u) ^ (seed * 2798796415u);
            h = (h ^ (h >> 16)) * 2246822519u;
            h = (h ^ (h >> 13)) * 3266489917u;
            h ^= h >> 16;
            return h * (1.0 / 4294967295.0);
        }

        // Smooth value noise at the grain size, -1 to 1.
        float GrainNoise(float2 pos, uint seed)
        {
            float2 p = pos / GrainSize;
            uint2 i = (uint2)floor(p);
            float2 f = frac(p);
            f = f * f * (3 - 2 * f);
            float a = Hash(i, seed), b = Hash(i + uint2(1, 0), seed);
            float c = Hash(i + uint2(0, 1), seed), d = Hash(i + uint2(1, 1), seed);
            return (lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y) * 2 - 1) * 1.6;
        }

        float4 PS(float4 pos : SV_Position) : SV_Target
        {
            if (Letterbox > 0)
            {
                float bar = max(0, (Size.y - Size.x / Letterbox) * 0.5);
                if (pos.y < bar || pos.y > Size.y - bar)
                    return float4(0, 0, 0, 1);
            }

            float2 d = (pos.xy / Size - 0.5) * 2;
            float r = length(d) * 0.70710678;
            float vignette = Vignette * smoothstep(VignetteSize * (1 - VignetteSoftness), VignetteSize, r);
            float scan = Scanlines * 0.6 * 0.5 * (1 + cos(6.2831853 * pos.y / ScanlineSpacing));
            float dark = 1 - (1 - vignette) * (1 - scan) * (1 - Flicker);

            // Darkening layers are black; grain then lightens or darkens on top. Premultiplied alpha.
            float g = Grain > 0 ? clamp(GrainNoise(pos.xy, (uint)Seed) * Grain * 0.3, -1, 1) : 0;
            float a = abs(g);
            float3 rgb = (g > 0 ? 1 : 0) * a;
            return float4(rgb, a + dark * (1 - a));
        }
        """;
}
