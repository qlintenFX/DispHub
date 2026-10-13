using DispHub.Core.Effects;
using DispHub.Core.Sharing;

namespace DispHub.Tests.Core;

public sealed class SharedLookTests
{
    [Fact]
    public void EveryPreset_RoundTripsThroughALink()
    {
        foreach (LookPreset preset in Looks.All)
        {
            string link = new SharedLook(preset.Id, preset.Look, preset.Id).ToLink();

            Assert.True(SharedLook.TryParse(link, out SharedLook? parsed), link);
            Assert.Equal(preset.Look, parsed.Look);
            Assert.Equal(preset.Id, parsed.BasedOn);
        }
    }

    [Fact]
    public void ToLink_ListsOnlyChangedSettingsByName()
    {
        string link = new SharedLook("Holiday film", new Look { Grain = 0.4, Palette = RetroPalette.GameBoy }, "super8").ToLink();

        Assert.Equal("disphub://look?name=Holiday%20film&from=super8&grain=0.4&palette=gameBoy", link);
    }

    [Fact]
    public void TryParse_ClampsAndIgnoresJunk()
    {
        const string link = "disphub://look?name=%00Mine&grain=9&dim=NaN&palette=nope&colorVision=greenWeak&unknown=1&from=missing";

        Assert.True(SharedLook.TryParse(link, out SharedLook? shared));

        Assert.Equal("Mine", shared.Name);
        Assert.Equal(1, shared.Look.Grain);
        Assert.Equal(0, shared.Look.Dim);
        Assert.Equal(RetroPalette.None, shared.Look.Palette);
        Assert.Equal(ColorVision.GreenWeak, shared.Look.ColorVision);
        Assert.Null(shared.BasedOn);
    }

    [Theory]
    [InlineData("disphub://preset?name=Night&t=3400")]
    [InlineData("https://example.com/look?grain=1")]
    [InlineData("")]
    public void TryParse_RejectsOtherLinks(string text) => Assert.False(SharedLook.TryParse(text, out _));

    [Fact]
    public void TryParse_AcceptsALinkWithoutTheScheme()
    {
        Assert.True(SharedLook.TryParse("look?name=Dim&dim=0.6", out SharedLook? shared));
        Assert.Equal(0.6, shared.Look.Dim);
    }
}
