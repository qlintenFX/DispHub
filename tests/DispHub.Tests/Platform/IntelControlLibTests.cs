using DispHub.Platform.Vibrance;

namespace DispHub.Tests.Platform;

public sealed unsafe class IntelControlLibTests
{
    // Sizes from igcl_api.h under the x64 MSVC ABI. A wrong size makes the driver reject or corrupt the call.
    [Fact]
    public void StructSizes_MatchTheNativeHeader()
    {
        Assert.Equal(36, sizeof(IntelControlLib.InitArgs));
        Assert.Equal(320, sizeof(IntelControlLib.DeviceProperties));
        Assert.Equal(200, sizeof(IntelControlLib.DisplayProperties));
        Assert.Equal(128, sizeof(IntelControlLib.MatrixConfig));
        Assert.Equal(144, sizeof(IntelControlLib.BlockConfig));
        Assert.Equal(272, sizeof(IntelControlLib.PipeGetConfig));
        Assert.Equal(32, sizeof(IntelControlLib.PipeSetConfig));
    }

    [Fact]
    public void SaturationMatrix_One_IsIdentity()
    {
        double[] m = IntelControlLib.SaturationMatrix(1);

        Assert.Equal([1, 0, 0, 0, 1, 0, 0, 0, 1], m.Select(v => Math.Round(v, 12)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(1.4)]
    [InlineData(2)]
    public void SaturationMatrix_KeepsWhiteAndGreyNeutral(double saturation)
    {
        double[] m = IntelControlLib.SaturationMatrix(saturation);

        for (int row = 0; row < 3; row++)
            Assert.Equal(1, m[row * 3] + m[(row * 3) + 1] + m[(row * 3) + 2], 9);
    }

    [Fact]
    public void SaturationMatrix_Zero_IsGreyscale()
    {
        double[] m = IntelControlLib.SaturationMatrix(0);

        Assert.Equal(m[0..3], m[3..6]);
        Assert.Equal(m[0..3], m[6..9]);
    }
}

public sealed unsafe class AmdAdlTests
{
    // Sizes from the ADL SDK's adl_structures.h. Both structs are ints and fixed char arrays, so no padding.
    [Fact]
    public void StructSizes_MatchTheNativeHeader()
    {
        Assert.Equal(1572, sizeof(DispHub.Platform.Vibrance.AmdAdl.AdapterInfo));
        Assert.Equal(552, sizeof(DispHub.Platform.Vibrance.AmdAdl.DisplayInfo));
    }
}
