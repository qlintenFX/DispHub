// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace DispHub.Core.Color;

/// <summary>How a <see cref="LiquidGlass"/> panel looks. Lengths are in the picture's pixels.</summary>
/// <param name="CornerRadius">Rounding of the panel's corners; half its height makes a pill.</param>
/// <param name="BevelDepth">How far in from the edge the glass curves; the bend is strongest at the very edge.</param>
/// <param name="Scale">Picture pixels per point, so the fine details (rim, shadow) keep their size on screen.</param>
public sealed record GlassLook(
    double CornerRadius,
    double BevelDepth,
    double Scale = 1,
    double Refraction = 0.69,
    double ChromaticAberration = 0.05,
    double EdgeHighlight = 0.35,
    double Specular = 0.25,
    double Fresnel = 1.0,
    double Brightness = -0.04,
    double ShadowOpacity = 0.3);

/// <summary>
/// A clear "liquid glass" panel drawn onto a picture: the glass thickens towards a curved bevel at its edge, which
/// bends the picture behind it like a lens, splits colours slightly, catches light along the top rim and reflects
/// at grazing angles, and casts a soft shadow. Pure maths on a BGRA buffer, so it runs anywhere and can be tested.
/// </summary>
/// <remarks>
/// The model (rounded-rectangle distance field, half-circle bevel, two-surface refraction, dispersion, Fresnel,
/// Blinn-Phong lights, inner stroke and contact shadow) follows the fragment shader of
/// <a href="https://github.com/ybouane/liquidglass">liquidglass</a> by ybouane (MIT; see THIRD_PARTY_NOTICES.md),
/// computed on the CPU here because DispHub draws its previews in software.
/// </remarks>
public static class LiquidGlass
{
    private const double RefractiveIndex = 1.5;

    /// <summary>
    /// Draws a glass panel of <paramref name="width"/> by <paramref name="height"/> centred on
    /// (<paramref name="centreX"/>, <paramref name="centreY"/>) onto a copy of <paramref name="pixels"/> (BGRA, rows of
    /// <paramref name="pictureWidth"/> pixels) and returns it.
    /// </summary>
    public static byte[] Draw(byte[] pixels, int pictureWidth, int pictureHeight, double centreX, double centreY, double width, double height, GlassLook look)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(look);
        if (pictureWidth <= 0 || pictureHeight <= 0 || pixels.Length != pictureWidth * pictureHeight * 4)
            throw new ArgumentException("The buffer doesn't match the picture's size.", nameof(pixels));

        var picture = new Picture(pixels, pictureWidth, pictureHeight);
        double s = look.Scale;
        double halfW = width / 2, halfH = height / 2;
        double radius = Math.Min(look.CornerRadius, Math.Min(halfW, halfH));
        double bevel = Math.Max(look.BevelDepth, s);
        double shadowSpread = 10 * s, shadowOffset = 1 * s;
        double step = 2 * s;                  // the distance over which the bevel's slope is measured
        double refractPower = 1 - (1 / RefractiveIndex);
        double maxInside = Math.Min(halfW, halfH);
        byte[] result = (byte[])pixels.Clone();

        // Everything the panel touches: the panel and its shadow.
        double pad = 3 * shadowSpread;
        int x0 = Math.Max(0, (int)Math.Floor(centreX - halfW - pad)), x1 = Math.Min(pictureWidth, (int)Math.Ceiling(centreX + halfW + pad));
        int y0 = Math.Max(0, (int)Math.Floor(centreY - halfH - pad)), y1 = Math.Min(pictureHeight, (int)Math.Ceiling(centreY + halfH + pad));

        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                // Pixel centre relative to the panel's centre, y pointing down.
                double px = x + 0.5 - centreX, py = y + 0.5 - centreY;
                double sdf = RoundedRect(px, py, halfW, halfH, radius);
                int i = ((y * pictureWidth) + x) * 4;

                // A soft drop shadow and a tighter contact shadow, a little below the panel.
                double d = Math.Max(RoundedRect(px, py - shadowOffset, halfW, halfH, radius) - 1, 0);
                double outer = Math.Exp(-d * d / (shadowSpread * shadowSpread)) * 0.65;
                double contact = Math.Exp(-d * 0.08 / Math.Max(shadowSpread * 0.04, 0.01)) * 0.35;
                double shade = 1 - Math.Clamp((outer + contact) * look.ShadowOpacity, 0, 1);
                double baseB = pixels[i] * shade, baseG = pixels[i + 1] * shade, baseR = pixels[i + 2] * shade;

                // The glass fades in over the last pixel or two of its edge, which keeps the outline smooth.
                double mask = 1 - SmoothStep(-1.5 * s, 0.5 * s, sdf);
                if (mask <= 0)
                {
                    result[i] = ToByte(baseB);
                    result[i + 1] = ToByte(baseG);
                    result[i + 2] = ToByte(baseR);
                    continue;
                }

                double inside = -sdf;
                double edge = SmoothStep(maxInside * 0.35, 0, inside);

                // The glass's thickness rises along a half circle from the edge; its slope gives the surface normal.
                double thickness = Bevel(inside, bevel);
                double slopeX = (Bevel(-RoundedRect(px + step, py, halfW, halfH, radius), bevel) - Bevel(-RoundedRect(px - step, py, halfW, halfH, radius), bevel)) / (2 * step);
                double slopeY = (Bevel(-RoundedRect(px, py + step, halfW, halfH, radius), bevel) - Bevel(-RoundedRect(px, py - step, halfW, halfH, radius), bevel)) / (2 * step);
                double nLength = Math.Sqrt((slopeX * slopeX) + (slopeY * slopeY) + 1);
                double nx = -slopeX / nLength, ny = -slopeY / nLength, nz = 1 / nLength;
                double depth = SmoothStep(0, bevel, inside);

