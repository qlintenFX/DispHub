// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Color;

namespace DispHub.Tests.Core;

public sealed class RampBuilderTests
{
    public static TheoryData<int, int, double, int> Grid()
    {
        var data = new TheoryData<int, int, double, int>();
        foreach (int brightness in new[] { -50, -20, 0, 25, 50 })
        {
            foreach (int contrast in new[] { -50, 0, 50 })
            {
                foreach (double gamma in new[] { 0.3, 0.8, 1.0, 1.6, 2.8 })
                {
                    foreach (int kelvin in new[] { 2000, 3400, 6500, 10000 })
                        data.Add(brightness, contrast, gamma, kelvin);
                }
            }
        }
        return data;
    }

    [Fact]
    public void Build_NeutralSettings_ReturnsExactIdentity()
    {
        GammaRamp ramp = RampBuilder.Build(ColorSettings.Neutral);

        for (int c = 0; c < 3; c++)
        {
            for (int i = 0; i < GammaRamp.Size; i++)
                Assert.Equal(i * 257, ramp.Channel(c)[i]);
        }
    }

    [Theory]
    [MemberData(nameof(Grid))]
    public void Build_AnySettings_IsMonotonicAndNotFlat(int brightness, int contrast, double gamma, int kelvin)
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(brightness, contrast, gamma, kelvin));

        for (int c = 0; c < 3; c++)
        {
            ReadOnlySpan<ushort> channel = ramp.Channel(c);
            for (int i = 1; i < channel.Length; i++)
                Assert.True(channel[i] >= channel[i - 1], $"channel {c} decreases at {i}");

            // Blue is legitimately tiny at the warmest temperatures; red and green must keep real range.
            int minimumSpan = c == 2 ? 1000 : 8000;
            Assert.True(channel[^1] - channel[0] > minimumSpan, $"channel {c} is nearly flat");
        }
    }

    [Theory]
    [MemberData(nameof(Grid))]
    public void FitToLimits_AnySettings_ResultIsAccepted(int brightness, int contrast, double gamma, int kelvin)
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(brightness, contrast, gamma, kelvin));

        GammaRamp fitted = RampBuilder.FitToLimits(ramp, RampLimits.Default);

        Assert.True(RampLimits.Default.Accepts(fitted));
    }

    [Fact]
    public void Build_WarmTemperature_ReducesBlueMoreThanGreen()
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(TemperatureK: 3000));

        Assert.Equal(ushort.MaxValue, ramp.Red[^1]);
        Assert.True(ramp.Green[^1] < ramp.Red[^1]);
        Assert.True(ramp.Blue[^1] < ramp.Green[^1]);
    }

    [Fact]
    public void Build_CoolTemperature_KeepsBlueAtFull()
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(TemperatureK: 9000));

        Assert.Equal(ushort.MaxValue, ramp.Blue[^1]);
        Assert.True(ramp.Red[^1] < ramp.Blue[^1]);
    }

    [Fact]
    public void Build_NegativeBrightness_DimsWhite()
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(Brightness: -50));

        Assert.InRange(ramp.Red[^1], 32700, 32800);
    }

    [Fact]
    public void Build_PositiveBrightness_LiftsBlack()
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(Brightness: 50));

        Assert.InRange(ramp.Red[0], 16300, 16450);
        Assert.Equal(ushort.MaxValue, ramp.Red[^1]);
    }

    [Fact]
    public void Build_HigherGamma_BrightensMidtones()
    {
        GammaRamp normal = RampBuilder.Build(ColorSettings.Neutral);
        GammaRamp bright = RampBuilder.Build(new ColorSettings(Gamma: 1.5));

        Assert.True(bright.Green[128] > normal.Green[128]);
    }

    [Fact]
    public void FitToLimits_AcceptedRamp_ReturnsSameInstance()
    {
        GammaRamp identity = GammaRamp.Identity();

        Assert.Same(identity, RampBuilder.FitToLimits(identity, RampLimits.Default));
    }

    [Fact]
    public void FitToLimits_TooWarmForWindows_MovesTowardIdentity()
    {
        GammaRamp target = RampBuilder.Build(new ColorSettings(TemperatureK: 2000));
        Assert.False(RampLimits.Default.Accepts(target));

        GammaRamp fitted = RampBuilder.FitToLimits(target, RampLimits.Default);

        Assert.True(RampLimits.Default.Accepts(fitted));
        Assert.True(fitted.Blue[^1] > target.Blue[^1]);
        Assert.True(fitted.Blue[^1] < ushort.MaxValue);
    }

    [Theory]
    [InlineData(3400, 0, true)]
    [InlineData(2700, 0, false)]
    [InlineData(10000, 0, true)]
    [InlineData(6500, -50, true)]
    [InlineData(2700, -30, false)]
    public void DefaultLimits_MatchMeasuredWindowsBehaviour(int kelvin, int brightness, bool accepted) =>
        Assert.Equal(accepted, RampLimits.Default.Accepts(RampBuilder.Build(new ColorSettings(brightness, TemperatureK: kelvin))));

    [Fact]
    public void ExtendedLimits_AcceptWarmRamps() =>
        Assert.True(RampLimits.Extended.Accepts(RampBuilder.Build(new ColorSettings(Brightness: -30, TemperatureK: 2000))));

    [Theory]
    [InlineData(null, false)]
    [InlineData(128, false)]
    [InlineData(256, true)]
    public void ForGammaRange_PicksExtendedOnlyAt256(int? value, bool extended) =>
        Assert.Equal(extended, ReferenceEquals(RampLimits.Extended, RampLimits.ForGammaRange(value)));

    [Fact]
    public void Base64_RoundTrip_PreservesValues()
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(Brightness: 10, TemperatureK: 4000));

        GammaRamp? restored = GammaRamp.FromBase64(ramp.ToBase64());

        Assert.NotNull(restored);
        Assert.True(ramp.ApproximatelyEquals(restored, 0));
    }

    [Fact]
    public void ShadowBoost_LiftsDarkTonesAndLeavesTheRest()
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(ShadowBoost: 100));

        Assert.Equal(0, ramp.Red[0]); // black stays black
        Assert.True(ramp.Red[40] > 40 * 257 * 1.5, "dark greys are lifted a lot");
        Assert.True(ramp.Red[180] < (180 * 257) + 600, "mid-tones and highlights barely move");
        Assert.Equal(ushort.MaxValue, ramp.Red[255]);
    }

    [Fact]
    public void ShadowBoost_KeepsContrast_SoThePictureDoesNotGoGrey()
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(ShadowBoost: 100));

        // Over every eight steps the curve rises by at least 60 % of what it does unchanged.
        for (int i = 8; i < GammaRamp.Size; i++)
            Assert.True(ramp.Red[i] - ramp.Red[i - 8] >= 0.6 * 8 * 257, $"flat at step {i}");
    }

    [Theory]
    [InlineData(25)]
    [InlineData(60)]
    [InlineData(100)]
    public void ShadowBoost_StaysRisingAndWithinWindowsDefaultLimits(int amount)
    {
        GammaRamp ramp = RampBuilder.Build(new ColorSettings(ShadowBoost: amount));

        Assert.True(RampLimits.Default.Accepts(ramp));
        for (int i = 1; i < GammaRamp.Size; i++)
            Assert.True(ramp.Red[i] > ramp.Red[i - 1], $"step {i}");
    }

    [Fact]
    public void ShadowBoost_IsPartOfTheNeutralCheck()
    {
        Assert.False(new ColorSettings(ShadowBoost: 1).HasNeutralRamp);
        Assert.Equal(100, new ColorSettings(ShadowBoost: 500).Clamp().ShadowBoost);
    }

    [Fact]
    public void ShadowBoost_FadesWithTheSchedule() =>
        Assert.Equal(30, ColorBlend.Lerp(new ColorSettings(), new ColorSettings(ShadowBoost: 60), 0.5).ShadowBoost);

    [Fact]
    public void ShadowBoost_IsOffWithoutPro()
    {
        var settings = new ColorSettings(Gamma: 1.1, ShadowBoost: 40);
        Assert.Equal(0, new DispHub.Core.Licensing.FeatureGate(IsPro: false).Allowed(settings).ShadowBoost);
        Assert.Equal(40, new DispHub.Core.Licensing.FeatureGate(IsPro: false, InTrial: true).Allowed(settings).ShadowBoost);
        Assert.Equal(1.1, new DispHub.Core.Licensing.FeatureGate(IsPro: false).Allowed(settings).Gamma);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public void FromBase64_InvalidInput_ReturnsNull(string? input) => Assert.Null(GammaRamp.FromBase64(input));
}
