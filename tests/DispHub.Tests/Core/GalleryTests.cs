using DispHub.Core.Color;
using DispHub.Core.Sharing;

namespace DispHub.Tests.Core;

public sealed class GalleryTests
{
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DispHub.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, relative);
    }

    [Fact]
    public void RepositoryGallery_EveryEntryIsValid()
    {
        string json = File.ReadAllText(RepoFile("presets/gallery.json"));

        var presets = GalleryFile.Parse(json);

        Assert.NotNull(presets);
        Assert.NotEmpty(presets);
        Assert.Equal(System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("presets").GetArrayLength(), presets.Count);
        Assert.All(presets, p =>
        {
            Assert.Equal(p.Entry.Name, p.Preset.Name);
            Assert.False(string.IsNullOrWhiteSpace(p.Entry.Description));
        });
    }

    [Fact]
    public void RepositoryGallery_WorksWithinWindowsDefaultLimits()
    {
        // Gallery presets should look the same for everyone, so none may need the extended gamma range.
        foreach ((GalleryEntry entry, SharedPreset preset) in GalleryFile.Parse(File.ReadAllText(RepoFile("presets/gallery.json")))!)
            Assert.True(RampLimits.Default.Accepts(RampBuilder.Build(preset.Color)), $"{entry.Name} exceeds Windows' default gamma limit");
    }

    [Fact]
    public void Parse_SkipsBadEntriesAndRejectsBadJson()
    {
        const string Json = """{ "presets": [ { "name": "Good", "description": "x", "link": "disphub://preset?name=Good&v=70" }, { "name": "Bad", "link": "https://evil" } ] }""";

        Assert.Single(GalleryFile.Parse(Json)!);
        Assert.Null(GalleryFile.Parse("not json"));
    }
}
