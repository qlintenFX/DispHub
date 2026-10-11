using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using DispHub.Core.Color;
using DispHub.Core.Config;

namespace DispHub.Core.Sharing;

/// <summary>
/// A profile shared as a link, e.g. <c>disphub://preset?name=CS2%20Vivid&amp;b=5&amp;c=5&amp;g=1.15&amp;t=6500&amp;v=70</c>.
/// Readable on purpose, so people can see what a link does before clicking it. Unknown parameters are ignored and
/// values are clamped, so links from newer versions still import.
/// </summary>
public sealed record SharedPreset(string Name, ColorSettings Color)
{
    public const string Scheme = "disphub";
    private const string Host = "preset";

    public string ToLink()
    {
        var link = new StringBuilder($"{Scheme}://{Host}?name=");
        link.Append(Uri.EscapeDataString(Name));
        ColorSettings c = Color.Clamp();
        if (c.Brightness != 0)
            link.Append(CultureInfo.InvariantCulture, $"&b={c.Brightness}");
        if (c.Contrast != 0)
            link.Append(CultureInfo.InvariantCulture, $"&c={c.Contrast}");
        if (Math.Abs(c.Gamma - 1) > 0.001)
            link.Append(CultureInfo.InvariantCulture, $"&g={c.Gamma:0.##}");
        if (c.TemperatureK != ColorSettings.NeutralTemperatureK)
            link.Append(CultureInfo.InvariantCulture, $"&t={c.TemperatureK}");
        if (c.Vibrance != ColorSettings.NeutralVibrance)
            link.Append(CultureInfo.InvariantCulture, $"&v={c.Vibrance}");
        return link.ToString();
    }

    /// <summary>Parses a share link. Accepts surrounding whitespace and links pasted with or without the scheme.</summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out SharedPreset? preset)
    {
        preset = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.Trim();
        if (!trimmed.StartsWith(Scheme + "://", StringComparison.OrdinalIgnoreCase))
            trimmed = $"{Scheme}://{trimmed.TrimStart('/')}";
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
                continue;
            query[pair[..equals]] = Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
        }

        string name = CleanName(query.GetValueOrDefault("name"));
        var color = new ColorSettings(
            Int(query, "b", 0),
            Int(query, "c", 0),
            Double(query, "g", 1.0),
            Int(query, "t", ColorSettings.NeutralTemperatureK),
            Int(query, "v", ColorSettings.NeutralVibrance));
        preset = new SharedPreset(name, color.Clamp());
        return true;
    }

    private static string CleanName(string? name)
    {
        string cleaned = new((name ?? "").Where(ch => !char.IsControl(ch)).ToArray());
        cleaned = cleaned.Trim();
        if (cleaned.Length == 0)
            return "Shared preset";
        return cleaned.Length > ConfigNormalizer.MaxNameLength ? cleaned[..ConfigNormalizer.MaxNameLength] : cleaned;
    }

    private static int Int(Dictionary<string, string> query, string key, int fallback) =>
        query.TryGetValue(key, out string? value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : fallback;

    private static double Double(Dictionary<string, string> query, string key, double fallback) =>
        query.TryGetValue(key, out string? value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : fallback;
}
