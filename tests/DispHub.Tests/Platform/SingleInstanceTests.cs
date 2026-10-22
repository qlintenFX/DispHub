using DispHub.Platform.Shell;

namespace DispHub.Tests.Platform;

public sealed class SingleInstanceTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public void TakeOver_AsksTheRunningOneToQuit_ThenBecomesFirst()
    {
        string name = "DispHubTest." + Guid.NewGuid().ToString("N");
        using var running = new ManualResetEventSlim();
        string[]? received = null;

        // The running instance owns its mutex on its own thread and lets go when asked to exit, as DispHub does.
        var owner = new Thread(() =>
        {
            using var first = new SingleInstance(name);
            using var asked = new ManualResetEventSlim();
            first.Activated += (_, args) =>
            {
                received = args;
                asked.Set();
            };
            first.StartListening();
            running.Set();
            asked.Wait(Wait);
        });
        owner.Start();
        Assert.True(running.Wait(Wait, TestContext.Current.CancellationToken));

        using var second = new SingleInstance(name);
        Assert.False(second.IsFirstInstance);

        Assert.True(second.TakeOver(Wait));
        Assert.True(second.IsFirstInstance);
        Assert.Equal<string>(["--exit"], received!);
        owner.Join(Wait);
    }

    [Fact]
    public void TakeOver_WhenAlreadyFirst_DoesNothing()
    {
        using var only = new SingleInstance("DispHubTest." + Guid.NewGuid().ToString("N"));

        Assert.True(only.TakeOver(TimeSpan.Zero));
        Assert.True(only.IsFirstInstance);
    }
}
