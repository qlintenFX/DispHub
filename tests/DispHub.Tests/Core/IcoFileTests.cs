using DispHub.Core.Imaging;

namespace DispHub.Tests.Core;

public sealed class IcoFileTests
{
    private static byte[] AppIcon(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DispHub.slnx")))
            dir = dir.Parent;
        return File.ReadAllBytes(Path.Combine(dir!.FullName, "src", "DispHub", "Assets", name));
    }

    [Theory]
    [InlineData("DispHub.ico")]
    [InlineData("DispHub-paused.ico")]
    public void AppIcons_HaveEveryNotificationAreaSize(string name)
    {
        IReadOnlyList<IcoFile.Frame> frames = IcoFile.ReadFrames(AppIcon(name));

        // 100 % to 400 % scaling: 16, 20, 24, 32, 40, 48 and 64 px.
        foreach (int size in new[] { 16, 20, 24, 32, 40, 48, 64 })
            Assert.Equal(size, IcoFile.Pick(frames, size)!.Value.Size);
    }

    [Fact]
    public void Pick_UsesTheNextLargerFrameOrTheLargest()
    {
        IcoFile.Frame[] frames = [new(16, 100, 10), new(32, 200, 10), new(48, 300, 10)];

        Assert.Equal(32, IcoFile.Pick(frames, 28)!.Value.Size);
        Assert.Equal(48, IcoFile.Pick(frames, 96)!.Value.Size);
        Assert.Null(IcoFile.Pick([], 16));
    }

    [Fact]
    public void ReadFrames_RejectsGarbage()
    {
        Assert.Empty(IcoFile.ReadFrames([1, 2, 3]));
        Assert.Empty(IcoFile.ReadFrames(new byte[64]));
    }
}
