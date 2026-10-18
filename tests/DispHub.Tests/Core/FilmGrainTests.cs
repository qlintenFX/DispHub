using DispHub.Core.Effects;

namespace DispHub.Tests.Core;

public sealed class FilmGrainTests
{
    [Fact]
    public void Template_IsTheSameEveryTime()
    {
        Assert.Equal(FilmGrain.Template(), FilmGrain.Template());
        Assert.Equal(FilmGrain.Width * FilmGrain.Height * 4, FilmGrain.Template().Length);
    }

    [Fact]
    public void Template_HasUnitVariance_AndClumpsLikeFilm()
    {
        float[] t = FilmGrain.Template();
        double sum = 0, squares = 0, neighbours = 0;
        int n = 0;
        for (int y = 9; y < FilmGrain.Height; y++)
        {
            for (int x = 10; x < FilmGrain.Width - 3; x++)
            {
                double v = t[((y * FilmGrain.Width) + x) * 4];
                double left = t[((y * FilmGrain.Width) + x - 1) * 4];
                sum += v;
                squares += v * v;
                neighbours += v * left;
                n++;
            }
        }
        double variance = (squares / n) - ((sum / n) * (sum / n));
        Assert.InRange(variance, 0.85, 1.15);

        // The auto-regressive filter makes neighbouring grains alike: grain, not white noise.
        Assert.True(neighbours / n > 0.1, $"neighbour correlation {neighbours / n:F2}");
    }

    [Fact]
    public void ScalingTable_InterpolatesThePointsLikeAV1Decoders()
    {
        float[] table = FilmGrain.ScalingTable();

        Assert.Equal(256, table.Length);
        Assert.Equal(0, table[0]);
        Assert.Equal(0, table[16]);
        Assert.Equal(136 / 184f, table[25]);
        Assert.Equal(1, table[178]);
        Assert.Equal(1, table[255]);
        // Halfway between (16, 0) and (25, 136), rounded the way dav1d rounds.
        Assert.Equal(76 / 184f, table[21]);
    }

    [Fact]
    public void Scaling_IsAV1TestVectorOne()
    {
        Assert.Equal(14, FilmGrain.LumaScaling.Count);
        Assert.Equal((16, 0), FilmGrain.LumaScaling[0]);
        Assert.Equal((178, 184), FilmGrain.LumaScaling[^1]);
    }
}
