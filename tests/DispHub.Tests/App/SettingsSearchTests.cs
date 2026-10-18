using DispHub.Services;
using DispHub.ViewModels;
using Wpf.Ui.Controls;

namespace DispHub.Tests.App;

public sealed class SettingsSearchTests
{
    private static readonly PageViewModel Settings = new Page("Settings");
    private static readonly PageViewModel Effects = new Page("Effects");

    private static readonly IReadOnlyList<SearchResult> Index =
    [
        new("Settings", "Page", null, Settings, null),
        new("Start with Windows", "Settings", "Starts quietly in the notification area.", Settings, "Start with Windows"),
        new("Hotkeys", "Settings", "Switch profiles from any app.", Settings, "Hotkeys"),
        new("Frame rate", "Effects", "Lower uses less power; games always get every frame.", Effects, "Frame rate"),
        new("Effects", "Page", null, Effects, null),
    ];

    [Fact]
    public void Find_MatchesEveryWord_InTitleOrDescription()
    {
        Assert.Equal(["Start with Windows"], Titles("start windows"));
        Assert.Equal(["Frame rate"], Titles("power"));
        Assert.Empty(Titles("start effects"));
    }

    [Fact]
    public void Find_RanksTitleStartsThenTitlesThenDescriptions_PagesFirst()
    {
        Assert.Equal(["Settings", "Start with Windows", "Effects", "Hotkeys", "Frame rate"], Titles("s"));
    }

    [Fact]
    public void Find_IgnoresCaseAndAccents()
    {
        Assert.Equal(["Hotkeys"], Titles("HÖTKEY"));
    }

    [Fact]
    public void Find_NothingForAnEmptyQuery()
    {
        Assert.Empty(Titles("   "));
    }

    private static List<string> Titles(string query) => [.. SettingsSearch.Find(Index, query).Select(r => r.Title)];

    private sealed class Page(string title) : PageViewModel(title, SymbolRegular.Empty);
}
