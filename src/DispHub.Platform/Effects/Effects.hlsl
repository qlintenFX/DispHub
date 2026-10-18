// DispHub screen effects, compiled at run time with D3DCompile (shader model 4.0, so older GPUs work too).
//
// Where well-known work exists it is followed rather than reinvented:
// - Film grain: the AV1 film grain synthesis (dav1d, BSD-2; libaom's film grain test vectors). The template is
//   made on the CPU, see FilmGrain.cs.
// - CRT scanlines, shadow mask and screen curve: Timothy Lottes' public-domain CRT shader.
// - VHS: the effect chain of ntsc-rs (MIT/Apache-2.0/ISC): tape bandwidth for brightness and much less for
//   colour, colour delay, the tape's sharpening ring, colour phase noise, edge wave, head switching, tracking
//   noise and snow. ntsc-rs filters each scanline in order; here each step is a few taps per pixel.
//
// Passes: Quarter (quarter size: what glows, and the plain picture for the tape's colour), Blur (separable
// Gaussian), Main (everything else). Main runs in two modes. With a captured screen (HasSource = 1) it outputs the
// finished picture. Without one it outputs only the layers drawn on top (grain, vignette, scanlines...) as
// premultiplied alpha over the live desktop.
//
// Main runs for every pixel of the screen up to 240 times a second, on integrated GPUs too, so anything that is the
// same for a whole frame or a whole line is worked out on the CPU (ShaderConstants and the line data in
// EffectRenderer.cs, whose constant layout matches cbuffer Params).

Texture2D Source : register(t0);
Texture2D BloomTex : register(t1);
Texture2D GrainTex : register(t2);
Texture2D GrainScale : register(t3);   // AV1's grain strength by brightness, 256 entries
Texture2D Lines : register(t4);        // per 96-dpi line: sideways shift, tracking band, tape colour phase (cos, sin)
Texture2D QuarterTex : register(t5);   // the picture at quarter size
Texture2D<uint> GrainBlocks : register(t6);   // per 32-pixel grain block: where in the template it reads this frame
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
    float2 WeaveOffset;   // This frame's gate weave, in uv.

    float PaletteSize;    // 0 = no palette
    float PaletteSpread;
    float PaletteByBrightness;
    float Dim;

    float FlickerGain;    // This frame's brightness, 1 = no flicker.
    float ScratchX;       // This frame's dust scratch, in 96-dpi pixels; negative for none.
    float HasLines;       // Lines holds this frame's line data.
    float Unused2;

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

// Integer hash, so the noise is the same on every GPU (and in FrameNoise.cs, which works out the per-frame and
// per-line noise).
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

// ---- Quarter-size passes ----------------------------------------------------------------------------------------

struct QuarterOut
{
    float4 Bright : SV_Target0;   // What glows, for bloom and halation.
    float4 Plain : SV_Target1;    // The picture, for the tape's wide filters.
};

