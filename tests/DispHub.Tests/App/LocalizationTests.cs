// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DispHub.Tests.App;

public sealed partial class LocalizationTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string LocalizationDir = Path.Combine(Root, "src", "DispHub", "Localization");

    [Fact]
    public void EveryKeyUsedInCodeExistsInEnglish()
    {
        HashSet<string> english = Keys(Path.Combine(LocalizationDir, "Strings.resx"));
        var missing = new SortedSet<string>(StringComparer.Ordinal);

        foreach (string file in Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".cs", StringComparison.Ordinal) && !file.EndsWith(".xaml", StringComparison.Ordinal))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;
            string text = File.ReadAllText(file);
            foreach (Match match in UsedKey().Matches(text))
            {
                string key = match.Groups["key"].Value;
                if (!english.Contains(key))
                    missing.Add($"{Path.GetFileName(file)}: {key}");
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryPreviewPictureHasANameAndAnImage()
    {
        HashSet<string> english = Keys(Path.Combine(LocalizationDir, "Strings.resx"));
        foreach (string picture in DispHub.Controls.ColorPreview.Pictures)
        {
            Assert.Contains($"Preview_{picture}", english);
            Assert.True(File.Exists(Path.Combine(Root, "src", "DispHub", "Assets", "Previews", picture + ".jpg")), picture);
        }
        // Home's own picture.
        Assert.True(File.Exists(Path.Combine(Root, "src", "DispHub", "Assets", "Previews", "home.jpg")));
    }

    [Fact]
    public void EnumValuesShownInTheUiHaveNames()
    {
        HashSet<string> english = Keys(Path.Combine(LocalizationDir, "Strings.resx"));
        foreach (Type type in new[] { typeof(DispHub.Core.Model.ThemePreference), typeof(DispHub.Core.Model.TrayClickAction), typeof(DispHub.Core.Model.WindowBackdrop), typeof(DispHub.Core.Model.OsdPosition) })
        {
            foreach (string value in Enum.GetNames(type))
                Assert.Contains($"Enum_{type.Name}_{value}", english);
        }
    }

    [Fact]
    public void Translations_OnlyUseKnownKeysAndKeepPlaceholders()
    {
        Dictionary<string, string> english = Values(Path.Combine(LocalizationDir, "Strings.resx"));
        foreach (string file in Directory.EnumerateFiles(LocalizationDir, "Strings.*.resx"))
        {
            foreach ((string key, string value) in Values(file))
            {
                Assert.True(english.ContainsKey(key), $"{Path.GetFileName(file)} has unknown key {key}");
                var expected = Placeholder().Matches(english[key]).Select(m => m.Value).Order().ToList();
                var actual = Placeholder().Matches(value).Select(m => m.Value).Order().ToList();
                Assert.True(expected.SequenceEqual(actual), $"{Path.GetFileName(file)} {key}: placeholders differ");
            }
        }
    }

    [Fact]
    public void TheLanguageListIsEnglishPlusEveryTranslationFile()
    {
        var files = Directory.EnumerateFiles(LocalizationDir, "Strings.*.resx")
            .Select(f => Path.GetFileNameWithoutExtension(f)["Strings.".Length..])
            .Order(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["en", .. files], DispHub.Localization.Loc.Languages);
        Assert.All(DispHub.Localization.Loc.Languages, l => System.Globalization.CultureInfo.GetCultureInfo(l));
    }

    private static HashSet<string> Keys(string path) => [.. Values(path).Keys];

    private static Dictionary<string, string> Values(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(e => (string)e.Attribute("name")!, e => (string?)e.Element("value") ?? "");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DispHub.slnx")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    [GeneratedRegex("""(?:Loc\.(?:Get|Format)\(\s*"|l:Tr\s+|\?\s*"|:\s*")(?<key>[A-Z][A-Za-z]+_[A-Za-z]+)\b""")]
    private static partial Regex UsedKey();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();
}
