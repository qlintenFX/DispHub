using DispHub.Core.Config;
using DispHub.Core.Effects;
using DispHub.Core.Licensing;
using DispHub.Core.Model;

namespace DispHub.Tests.Core;

public sealed class LookTests
{
    [Fact]
    public void Neutral_ChangesNothing()
    {
        Look look = Look.Neutral;

        Assert.True(look.IsEmpty);
        Assert.False(look.NeedsCapture);
        Assert.False(look.IsAnimated);
        Assert.Empty(LookParameters.Changed(look));
    }

    [Fact]
    public void LayersOnly_DontNeedCapture()
    {
        var look = new Look { Grain = 0.3, Vignette = 0.4, Scanlines = 0.2, Letterbox = 2.39, Dim = 0.5, Flicker = 0.1 };

        Assert.False(look.NeedsCapture);
        Assert.False(look.IsEmpty);
    }

    [Theory]
    [InlineData(nameof(Look.Saturation))]
    [InlineData(nameof(Look.Bloom))]
    [InlineData(nameof(Look.Curvature))]
    [InlineData(nameof(Look.Palette))]
    [InlineData(nameof(Look.ColorVision))]
    public void PictureChanges_NeedCapture(string property)
    {
        Look look = property switch
        {
            nameof(Look.Saturation) => new Look { Saturation = 0 },
            nameof(Look.Bloom) => new Look { Bloom = 0.3 },
            nameof(Look.Curvature) => new Look { Curvature = 0.3 },
            nameof(Look.Palette) => new Look { Palette = RetroPalette.GameBoy },
            _ => new Look { ColorVision = ColorVision.GreenWeak },
        };

        Assert.True(look.NeedsCapture);
    }

    [Fact]
    public void IsAnimated_FollowsTheGrainSpeed()
    {
        Assert.True(new Look { Grain = 0.3 }.IsAnimated);
        Assert.False(new Look { Grain = 0.3, GrainFps = 0 }.IsAnimated);
        Assert.False(new Look { Vignette = 0.3 }.IsAnimated);
    }

    [Fact]
    public void Clamp_KeepsValuesInRange()
    {
        Look clamped = new Look
        {
            Exposure = 9,
            Contrast = 0,
            Saturation = -1,
            Fade = 1,
            ShadowHue = -30,
            HighlightHue = 400,
            Grain = 5,
            GrainFps = 500,
            Letterbox = 0.5,
            Pixelate = 1,
            Posterize = 100,
            Dither = 3,
            Dim = 1,
            Palette = (RetroPalette)99,
            ColorVision = (ColorVision)42,
        }.Clamp();

        Assert.Equal(2, clamped.Exposure);
        Assert.Equal(0.5, clamped.Contrast);
        Assert.Equal(0, clamped.Saturation);
        Assert.Equal(0.3, clamped.Fade);
        Assert.Equal(330, clamped.ShadowHue);
        Assert.Equal(40, clamped.HighlightHue);
        Assert.Equal(1, clamped.Grain);
        Assert.Equal(60, clamped.GrainFps);
        Assert.Equal(1.2, clamped.Letterbox);
        Assert.Equal(0, clamped.Pixelate);
        Assert.Equal(32, clamped.Posterize);
        Assert.Equal(1, clamped.Dither);
        Assert.Equal(0.95, clamped.Dim);
        Assert.Equal(RetroPalette.None, clamped.Palette);
        Assert.Equal(ColorVision.Typical, clamped.ColorVision);
    }

    [Fact]
    public void Presets_HaveUniqueIdsAndInRangeValuesThatDoSomething()
    {
        Assert.Equal(Looks.All.Count, Looks.All.Select(l => l.Id).Distinct().Count());
        foreach (LookPreset preset in Looks.All)
        {
            Assert.Equal(preset.Look, preset.Look.Clamp());
            Assert.False(preset.Look.IsEmpty, preset.Id);
            Assert.Same(preset, Looks.Find(preset.Id));
        }
        Assert.Contains(Looks.All, l => l.Category == LookCategory.Comfort);
        Assert.Contains(Looks.All, l => l.Category == LookCategory.Access);
    }

    [Fact]
    public void Parameters_CoverEveryLookSettingOnceAndRoundTrip()
    {
        Assert.Equal(LookParameters.All.Count, LookParameters.All.Select(p => p.Key).Distinct().Count());
        // Every settable property of a look has exactly one parameter.
        int properties = typeof(Look).GetProperties().Count(p => p.CanWrite);
        Assert.Equal(properties, LookParameters.All.Count);

        foreach (LookParameter parameter in LookParameters.All)
        {
            Assert.Equal(parameter.Default, parameter.Get(Look.Neutral));
            double value = parameter.Options is { Count: > 1 } options ? options[^1]
                : parameter.Max != parameter.Default ? parameter.Max : parameter.Min;
            Look changed = parameter.Set(Look.Neutral, value);
            Assert.Equal(value, parameter.Get(changed), 6);
            Assert.Contains(parameter, LookParameters.Changed(changed));
            Assert.Equal(parameter.Default, parameter.Get(parameter.Set(changed, parameter.Default)), 6);
        }
    }

