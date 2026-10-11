namespace DispHub.Core.Color;

/// <summary>
/// Converts a colour temperature to per-channel ramp multipliers.
/// Uses the Kim et al. (2002) cubic fit of the CIE 1931 Planckian locus, converts to linear sRGB,
/// normalises so 6500 K is exactly neutral, then encodes with the sRGB transfer curve because
/// gamma ramps operate on encoded values.
/// </summary>
public static class Blackbody
{
    private static readonly (double R, double G, double B) Reference = LinearRgb(ColorSettings.NeutralTemperatureK);

    public static (double R, double G, double B) Multipliers(int kelvin)
    {
        if (kelvin == ColorSettings.NeutralTemperatureK)
            return (1.0, 1.0, 1.0);

        double k = Math.Clamp(kelvin, ColorSettings.TemperatureMinK, ColorSettings.TemperatureMaxK);
        (double r, double g, double b) = LinearRgb(k);
        r /= Reference.R;
        g /= Reference.G;
        b /= Reference.B;

        double max = Math.Max(r, Math.Max(g, b));
        return (Encode(r / max), Encode(g / max), Encode(b / max));
    }

    private static (double R, double G, double B) LinearRgb(double kelvin)
    {
        double t = kelvin;
        double t2 = t * t;
        double t3 = t2 * t;

        double x = t <= 4000
            ? (-0.2661239e9 / t3) - (0.2343589e6 / t2) + (0.8776956e3 / t) + 0.179910
            : (-3.0258469e9 / t3) + (2.1070379e6 / t2) + (0.2226347e3 / t) + 0.240390;

        double x2 = x * x;
        double x3 = x2 * x;
        double y = t <= 2222
            ? (-1.1063814 * x3) - (1.34811020 * x2) + (2.18555832 * x) - 0.20219683
            : t <= 4000
                ? (-0.9549476 * x3) - (1.37418593 * x2) + (2.09137015 * x) - 0.16748867
                : (3.0817580 * x3) - (5.87338670 * x2) + (3.75112997 * x) - 0.37001483;

        // xyY (Y = 1) to XYZ to linear sRGB.
        double bigX = x / y;
        double bigZ = (1 - x - y) / y;
        double r = (3.2404542 * bigX) - 1.5371385 - (0.4985314 * bigZ);
        double g = (-0.9692660 * bigX) + 1.8760108 + (0.0415560 * bigZ);
        double b = (0.0556434 * bigX) - 0.2040259 + (1.0572252 * bigZ);
        return (Math.Max(r, 0), Math.Max(g, 0), Math.Max(b, 0));
    }

    private static double Encode(double linear) =>
        linear <= 0.0031308 ? 12.92 * linear : (1.055 * Math.Pow(linear, 1 / 2.4)) - 0.055;
}
