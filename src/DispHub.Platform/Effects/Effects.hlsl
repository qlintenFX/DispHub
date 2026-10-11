// DispHub screen effects, compiled at run time with D3DCompile (shader model 4.0, so older GPUs work too).
//
// Passes: Bright (quarter size: what glows), Blur (separable Gaussian), Main (everything else).
// Main runs in two modes. With a captured screen (HasSource = 1) it outputs the finished picture. Without one it
// outputs only the layers drawn on top (grain, vignette, scanlines...) as premultiplied alpha over the live desktop.
// The constant layout matches ShaderConstants in EffectRenderer.cs.

Texture2D Source : register(t0);
Texture2D BloomTex : register(t1);
SamplerState LinearClamp : register(s0);

cbuffer Params : register(b0)
{
    float2 OutSize;
    float Time;
    float Seed;           // A whole number: changes once per grain frame.

    float HasSource;
    float Exposure;
    float Contrast;
    float Saturation;

    float Vibrance;
    float Temperature;
    float TintAmount;
    float FadeAmount;

    float SepiaAmount;
    float Posterize;
    float Sharpen;
    float BloomAmount;

    float Halation;
    float Aberration;
    float Distortion;
    float Vignette;

    float VignetteSize;
    float VignetteSoft;
    float Grain;
    float GrainSize;

    float GrainColor;
    float Flicker;
    float Weave;
    float Dust;

    float Letterbox;
    float Scanlines;
    float ScanSpacing;
    float Curvature;

    float Mask;
    float ChromaBleed;
    float Tracking;
    float Jitter;

    float Pixelate;
    float Dither;
    float CvdMode;
    float CvdStrength;

    float4 ShadowTint;    // rgb multiplier, (1, 1, 1) = none
    float4 HighlightTint;

    float BloomThreshold;
    float PixelScale;     // Screen pixels per 96-dpi pixel, so grain and noise keep their size on any display.
    float2 SourceTexel;

    float2 BlurStep;
    float2 Unused1;

    float PaletteSize;    // 0 = no palette
    float PaletteSpread;
    float PaletteByBrightness;
    float Dim;

    float4 Palette[16];
};