    [Fact]
    public void Parameters_SetClampsOutOfRangeValues()
    {
        LookParameter grain = LookParameters.Find("grain")!;

        Assert.Equal(1, grain.Get(grain.Set(Look.Neutral, 7)));
    }

    [Fact]
    public void DependentSettings_ApplyOnlyWhenTheirParentIsOn()
    {
        LookParameter size = LookParameters.Find("grainSize")!;

        Assert.False(size.Applies(Look.Neutral));
        Assert.True(size.Applies(new Look { Grain = 0.2 }));
    }

    [Fact]
    public void Tint_IsNeutralAtZeroAndKeepsBrightness()
    {
        Assert.Equal((1.0, 1.0, 1.0), LookColour.Tint(200, 0));
        foreach (double hue in new[] { 0.0, 60, 120, 195, 240, 300 })
        {
            (double r, double g, double b) = LookColour.Tint(hue, 0.5);
            Assert.Equal(1, (0.2126 * r) + (0.7152 * g) + (0.0722 * b), 6);
        }
    }

    [Fact]
    public void Palettes_FitTheShaderAndRampsRunDarkToLight()
    {
        foreach (RetroPalette palette in Enum.GetValues<RetroPalette>().Where(p => p != RetroPalette.None))
        {
            PaletteInfo info = Palettes.Get(palette)!;
            Assert.InRange(info.Colours.Count, 2, Palettes.MaxColours);
            if (!info.ByBrightness)
                continue;
            var lumas = info.Colours.Select(c => (0.2126 * ((c >> 16) & 0xFF)) + (0.7152 * ((c >> 8) & 0xFF)) + (0.0722 * (c & 0xFF))).ToList();
            Assert.Equal(lumas.Order(), lumas);
        }
        Assert.Null(Palettes.Get(RetroPalette.None));
    }

    [Fact]
    public void Effective_ProfileChoiceWinsOverTheEffectsPage()
    {
        var effects = new EffectsConfig { Enabled = true, Preset = "film", Look = Looks.Find("film")!.Look };
        var night = new Profile { Effect = "redNight" };
        var none = new Profile { Effect = LookLibrary.None };
        var keep = new Profile();

        Assert.Equal(("redNight", Looks.Find("redNight")!.Look), Swap(LookLibrary.Effective(effects, night, profilesOff: false)));
        Assert.Equal((null, null), Swap(LookLibrary.Effective(effects, none, profilesOff: false)));
        Assert.Equal(("film", effects.Look), Swap(LookLibrary.Effective(effects, keep, profilesOff: false)));
        Assert.Equal(("film", effects.Look), Swap(LookLibrary.Effective(effects, night, profilesOff: true))); // profiles off: the page's look

        effects.Enabled = false;
        Assert.Equal((null, null), Swap(LookLibrary.Effective(effects, keep, profilesOff: false)));
        Assert.Equal("redNight", LookLibrary.Effective(effects, night, profilesOff: false).Id);
    }

    [Fact]
    public void SavedLooks_AreFoundByKeyAndRememberTheirOrigin()
    {
        var saved = new SavedLook { Name = "Mine", Look = new Look { Grain = 0.7 }, BasedOn = "vhs" };
        var effects = new EffectsConfig { SavedLooks = [saved] };

        Assert.Equal(saved.Look, LookLibrary.Find(effects, saved.Key));
        Assert.Equal("vhs", LookLibrary.Origin(effects, saved.Key)?.Id);
        Assert.Null(LookLibrary.Find(effects, "saved:missing"));
    }

    [Fact]
    public void ScreenEffects_ArePro()
    {
        var free = new FeatureGate(IsPro: false);
        var pro = new FeatureGate(IsPro: true);

        Assert.False(free.ScreenEffects);
        Assert.False(free.SavedLooks);
        Assert.True(pro.ScreenEffects);
        Assert.True(pro.SavedLooks);
    }

    [Fact]
    public void Strength_ScalesAmountsButKeepsShapesAndChoices()
    {
        Look gameBoy = Looks.Find("gameBoy")!.Look with { Grain = 0.4, GrainSize = 2.5 };

        Look half = LookParameters.WithStrength(gameBoy, 0.5);

        Assert.Equal(0.2, half.Grain, 6);
        Assert.Equal(2.5, half.GrainSize);
        Assert.Equal(RetroPalette.GameBoy, half.Palette);
        Assert.Equal(1 + ((gameBoy.Contrast - 1) * 0.5), half.Contrast, 6);
        Assert.Same(gameBoy, LookParameters.WithStrength(gameBoy, 1));
        Assert.Equal(Look.Neutral, LookParameters.WithStrength(gameBoy, 0));
    }

