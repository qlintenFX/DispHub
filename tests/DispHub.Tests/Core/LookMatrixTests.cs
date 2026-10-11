using DispHub.Core.Config;
using DispHub.Core.Effects;
using DispHub.Core.Model;

namespace DispHub.Tests.Core;

public sealed class LookMatrixTests
{
    [Fact]
    public void Neutral_IsTheIdentity()
    {
        double[,] m = LookMatrix.Build(new LookSettings());

        for (int i = 0; i < LookMatrix.Size; i++)
            for (int j = 0; j < LookMatrix.Size; j++)
                Assert.Equal(i == j ? 1 : 0, m[i, j], 9);
    }

    [Fact]
    public void NoSaturation_GivesGreyOfTheSameBrightness()
    {
        double[,] m = LookMatrix.Build(new LookSettings(Saturation: 0));

        (double r, double g, double b) = LookMatrix.Apply(m, 0.9, 0.3, 0.1);

        Assert.Equal(r, g, 9);
        Assert.Equal(g, b, 9);
        Assert.Equal((0.2126 * 0.9) + (0.7152 * 0.3) + (0.0722 * 0.1), r, 9);
    }

    [Fact]
    public void Fade_LiftsBlackAndKeepsWhite()
    {
        double[,] m = LookMatrix.Build(new LookSettings(Fade: 0.1));

        Assert.Equal((0.1, 0.1, 0.1), Round(LookMatrix.Apply(m, 0, 0, 0)));
        Assert.Equal((1.0, 1.0, 1.0), Round(LookMatrix.Apply(m, 1, 1, 1)));
    }

    [Fact]
    public void Sepia_KeepsWhiteBrightAndTonesItWarm()
    {
        double[,] m = LookMatrix.Build(new LookSettings(Sepia: 1));

        (double r, double g, double b) = LookMatrix.Apply(m, 1, 1, 1);

        Assert.Equal(1, r, 6);
        Assert.True(r > g && g > b);
        Assert.Equal((0.0, 0.0, 0.0), Round(LookMatrix.Apply(m, 0, 0, 0)));
    }

    [Fact]
    public void FullGreenTint_IsAGreenMonochrome()
    {
        double[,] m = LookMatrix.Build(new LookSettings(Saturation: 0, TintHue: 120, TintStrength: 1));

        (double r, double g, double b) = LookMatrix.Apply(m, 0.4, 0.4, 0.4);

        Assert.Equal(0, r, 9);
        Assert.Equal(0, b, 9);
        Assert.True(g > 0.4);
    }

    [Fact]
    public void Contrast_PivotsAroundMidGrey()
    {
        double[,] m = LookMatrix.Build(new LookSettings(Contrast: 1.4));

        Assert.Equal((0.5, 0.5, 0.5), Round(LookMatrix.Apply(m, 0.5, 0.5, 0.5)));
        Assert.True(LookMatrix.Apply(m, 0.7, 0.7, 0.7).R > 0.7);
    }

    [Fact]
    public void ToFloats_IsRowMajor()
    {
        double[,] m = LookMatrix.Build(new LookSettings(Fade: 0.2));

        float[] floats = LookMatrix.ToFloats(m);

        Assert.Equal(25, floats.Length);
        Assert.Equal(0.2f, floats[(4 * 5) + 0], 6);
        Assert.Equal(0.8f, floats[0], 6);
    }

    [Fact]
    public void Presets_AreWithinRangeAndUnique()
    {
        Assert.Equal(EffectPresets.All.Count, EffectPresets.All.Select(p => p.Id).Distinct().Count());
        Assert.All(EffectPresets.All, p =>
        {
            Assert.Equal(p.Look, p.Look.Clamp());
            Assert.Equal(p.Overlay, p.Overlay.Clamp());
        });
    }

    [Fact]
    public void EffectsConfig_RoundTrips()
    {
        AppConfig config = DefaultConfig.Create();
        config.Effects = new EffectsConfig
        {
            Enabled = true,
            Preset = "film",
            Look = new LookSettings(Saturation: 0.5, Sepia: 0.3),
            Overlay = new OverlaySettings(Grain: 0.4, LetterboxAspect: 2.39),
        };

        string json = ConfigStore.Serialize(config);
        AppConfig loaded = ConfigStore.Deserialize(json);

        Assert.DoesNotContain("isNeutral", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isAnimated", json, StringComparison.Ordinal);

        Assert.Equal(config.Effects.Look, loaded.Effects.Look);
        Assert.Equal(config.Effects.Overlay, loaded.Effects.Overlay);
        Assert.True(loaded.Effects.Enabled);
    }

    private static (double, double, double) Round((double R, double G, double B) c) =>
        (Math.Round(c.R, 6), Math.Round(c.G, 6), Math.Round(c.B, 6));
}
