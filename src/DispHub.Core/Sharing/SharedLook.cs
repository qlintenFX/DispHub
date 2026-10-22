// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using DispHub.Core.Config;
using DispHub.Core.Effects;

namespace DispHub.Core.Sharing;

/// <summary>
/// A screen effect look shared as a link, e.g. <c>disphub://look?name=Holiday%20film&amp;from=super8&amp;grain=0.4</c>.
/// Like profile links it's readable: only settings that differ from a neutral look are listed, by name. Unknown
/// settings are ignored and values are clamped, so links from newer versions still import.
/// </summary>
public sealed record SharedLook(string Name, Look Look, string? BasedOn)
{
    private const string Host = "look";

    public string ToLink()
    {
        var link = new StringBuilder($"{SharedPreset.Scheme}://{Host}?name=");
        link.Append(Uri.EscapeDataString(Name));
        if (BasedOn is not null && Looks.Find(BasedOn) is not null)
            link.Append("&from=").Append(Uri.EscapeDataString(BasedOn));
        Look look = Look.Clamp();
        foreach (LookParameter parameter in LookParameters.Changed(look))
        {
            double value = parameter.Get(look);
            string text = parameter.Key switch
            {
                "palette" => Camel(((RetroPalette)(int)value).ToString()),
                "colorVision" => Camel(((ColorVision)(int)value).ToString()),
                _ => value.ToString("0.###", CultureInfo.InvariantCulture),
            };
            link.Append('&').Append(parameter.Key).Append('=').Append(text);
        }
        return link.ToString();
    }

    public static bool TryParse(string? text, [NotNullWhen(true)] out SharedLook? shared)
    {
        shared = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string trimmed = text.Trim();
        if (!trimmed.StartsWith(SharedPreset.Scheme + "://", StringComparison.OrdinalIgnoreCase))
            trimmed = $"{SharedPreset.Scheme}://{trimmed.TrimStart('/')}";
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Look look = Look.Neutral;
        string? name = null;
        string? basedOn = null;
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
                continue;
            string key = pair[..equals];
            string value = Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
            if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                name = value;
            else if (key.Equals("from", StringComparison.OrdinalIgnoreCase))
                basedOn = Looks.Find(value)?.Id;
            else if (LookParameters.Find(key) is { } parameter && Parse(parameter, value) is { } number)
                look = parameter.Set(look, number);
        }
        shared = new SharedLook(ConfigNormalizer.CleanName(name, "Shared look"), look.Clamp(), basedOn);
        return true;
    }

    private static double? Parse(LookParameter parameter, string value)
    {
        switch (parameter.Key)
        {
            case "palette":
                return Enum.TryParse(value, ignoreCase: true, out RetroPalette palette) && Enum.IsDefined(palette) ? (int)palette : null;
            case "colorVision":
                return Enum.TryParse(value, ignoreCase: true, out ColorVision vision) && Enum.IsDefined(vision) ? (int)vision : null;
            default:
                return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number)
                    ? number
                    : null;
        }
    }

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