                // Light bends where it enters and leaves both curved faces, more where the glass is thicker, and the
                // whole panel magnifies a little towards its middle.
                double bend = refractPower * (2 + (thickness / bevel * 0.5)) * look.Refraction * 30 * s;
                double sx = x + 0.5 + (slopeX * bend) - (px / Math.Max(halfW, 1) * look.Refraction * 4 * s * depth);
                double sy = y + 0.5 + (slopeY * bend) - (py / Math.Max(halfH, 1) * look.Refraction * 4 * s * depth);

                // Each colour bends a little differently.
                double split = look.ChromaticAberration * 18 * s * ((edge * 0.7) + 0.3) * 2;
                double dx = nx * split, dy = ny * split;
                double lift = (1 + look.Brightness) * (1 + (0.06 * depth));
                double r = picture.Sample(sx + dx, sy + dy, 2) * lift;
                double g = picture.Sample(sx, sy, 1) * lift;
                double b = picture.Sample(sx - dx, sy - dy, 0) * lift;

                // Reflection at grazing angles, highlights from four lights, a light rim and inner glow.
                double fresnel = Math.Pow(1 - Math.Abs(nz), 4) * look.Fresnel;
                double specular = (Light(nx, ny, nz, 0.4, -0.7, 1.0, 90, 1.0) + Light(nx, ny, nz, -0.3, 0.5, 1.0, 50, 0.3)
                    + Diffuse(nx, ny, nz, 0.1, -0.3, 1.0, 6, 0.1) + Light(nx, ny, nz, 0.0, -0.9, 0.4, 120, 0.6)) * look.Specular;
                double strokeWidth = 1.5 * s;
                double stroke = SmoothStep(-strokeWidth - s, -strokeWidth, sdf) * (1 - SmoothStep(-s, 0, sdf));
                stroke *= 0.4 + (0.6 * (0.5 + (0.5 * (-py / halfH))));   // brighter along the top
                double rim = edge * look.EdgeHighlight * 0.22;
                double glow = SmoothStep(5 * s, 0, inside) * look.EdgeHighlight * 0.15;
                double environment = ((-ny * 0.5) + 0.5) * fresnel * 0.08;
                double add = (specular + rim + glow + (stroke * look.EdgeHighlight * 0.55) + environment) * 255;
                double toWhite = fresnel * 0.2;

                result[i] = ToByte(Lerp(baseB, Lerp(b + add, 255, toWhite), mask));
                result[i + 1] = ToByte(Lerp(baseG, Lerp(g + add, 255, toWhite), mask));
                result[i + 2] = ToByte(Lerp(baseR, Lerp(r + add, 255, toWhite), mask));
            }
        }
        return result;
    }

    /// <summary>Signed distance to a rounded rectangle: negative inside, positive outside.</summary>
    public static double RoundedRect(double x, double y, double halfWidth, double halfHeight, double radius)
    {
        double qx = Math.Abs(x) - halfWidth + radius, qy = Math.Abs(y) - halfHeight + radius;
        double outside = Math.Sqrt((Math.Max(qx, 0) * Math.Max(qx, 0)) + (Math.Max(qy, 0) * Math.Max(qy, 0)));
        return Math.Min(Math.Max(qx, qy), 0) + outside - radius;
    }

    private static double Bevel(double inside, double depth) =>
        inside <= 0 ? 0 : inside >= depth ? depth : Math.Sqrt(inside * ((2 * depth) - inside));

    // Blinn-Phong with the viewer straight in front. Light directions are given with y pointing up, as in the shader.
    private static double Light(double nx, double ny, double nz, double lx, double ly, double lz, double shininess, double strength)
    {
        Normalise(ref lx, ref ly, ref lz);
        double hx = lx, hy = ly, hz = lz + 1;
        Normalise(ref hx, ref hy, ref hz);
        return Math.Pow(Math.Max((nx * hx) + (-ny * hy) + (nz * hz), 0), shininess) * strength;
    }

    private static double Diffuse(double nx, double ny, double nz, double lx, double ly, double lz, double power, double strength)
    {
        Normalise(ref lx, ref ly, ref lz);
        return Math.Pow(Math.Max((nx * lx) + (-ny * ly) + (nz * lz), 0), power) * strength;
    }

    private static void Normalise(ref double x, ref double y, ref double z)
    {
        double length = Math.Sqrt((x * x) + (y * y) + (z * z));
        x /= length;
        y /= length;
        z /= length;
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - (2 * t));
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    private readonly record struct Picture(byte[] Pixels, int Width, int Height)
    {
        /// <summary>Bilinear sample of one channel (0 blue, 1 green, 2 red), clamped to the picture's edges.</summary>
        public double Sample(double x, double y, int channel)
        {
            x = Math.Clamp(x - 0.5, 0, Width - 1);
            y = Math.Clamp(y - 0.5, 0, Height - 1);
            int ix = (int)x, iy = (int)y;
            int jx = Math.Min(ix + 1, Width - 1), jy = Math.Min(iy + 1, Height - 1);
            double fx = x - ix, fy = y - iy;
            double top = Lerp(Pixels[(((iy * Width) + ix) * 4) + channel], Pixels[(((iy * Width) + jx) * 4) + channel], fx);
            double bottom = Lerp(Pixels[(((jy * Width) + ix) * 4) + channel], Pixels[(((jy * Width) + jx) * 4) + channel], fx);
            return Lerp(top, bottom, fy);
        }
    }
}
