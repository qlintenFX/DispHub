// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using DispHub.Core.Color;
using DispHub.Core.Config;

namespace DispHub.Core.Sharing;

/// <summary>
/// A profile shared as a link, e.g. <c>https://getdisphub.com/p/?name=CS2%20Vivid&amp;b=5&amp;g=1.15&amp;v=70</c>.
/// The web page shows the profile and opens it in DispHub through <c>disphub://preset?...</c> with the same
/// parameters, or offers the Store to people who don't have DispHub yet, so a link works anywhere it's posted.
/// Readable on purpose, so people can see what a link does before clicking it. Unknown parameters are ignored and
/// values are clamped, so links from newer versions still import.
/// </summary>
public sealed record SharedPreset(string Name, ColorSettings Color)
{
    public const string Scheme = "disphub";
    public const string WebPage = "https://getdisphub.com/p/";
    private const string Host = "preset";
    private static readonly string[] WebHosts = ["getdisphub.com", "www.getdisphub.com"];

    /// <summary>The link to share: a web page, so it can be clicked where <c>disphub://</c> links can't.</summary>
    public string ToLink() => WebPage + "?" + Query();

    private string Query()
    {
        var link = new StringBuilder("name=");
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
        if (c.ShadowBoost != 0)
            link.Append(CultureInfo.InvariantCulture, $"&s={c.ShadowBoost}");
        return link.ToString();
    }

    /// <summary>
    /// Parses a share link: the web page's or DispHub's own. Accepts surrounding whitespace and links pasted with or
    /// without the scheme.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out SharedPreset? preset)
    {
        preset = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.Trim();
        if (WebQuery(trimmed) is { } webQuery)
            trimmed = $"{Scheme}://{Host}?{webQuery}";
        else if (!trimmed.StartsWith(Scheme + "://", StringComparison.OrdinalIgnoreCase))
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
            Int(query, "v", ColorSettings.NeutralVibrance),
            Int(query, "s", 0));
        preset = new SharedPreset(name, color.Clamp());
        return true;
    }

    /// <summary>The query of a link to the web page (<see cref="WebPage"/>), or null for any other text.</summary>
    private static string? WebQuery(string text)
    {
        if (!text.Contains("://", StringComparison.Ordinal) &&
            WebHosts.Any(host => text.StartsWith(host + "/", StringComparison.OrdinalIgnoreCase)))
        {
            text = "https://" + text;
        }
        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !WebHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(uri.AbsolutePath.TrimEnd('/'), "/p", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return uri.Query.TrimStart('?');
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
