using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace DispHub.Localization;

/// <summary>
/// All user-visible text comes from Strings.resx (English) and its translations (Strings.de.resx, ...).
/// Missing translations fall back to English automatically.
/// </summary>
public static class Loc
{
    /// <summary>
    /// Languages DispHub ships, as culture names: English (the neutral resource) and every Strings.&lt;culture&gt;.resx,
    /// listed by the build. To add one, see docs/translating.md.
    /// </summary>
    public static readonly IReadOnlyList<string> Languages =
    [
        "en",
        .. (typeof(Loc).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
            .Cast<System.Reflection.AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "DispHub.Languages")?.Value ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

    private static readonly ResourceManager Resources = new("DispHub.Localization.Strings", typeof(Loc).Assembly);

    /// <summary>
    /// The UI language. Kept here explicitly rather than read from the thread: WPF runs parts of page creation in
    /// dispatcher work with its own culture, which made pages fall back to English.
    /// </summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.CurrentUICulture;

    public static string Get(string key) => Resources.GetString(key, Culture) ?? key;

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>
    /// Picks the UI language: the user's choice in DispHub, otherwise the language Windows chose for this app
    /// (which includes the per-app language setting for the Store version), otherwise the Windows display language.
    /// </summary>
    public static void Apply(string? preferred)
    {
        CultureInfo culture = Resolve(preferred) ?? Resolve(Platform.PackageInfo.PreferredLanguage) ?? CultureInfo.CurrentUICulture;
        Culture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }

    /// <summary>The language's own name, e.g. "Deutsch" for German.</summary>
    public static string NativeName(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language);
        string name = culture.NativeName;
        return name.Length > 0 ? char.ToUpper(name[0], culture) + name[1..] : language;
    }

    private static CultureInfo? Resolve(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}

/// <summary>XAML: <c>Text="{l:Tr Settings_Title}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
