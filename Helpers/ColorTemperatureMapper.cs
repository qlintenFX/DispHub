using DisplayHub.Constants;

namespace DisplayHub.Helpers;

public static class ColorTemperatureMapper
{
    /// <summary>
    /// Convert color temperature value (0-100) to RGB multipliers.
    /// 0 = warmest, 50 = neutral white point (6500K), 100 = coolest.
    /// </summary>
    public static (double r, double g, double b) GetMultipliers(int temperature)
    {
        temperature = Math.Clamp(temperature, AppConstants.ColorTempMin, AppConstants.ColorTempMax);

        double kelvin = GetKelvinFromSlider(temperature);
        var (r, g, b) = KelvinToRgb(kelvin);
        var (nr, ng, nb) = KelvinToRgb(AppConstants.ColorTempNeutralKelvin);

        r /= nr > 0 ? nr : 1;
        g /= ng > 0 ? ng : 1;
        b /= nb > 0 ? nb : 1;

        double max = Math.Max(r, Math.Max(g, b));
        if (max > 0)
        {
            r /= max;
            g /= max;
            b /= max;
        }

        return (Math.Clamp(r, 0, 1), Math.Clamp(g, 0, 1), Math.Clamp(b, 0, 1));
    }

    public static double GetKelvinFromSlider(int temperature)
    {
        temperature = Math.Clamp(temperature, AppConstants.ColorTempMin, AppConstants.ColorTempMax);

        return temperature <= AppConstants.ColorTempNeutralValue
            ? AppConstants.ColorTempMinKelvin +
              (temperature / (double)AppConstants.ColorTempNeutralValue) *
              (AppConstants.ColorTempNeutralKelvin - AppConstants.ColorTempMinKelvin)
            : AppConstants.ColorTempNeutralKelvin +
              ((temperature - AppConstants.ColorTempNeutralValue) /
               (double)(AppConstants.ColorTempMax - AppConstants.ColorTempNeutralValue)) *
              (AppConstants.ColorTempMaxKelvin - AppConstants.ColorTempNeutralKelvin);
    }

    private static (double r, double g, double b) KelvinToRgb(double kelvin)
    {
        double r, g, b;
        double temp = kelvin / 100.0;

        if (temp <= 66)
            r = 1.0;
        else
            r = Math.Clamp(329.698727446 * Math.Pow(temp - 60, -0.1332047592) / 255.0, 0, 1);

        if (temp <= 66)
            g = Math.Clamp((99.4708025861 * Math.Log(temp) - 161.1195681661) / 255.0, 0, 1);
        else
            g = Math.Clamp(288.1221695283 * Math.Pow(temp - 60, -0.0755148492) / 255.0, 0, 1);

        if (temp >= 66)
            b = 1.0;
        else if (temp <= 19)
            b = 0.0;
        else
            b = Math.Clamp((138.5177312231 * Math.Log(temp - 10) - 305.0447927307) / 255.0, 0, 1);

        return (r, g, b);
    }
}