static const float BayerMatrix[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

struct VSOut
{
    float4 Position : SV_Position;
    float2 Uv : TEXCOORD0;
};

// One triangle that covers the target.
VSOut VS(uint id : SV_VertexID)
{
    VSOut o;
    o.Uv = float2((id << 1) & 2, id & 2);
    o.Position = float4(o.Uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return o;
}

// Integer hash, so the noise is the same on every GPU.
uint HashU(uint x)
{
    x ^= x >> 16;
    x *= 0x7feb352du;
    x ^= x >> 15;
    x *= 0x846ca68bu;
    x ^= x >> 16;
    return x;
}

float Hash(uint2 p, uint seed)
{
    return HashU(p.x * 1597334677u ^ HashU(p.y * 3812015801u ^ seed)) * (1.0 / 4294967295.0);
}

float Hash1(uint x)
{
    return HashU(x) * (1.0 / 4294967295.0);
}

float Luma(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

// The target's size in 96-dpi pixels.
float2 VirtualSize()
{
    return OutSize / PixelScale;
}

float3 Src(float2 uv)
{
    return Source.SampleLevel(LinearClamp, uv, 0).rgb;
}

// ---- Bloom passes ---------------------------------------------------------------------------------------------

float4 Bright(VSOut i) : SV_Target
{
    // The target is a quarter of the source each way: four bilinear taps average a 4 x 4 block.
    float2 t = SourceTexel;
    float3 c = Src(i.Uv + t * float2(-1, -1)) + Src(i.Uv + t * float2(1, -1)) + Src(i.Uv + t * float2(-1, 1)) + Src(i.Uv + t * float2(1, 1));
    c *= 0.25;
    return float4(c * saturate((Luma(c) - BloomThreshold) / max(1 - BloomThreshold, 0.05)), 1);
}

float4 Blur(VSOut i) : SV_Target
{
    // A 9-tap Gaussian from five bilinear samples.
    float3 c = Src(i.Uv) * 0.2270270;
    c += Src(i.Uv + BlurStep * 1.3846154) * 0.3162162;
    c += Src(i.Uv - BlurStep * 1.3846154) * 0.3162162;
    c += Src(i.Uv + BlurStep * 3.2307692) * 0.0702703;
    c += Src(i.Uv - BlurStep * 3.2307692) * 0.0702703;
    return float4(c, 1);
}

// Passes only where two captured frames differ; an occlusion query counts what passed, so the CPU learns whether
// the screen changed without reading it back.
float4 Differs(VSOut i) : SV_Target
{
    int3 p = int3(i.Position.xy, 0);
    if (all(Source.Load(p).rgb == BloomTex.Load(p).rgb))
        discard;
    return 0;
}

// ---- Geometry -------------------------------------------------------------------------------------------------

float2 CrtCurve(float2 uv)
{
    float2 c = uv * 2 - 1;
    float2 bend = abs(c.yx) / lerp(12.0, 3.2, Curvature);
    c += c * bend * bend;
    return c * 0.5 + 0.5;
}

// Barrel distortion that keeps the corners in place, like a wide lens.
float2 Lens(float2 uv)
{
    float aspect = OutSize.x / OutSize.y;
    float2 c = (uv - 0.5) * float2(aspect, 1);
    float k = Distortion * 0.6;
    float corner = 0.25 * (aspect * aspect + 1);
    c *= (1 + k * dot(c, c)) / (1 + k * corner);
    return c / float2(aspect, 1) + 0.5;
}

// ---- Sampling -------------------------------------------------------------------------------------------------

float3 Fetch(float2 uv)
{
    if (Aberration > 0)
    {
        float2 d = (uv - 0.5) * Aberration * 0.012;
        return float3(Src(uv + d).r, Src(uv).g, Src(uv - d).b);
    }
    return Src(uv);
}

float3 RgbToYiq(float3 c)
{
    return float3(dot(c, float3(0.299, 0.587, 0.114)), dot(c, float3(0.596, -0.274, -0.322)), dot(c, float3(0.211, -0.523, 0.312)));
}

float3 YiqToRgb(float3 y)
{
    return float3(dot(y, float3(1, 0.956, 0.621)), dot(y, float3(1, -0.272, -0.647)), dot(y, float3(1, -1.106, 1.703)));
}

float3 Picture(float2 uv)
{
    float3 c = Fetch(uv);
    if (ChromaBleed > 0)
    {
        // Tape: sharp brightness, smeared and slightly late colour.
        float3 yiq = RgbToYiq(c);
        float2 iq = 0;
        float smear = ChromaBleed * 2.5 / VirtualSize().x;
        float shift = ChromaBleed * 3 / VirtualSize().x;
        [unroll] for (int k = 1; k <= 6; k++)
            iq += RgbToYiq(Src(uv - float2(smear * k + shift, 0))).yz;
        yiq.yz = lerp(yiq.yz, iq / 6, saturate(ChromaBleed * 1.4));
        c = YiqToRgb(yiq);
    }
    if (Sharpen > 0)
    {
        float2 t = 1 / OutSize;
        float3 n = Fetch(uv + float2(0, -t.y));
        float3 s = Fetch(uv + float2(0, t.y));
        float3 e = Fetch(uv + float2(t.x, 0));
        float3 w = Fetch(uv + float2(-t.x, 0));
        float3 average = (n + s + e + w) * 0.25;
        // Sharpen less where there's already strong contrast, which avoids halos (like AMD's CAS).
        float hi = max(max(max(Luma(n), Luma(s)), max(Luma(e), Luma(w))), Luma(c));
        float lo = min(min(min(Luma(n), Luma(s)), min(Luma(e), Luma(w))), Luma(c));
        c += (c - average) * Sharpen * 2.2 * (1 - saturate((hi - lo) * 1.5));
    }
    return c;
}

// ---- Colour ---------------------------------------------------------------------------------------------------

// Daltonisation (Fidaner, Lin and Ozguven): simulate the colour vision deficiency in LMS space and move the
// difference into channels that are still seen.
float3 ColourVision(float3 c)
{
    float3x3 toLms = float3x3(17.8824, 43.5161, 4.11935, 3.45565, 27.1554, 3.86714, 0.0299566, 0.184309, 1.46709);
    float3x3 toRgb = float3x3(0.0809444479, -0.130504409, 0.116721066, -0.0102485335, 0.0540193266, -0.113614708, -0.000365296938, -0.00412161469, 0.693511405);
    float3 lms = mul(toLms, c);
    float3 seen;
    if (CvdMode < 1.5)
        seen = float3(2.02344 * lms.y - 2.52581 * lms.z, lms.y, lms.z);
    else if (CvdMode < 2.5)
        seen = float3(lms.x, 0.494207 * lms.x + 1.24827 * lms.z, lms.z);
    else
        seen = float3(lms.x, lms.y, -0.395913 * lms.x + 0.801109 * lms.y);
    float3 error = c - mul(toRgb, seen);
    float3 shift = float3(0, error.r * 0.7 + error.g, error.r * 0.7 + error.b);
    return c + shift * CvdStrength;
}

float3 Grade(float3 c)
{
    c *= exp2(Exposure);
    c *= float3(1 + Temperature * 0.25, 1 - TintAmount * 0.15, 1 - Temperature * 0.25);
    c = (c - 0.5) * Contrast + 0.5;

    float l = Luma(c);
    float chroma = max(max(c.r, c.g), c.b) - min(min(c.r, c.g), c.b);
    c = lerp(l.xxx, c, max(Saturation * (1 + Vibrance * (1 - saturate(chroma * 1.5))), 0));

    // Split toning: shadows lean one way, highlights another. The tints keep brightness the same.
    c *= lerp(ShadowTint.rgb, HighlightTint.rgb, smoothstep(0.05, 0.95, saturate(Luma(c))));

    if (SepiaAmount > 0)
    {
        float3 sepia = float3(dot(c, float3(0.393, 0.769, 0.189)), dot(c, float3(0.349, 0.686, 0.168)), dot(c, float3(0.272, 0.534, 0.131))) / 1.351;
        c = lerp(c, sepia, SepiaAmount);
    }
    if (CvdMode > 0.5)
        c = ColourVision(c);
    return FadeAmount + c * (1 - FadeAmount);
}

// ---- Fewer colours ---------------------------------------------------------------------------------------------

// Posterizes or limits to the palette; threshold (-0.5 to 0.5, from the Bayer matrix) dithers.
float3 Quantize(float3 c, float threshold)
{
    if (PaletteSize > 0 && PaletteByBrightness > 0.5)
    {
        // Single-hue screens: the palette runs dark to light, so spread brightness evenly over it.
        float last = PaletteSize - 1;
        return Palette[(int)clamp(floor(saturate(Luma(c)) * last + 0.5 + threshold * Dither), 0, last)].rgb;
    }
    if (PaletteSize > 0)
    {
        c += threshold * PaletteSpread * Dither;
        float3 best = Palette[0].rgb;
        float bestDistance = 1e9;
        [loop] for (int k = 0; k < (int)PaletteSize; k++)
        {
            float3 d = c - Palette[k].rgb;
            float distance = dot(d * d, float3(0.299, 0.587, 0.114));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = Palette[k].rgb;
            }
        }
        return best;
    }
    if (Posterize > 1)
        return floor(c * (Posterize - 1) + 0.5 + threshold * Dither) / (Posterize - 1);
    return c;
}

// ---- Layers on top --------------------------------------------------------------------------------------------

// What multiplies the picture: vignette, scanlines, flicker (and, without a picture, the CRT mask as brightness).
// Scanlines and the mask follow real pixels (never finer than one), or they'd shimmer.
float Darkening(float2 fine, float2 uv)
{
    float m = 1;
    if (Vignette > 0)
    {
        float aspect = OutSize.x / OutSize.y;
        float2 d = (uv - 0.5) * 2 * float2(sqrt(aspect), 1 / sqrt(aspect));
        float r = length(d) * 0.70710678;
        m *= 1 - Vignette * smoothstep(VignetteSize * (1 - VignetteSoft), VignetteSize, r);
    }
    if (Scanlines > 0)
        m *= 1 - Scanlines * 0.3 * (1 + cos(6.2831853 * fine.y / ScanSpacing));
    if (Mask > 0 && HasSource < 0.5)
        m *= lerp(1, fmod(fine.x, 3) < 1 ? 1 : 0.7, Mask);
    if (Flicker > 0)
        m *= 1 - Flicker * 0.12 * Hash1((uint)Seed * 747796405u + 1u);
    return m * (1 - Dim);
}

// An aperture grille: each column of three pixels lets through red, green or blue.
float3 GrilleMask(float2 pos)
{
    float column = fmod(pos.x, 3);
    float dim = 1 - Mask * 0.6;
    float3 mask = column < 1 ? float3(1, dim, dim) : column < 2 ? float3(dim, 1, dim) : float3(dim, dim, 1);
    return mask * 3 / (1 + 2 * dim);
}

// What adds light or dark: grain, dust and scratches, tape noise. Signed.
float3 Layers(float2 pos, float3 picture)
{
    uint seed = (uint)Seed;
    float3 add = 0;
    if (Grain > 0)
    {
        float2 p = pos / GrainSize;
        uint2 cell = uint2(floor(p));
        float2 f = frac(p);
        f = f * f * (3 - 2 * f);
        float a = Hash(cell, seed), b = Hash(cell + uint2(1, 0), seed);
        float c = Hash(cell + uint2(0, 1), seed), d = Hash(cell + uint2(1, 1), seed);
        float n = (lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y) * 2 - 1) * 1.6;
        float3 noise = n.xxx;
        if (GrainColor > 0)
        {
            float g = (Hash(cell, seed ^ 0x9e3779b9u) * 2 - 1) * 1.6;
            float h = (Hash(cell, seed ^ 0x85ebca6bu) * 2 - 1) * 1.6;
            noise = lerp(noise, float3(n, g, h), GrainColor);
        }
        // Film grain shows most in the mid-tones.
        float weight = HasSource > 0.5 ? 1.2 - abs(Luma(picture) - 0.5) * 1.4 : 1;
        add += noise * Grain * 0.12 * weight;
    }
    if (Dust > 0)
    {
        uint2 cell = uint2(pos / 56);
        if (Hash(cell, seed * 3u + 11u) > 1 - Dust * 0.012)
        {
            float2 centre = float2(cell) * 56 + 8 + float2(Hash(cell, seed + 5u), Hash(cell, seed + 9u)) * 40;
            float speck = smoothstep(3.0, 0.5, length(pos - centre));
            add += (Hash(cell, seed + 17u) > 0.5 ? 0.6 : -0.6) * speck;
        }
        // Now and then a thin vertical scratch.
        if (Hash1(seed * 31u + 7u) < Dust * 0.5)
        {
            float x = Hash1(seed * 17u + 3u) * VirtualSize().x;
            add -= 0.35 * smoothstep(1.5, 0, abs(pos.x - x));
        }
    }
    if (Tracking > 0)
    {
        // A band of tape noise drifting down the screen, and head-switching noise along the bottom.
        float band = frac(Time * 0.11);
        float y = pos.y / VirtualSize().y;
        float inBand = smoothstep(0.05, 0, abs(y - band)) + smoothstep(0.985, 1, y);
        float streak = step(1 - Tracking * 0.35, Hash(uint2(pos.x / 3, pos.y), seed));
        add += streak * inBand * Tracking * 0.8;
    }
    return add;
}

// ---- Main pass ------------------------------------------------------------------------------------------------

float4 Main(VSOut i) : SV_Target
{
    float2 pos = i.Position.xy / PixelScale;
    float2 fine = i.Position.xy / max(PixelScale, 1);
    float2 size = VirtualSize();
    if (Letterbox > 0)
    {
        float bar = max(0, (size.y - size.x / Letterbox) * 0.5);
        if (pos.y < bar || pos.y > size.y - bar)
            return float4(0, 0, 0, 1);
    }

    float2 uv = i.Uv;
    if (HasSource < 0.5)
    {
        // No picture: darken by alpha, lighten by adding white (premultiplied).
        float dark = 1 - Darkening(fine, uv);
        float g = Layers(pos, 0.5).g;
        float a = abs(g);
        return float4((g > 0 ? a : 0).xxx, a + dark * (1 - a));
    }

    uint seed = (uint)Seed;
    if (Curvature > 0)
    {
        uv = CrtCurve(uv);
        if (any(uv < 0) || any(uv > 1))
            return float4(0, 0, 0, 1);
    }
    if (Distortion > 0)
        uv = Lens(uv);
    if (Weave > 0)
        uv += (float2(Hash1(seed * 13u + 1u), Hash1(seed * 29u + 2u)) - 0.5) * Weave * float2(0.0025, 0.004);
    if (Jitter > 0)
    {
        uint row = (uint)(pos.y / 2);
        float shift = (Hash(uint2(row, 0), seed) - 0.5) * Jitter * 3;
        if (Hash(uint2(row / 40, 7), seed / 6) > 0.96)
            shift += Jitter * 18 * (Hash(uint2(row / 40, 9), seed) - 0.3);
        uv.x += shift / size.x;
    }
    // Pixel blocks snap to whole screen pixels, or small previews would shimmer.
    float block = Pixelate > 1 ? max(round(Pixelate * PixelScale), 1) : 1;
    if (block > 1)
        uv = (floor(uv * OutSize / block) + 0.5) * block / OutSize;

    float3 c = Picture(uv);
    if (BloomAmount > 0 || Halation > 0)
    {
        float3 glow = BloomTex.SampleLevel(LinearClamp, uv, 0).rgb;
        c += glow * BloomAmount * 1.4;
        c += glow * float3(1, 0.32, 0.18) * Halation * 1.1;
    }
    c = Grade(c);

    if (Posterize > 1 || PaletteSize > 0)
    {
        // Dither per pixel, or per block when pixelated, like the real thing.
        uint2 p = uint2(block > 1 ? floor(i.Position.xy / block) : fine) % 4;
        c = Quantize(saturate(c), (BayerMatrix[p.y * 4 + p.x] + 0.5) / 16.0 - 0.5);
    }
    if (Mask > 0)
        c *= GrilleMask(fine);

    c = c * Darkening(fine, uv) + Layers(pos, c);
    return float4(saturate(c), 1);
}