QuarterOut Quarter(VSOut i)
{
    // The target is a quarter of the source each way: four bilinear taps average a 4 x 4 block.
    float2 t = SourceTexel;
    float3 c = Src(i.Uv + t * float2(-1, -1)) + Src(i.Uv + t * float2(1, -1)) + Src(i.Uv + t * float2(-1, 1)) + Src(i.Uv + t * float2(1, 1));
    c *= 0.25;
    QuarterOut o;
    o.Bright = float4(c * saturate((Luma(c) - BloomThreshold) / max(1 - BloomThreshold, 0.05)), 1);
    o.Plain = float4(c, 1);
    return o;
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

// Screen curve (Lottes): rows bow with the distance from the middle, more for wider tubes.
float2 CrtWarp(float2 uv)
{
    float2 c = uv * 2 - 1;
    float k = Curvature * 1.6;
    c *= float2(1 + c.y * c.y * 0.031 * k, 1 + c.x * c.x * 0.041 * k);
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

float3 QuarterAt(float2 uv)
{
    return QuarterTex.SampleLevel(LinearClamp, uv, 0).rgb;
}

// VHS tape (after ntsc-rs): the tape keeps brightness at roughly 3 MHz and colour at 0.4 MHz, recorded a little
// late; playback sharpens brightness (which rings around edges) and colours drift from line to line (phase holds
// this line's colour error as cos, sin). The wide filters read the quarter-size picture, which already averages
// 4 x 4 pixels.
float3 Tape(float2 uv, float2 phase)
{
    float k = ChromaBleed;
    float px = PixelScale / OutSize.x;
    float lumaStep = (0.6 + k * 1.2) * px;
    // A five-step Gaussian (weights e^(-t*t/2)) from three taps: each outer pair merged into one bilinear tap.
    float y = (Luma(Fetch(uv)) + 0.7418 * (Luma(Fetch(uv + float2(1.1824 * lumaStep, 0))) + Luma(Fetch(uv - float2(1.1824 * lumaStep, 0))))) / 2.4836;
    float wide = (Luma(QuarterAt(uv - float2(4 * lumaStep, 0))) + Luma(QuarterAt(uv)) + Luma(QuarterAt(uv + float2(4 * lumaStep, 0)))) / 3;
    y += (y - wide) * 1.6 * k;

    float colourStep = k * 5 * px;
    float delay = k * 9 * px;
    float2 iq = (RgbToYiq(QuarterAt(uv - float2(delay - 1.25 * colourStep, 0))).yz + RgbToYiq(QuarterAt(uv - float2(delay + 1.25 * colourStep, 0))).yz) * 0.5;
    iq = float2(iq.x * phase.x - iq.y * phase.y, iq.x * phase.y + iq.y * phase.x) * (1 - 0.15 * k);
    return YiqToRgb(float3(y, iq));
}

float3 Picture(float2 uv, float2 phase)
{
    float3 c = ChromaBleed > 0 ? Tape(uv, phase) : Fetch(uv);
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

// One scanline beam (Lottes): a Gaussian across the line, summed with its neighbours' tails. hardScan goes from
// soft (-2) to tight (-12). Lottes leaves the screen darker; half of that is made up, without blowing out.
float Beam(float2 fine)
{
    float spacing = max(ScanSpacing, 1);
    float d = frac(fine.y / spacing) - 0.5;
    float hard = lerp(-2.0, -12.0, Scanlines);
    float w = exp2(hard * (d - 1) * (d - 1)) + exp2(hard * d * d) + exp2(hard * (d + 1) * (d + 1));
    float average = min(sqrt(3.14159265 / (-hard * 0.69314718)), 1);
    return w / lerp(1, average, 0.5);
}

// What multiplies the picture: vignette and flicker (and, without a picture, scanlines and the mask as shading).
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
    if (Scanlines > 0 && HasSource < 0.5)
    {
        // Only darkening is possible on top of the desktop: the beam at its brightest is 1.
        float hard = lerp(-2.0, -12.0, Scanlines);
        float average = min(sqrt(3.14159265 / (-hard * 0.69314718)), 1);
        m *= Beam(fine) * lerp(1, average, 0.5) / (1 + 2 * exp2(hard));
    }
    if (Mask > 0 && HasSource < 0.5)
        m *= lerp(1, fmod(fine.x, 3) < 1 ? 1 : 0.7, Mask);
    return m * FlickerGain * (1 - Dim);
}

// Aperture grille (Lottes' shadow mask 2): each column of three pixels lets through red, green or blue.
float3 GrilleMask(float2 pos)
{
    float dark = lerp(1, 0.5, Mask), light = lerp(1, 1.5, Mask);
    float column = frac(pos.x / 3);
    float3 mask = dark.xxx;
    if (column < 0.333)
        mask.r = light;
    else if (column < 0.666)
        mask.g = light;
    else
        mask.b = light;
    return mask;
}

// AV1's grain strength by brightness, 0 to 1: a 256-entry table, the way AV1 decoders build it from the scaling
// points (libaom's film grain test vector 1, see FilmGrain.cs).
float GrainStrength(float luma)
{
    return GrainScale.SampleLevel(LinearClamp, float2(saturate(luma) * (255.0 / 256) + 0.5 / 256, 0.5), 0).r;
}

// Film grain, the AV1 way: 32 by 32 blocks of the grain template, each from a random place in it every frame
// (picked on the CPU, see FrameNoise.GrainBlocks).
float3 FilmGrain(float2 pos, float3 picture)
{
    float2 p = pos / GrainSize;
    float2 block = floor(p / 32);
    uint r = GrainBlocks.Load(int3(block, 0));
    float2 offset = 3 + 2 * (3 + float2(r >> 4, r & 15));
    float2 t = offset + (p - block * 32);
    float3 g = GrainTex.SampleLevel(LinearClamp, t / float2(82, 73), 0).rgb;
    float strength = HasSource > 0.5 ? GrainStrength(Luma(picture)) : 0.75;
    float3 noise = g.rrr;
    if (GrainColor > 0)
        noise += GrainColor * 0.6 * float3(1.402 * g.b, -0.344 * g.g - 0.714 * g.b, 1.772 * g.g);
    return noise * Grain * 0.075 * strength;
}

// What adds light or dark: grain, dust and scratches, tape snow. Signed.
float3 Layers(float2 pos, float3 picture, float4 scan)
{
    uint seed = (uint)Seed;
    float3 add = 0;
    if (Grain > 0)
        add += FilmGrain(pos, picture);
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
        if (ScratchX >= 0)
            add -= 0.35 * smoothstep(1.5, 0, abs(pos.x - ScratchX));
    }
    if (Tracking > 0)
    {
        // Snow (ntsc-rs): dropouts start bright and fade along the line, many more inside the tracking band.
        uint row = (uint)pos.y;
        float inBand = scan.y;
        uint cell = (uint)(pos.x / 32);
        float chance = Tracking * (0.003 + inBand * 0.45);
        if (Hash(uint2(cell, row), seed) < chance)
        {
            float start = cell * 32 + Hash(uint2(cell, row), seed ^ 0x5bd1e995u) * 32;
            float tail = 4 + Hash(uint2(cell, row), seed ^ 0x27d4eb2fu) * 18;
            float x = pos.x - start;
            if (x >= 0)
                add += pow(saturate(1 - x / tail), 3) * (0.5 + 0.5 * inBand);
        }
        if (inBand > 0)
            add += (Hash(uint2(pos.x / 2, row), seed ^ 0x9e3779b9u) - 0.5) * inBand * Tracking * 0.35;
    }
    return add;
}

// ---- Main pass ------------------------------------------------------------------------------------------------

// This line's data (see Lines); none when the look doesn't move lines.
float4 LineData(float2 pos)
{
    return HasLines > 0.5 ? Lines.Load(int3(0, (int)pos.y, 0)) : float4(0, 0, 1, 0);
}

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
        // No picture: darken by alpha, lighten by adding white (premultiplied), then fade towards white on top.
        float dark = 1 - Darkening(fine, uv);
        float g = Layers(pos, 0.5, LineData(pos)).g;
        float a = abs(g);
        float4 o = float4((g > 0 ? a : 0).xxx, a + dark * (1 - a));
        return float4(FadeAmount + (1 - FadeAmount) * o.rgb, 1 - (1 - FadeAmount) * (1 - o.a));
    }

    if (Curvature > 0)
    {
        uv = CrtWarp(uv);
        if (any(uv < 0) || any(uv > 1))
            return float4(0, 0, 0, 1);
    }
    if (Distortion > 0)
        uv = Lens(uv);
    uv += WeaveOffset;

    // Lines pushed sideways (ntsc-rs's edge wave, head switching and tracking band), in 96-dpi pixels.
    float4 scan = LineData(pos);
    uv.x -= scan.x / size.x;

    // Pixel blocks snap to whole screen pixels, or small previews would shimmer.
    float block = Pixelate > 1 ? max(round(Pixelate * PixelScale), 1) : 1;
    if (block > 1)
        uv = (floor(uv * OutSize / block) + 0.5) * block / OutSize;

    float3 c = Picture(uv, scan.zw);
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
    if (Scanlines > 0 || Mask > 0)
    {
        // Lottes: beams and mask work on light, not on the encoded values. Gamma 2 (a square) is close enough
        // here and much cheaper than 2.2.
        float3 light = saturate(c);
        light *= light;
        if (Scanlines > 0)
            light *= Beam(fine);
        if (Mask > 0)
            light *= GrilleMask(fine);
        c = sqrt(light);
    }

    c = c * Darkening(fine, uv) + Layers(pos, c, scan);
    return float4(saturate(c), 1);
}