    [Fact]
    public void Strength_IsWhatTheEffectsPageShows()
    {
        var effects = new EffectsConfig { Enabled = true, Look = new Look { Grain = 0.6 }, Strength = 0.5 };

        Assert.Equal(0.3, LookLibrary.Effective(effects, null, profilesOff: false).Look!.Grain, 6);
    }

    [Fact]
    public void Parts_CoverEverySettingOutsideTheBasics()
    {
        var covered = LookParts.Basics.Select(p => p.Key)
            .Concat(LookParts.All.Select(p => p.Main.Key))
            .Concat(LookParts.All.SelectMany(p => p.Details).Select(p => p.Key))
            .ToHashSet();

        Assert.All(LookParameters.All, p => Assert.Contains(p.Key, covered));
        Assert.Equal(LookParts.All.Count, LookParts.All.Select(p => p.Key).Distinct().Count());
    }

    [Fact]
    public void Parts_AddAndRemoveCleanly()
    {
        foreach (LookPart part in LookParts.All)
        {
            Look added = part.Add(Look.Neutral);
            Assert.True(part.IsIn(added), part.Key);
            Assert.Equal([part], LookParts.In(added));
            Assert.Equal(Look.Neutral, part.Remove(added));
        }
    }

    [Fact]
    public void Parts_KeepSharedDetailsWhileAnotherPartUsesThem()
    {
        Look look = new Look { Grain = 0.3, Flicker = 0.2, GrainFps = 12 };

        Look withoutGrain = LookParts.Find("grain")!.Remove(look);

        Assert.Equal(0, withoutGrain.Grain);
        Assert.Equal(12, withoutGrain.GrainFps);
        Assert.Equal(Look.Neutral.GrainFps, LookParts.Find("flicker")!.Remove(withoutGrain).GrainFps);
    }


    [Fact]
    public void Normalizer_RepairsEffects()
    {
        AppConfig config = DefaultConfig.Create();
        Guid id = Guid.NewGuid();
        config.Effects = new EffectsConfig
        {
            Look = new Look { Grain = 3 },
            Preset = "gone",
            MaxFps = 9999,
            SavedLooks =
            [
                new SavedLook { Id = id, Name = "  ", Look = new Look { Dim = 4 }, BasedOn = "nope" },
                new SavedLook { Id = id, Name = new string('x', 80) },
            ],
        };
        config.Effects.Strength = double.NaN;
        config.Ui.Backdrop = (WindowBackdrop)42;
        config.Profiles[0].Effect = "saved:missing";
        config.Profiles[1].Effect = LookLibrary.None;

        ConfigNormalizer.Normalize(config);

        Assert.Equal(1, config.Effects.Strength);
        Assert.Equal(WindowBackdrop.Mica, config.Ui.Backdrop);

        EffectsConfig effects = config.Effects;
        Assert.Equal(1, effects.Look.Grain);
        Assert.Null(effects.Preset);
        Assert.Equal(240, effects.MaxFps);
        Assert.Equal("Look", effects.SavedLooks[0].Name);
        Assert.Equal(0.95, effects.SavedLooks[0].Look.Dim);
        Assert.Null(effects.SavedLooks[0].BasedOn);
        Assert.NotEqual(effects.SavedLooks[0].Id, effects.SavedLooks[1].Id);
        Assert.Equal(ConfigNormalizer.MaxNameLength, effects.SavedLooks[1].Name.Length);
        Assert.Null(config.Profiles[0].Effect);
        Assert.Equal(LookLibrary.None, config.Profiles[1].Effect);
    }

    [Fact]
    public void Config_SavesAndLoadsLooks()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(temp.Path);
        AppConfig config = DefaultConfig.Create();
        var saved = new SavedLook { Name = "Mine", Look = Looks.Find("gameBoy")!.Look, BasedOn = "gameBoy" };
        config.Effects = new EffectsConfig { Enabled = true, Preset = saved.Key, Look = saved.Look, SavedLooks = [saved] };
        config.Profiles[0].Effect = saved.Key;

        store.Save(config);
        AppConfig loaded = store.Load().Config;

        Assert.True(loaded.Effects.Enabled);
        Assert.Equal(saved.Look, loaded.Effects.Look);
        Assert.Equal(saved.Key, loaded.Effects.Preset);
        Assert.Equal(RetroPalette.GameBoy, loaded.Effects.SavedLooks[0].Look.Palette);
        Assert.Equal(saved.Key, loaded.Profiles[0].Effect);
    }

    private static (string? Id, Look? Look) Swap((Look? Look, string? Id) value) => (value.Id, value.Look);
}
