// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Windows;
using DispHub.Controls;
using DispHub.Localization;
using DispHub.ViewModels;

namespace DispHub.Services;

/// <summary>A search result: a page, or a setting on one (by its title).</summary>
/// <param name="Where">The page it's on, or that it is a page.</param>
/// <param name="Setting">The setting's title on that page; null for the page itself.</param>
public sealed record SearchResult(string Title, string Where, string? Description, PageViewModel Page, string? Setting);

/// <summary>
/// What the title bar's search finds: every page, and every setting on them, by title and description. The index
/// is read from the pages themselves, so a new setting is found without a list to keep up to date.
/// </summary>
public static class SettingsSearch
{
    private const int MaxResults = 8;

    /// <summary>Builds the index once: each page's view is made off screen and its settings read.</summary>
    public static IReadOnlyList<SearchResult> Index(IEnumerable<PageViewModel> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var results = new List<SearchResult>();
        foreach (PageViewModel page in pages)
        {
            results.Add(new SearchResult(page.Title, Loc.Get("Search_Page"), null, page, null));
            if (Application.Current?.TryFindResource(new DataTemplateKey(page.GetType())) is not DataTemplate template ||
                template.LoadContent() is not DependencyObject view)
                continue;
            foreach (SettingsCard card in Cards(view))
            {
                if (!string.IsNullOrWhiteSpace(card.Header))
                    results.Add(new SearchResult(card.Header, page.Title, card.Description, page, card.Header));
            }
        }
        return results;
    }

    /// <summary>
    /// The best matches for <paramref name="query"/>: every word must appear in the title or description. Titles
    /// that start with the query come first, then other title matches, then description matches.
    /// </summary>
    public static IReadOnlyList<SearchResult> Find(IReadOnlyList<SearchResult> index, string? query)
    {
        ArgumentNullException.ThrowIfNull(index);
        string[] words = (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return [];
        CompareInfo compare = CultureInfo.CurrentCulture.CompareInfo;
        const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        bool Has(string? text, string word) => text is not null && compare.IndexOf(text, word, Loose) >= 0;

        return [.. index
            .Where(r => words.All(w => Has(r.Title, w) || Has(r.Description, w)))
            .OrderBy(r => compare.IsPrefix(r.Title, words[0], Loose) ? 0 : words.All(w => Has(r.Title, w)) ? 1 : 2)
            .ThenBy(r => r.Setting is null ? 0 : 1)
            .Take(MaxResults)];
    }

    private static IEnumerable<SettingsCard> Cards(DependencyObject parent)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is SettingsCard card)
                yield return card;
            if (child is DependencyObject element)
            {
                foreach (SettingsCard inner in Cards(element))
                    yield return inner;
            }
        }
    }
}
