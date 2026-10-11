using DispHub.Core.Color;
using DispHub.Core.Sharing;

namespace DispHub.Tests.Core;

public sealed class SharedPresetTests
{
    [Fact]
    public void ToLink_ThenParse_RoundTrips()
    {
        var preset = new SharedPreset("CS2 Vivid & bright", new ColorSettings(5, -3, 1.15, 5800, 80));

        Assert.True(SharedPreset.TryParse(preset.ToLink(), out SharedPreset? parsed));

        Assert.Equal(preset, parsed);
    }

    [Fact]
    public void ToLink_OmitsNeutralValues()
    {
        string link = new SharedPreset("Vivid", new ColorSettings(Vibrance: 70)).ToLink();

        Assert.Equal("disphub://preset?name=Vivid&v=70", link);
    }

    [Theory]
    [InlineData("  disphub://preset?name=Night&t=3400  ")]
    [InlineData("preset?name=Night&t=3400")]
    [InlineData("DISPHUB://PRESET?name=Night&t=3400")]
    public void TryParse_AcceptsCommonPastes(string text)
    {
        Assert.True(SharedPreset.TryParse(text, out SharedPreset? preset));
        Assert.Equal("Night", preset.Name);
        Assert.Equal(3400, preset.Color.TemperatureK);
    }

    [Fact]
    public void TryParse_ClampsAndIgnoresJunk()
    {
        Assert.True(SharedPreset.TryParse("disphub://preset?name=%00%01Hack&b=999&g=abc&x=1&t=-5", out SharedPreset? preset));

        Assert.Equal("Hack", preset.Name);
        Assert.Equal(ColorSettings.BrightnessMax, preset.Color.Brightness);
        Assert.Equal(1.0, preset.Color.Gamma);
        Assert.Equal(ColorSettings.TemperatureMinK, preset.Color.TemperatureK);
    }

    [Fact]
    public void TryParse_LongName_IsTruncated()
    {
        Assert.True(SharedPreset.TryParse("disphub://preset?name=" + new string('a', 200), out SharedPreset? preset));

        Assert.Equal(40, preset.Name.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/?name=x")]
    [InlineData("disphub://settings?name=x")]
    public void TryParse_RejectsOtherLinks(string? text) => Assert.False(SharedPreset.TryParse(text, out _));
}
