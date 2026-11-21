// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Config;
using DispHub.Core.Licensing;
using DispHub.Platform.Store;
using DispHub.Services;
using DispHub.Tests.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispHub.Tests.App;

public sealed class ProTrialTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeLicenseService _license = new(startAsPro: false);
    private readonly AppController _app;
    private readonly List<string> _notices = [];

    public ProTrialTests()
    {
        _app = new AppController(new ConfigStore(_dir.Path), new AppControllerTests.FakeColors(), _license, new AppControllerTests.FakeForeground(), NullLogger<AppController>.Instance, _time);
        _app.Start(new AppControllerTests.FakeHotkeys());
    }

    public void Dispose()
    {
        _app.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public void TrialThatEndedWhileClosed_IsNotifiedOnce()
    {
        ProTrial trial = Trial(firstRun: _time.GetUtcNow() - FeatureGate.TrialLength - TimeSpan.FromDays(1));

        trial.NotifyWhenEnded(Notify, () => { });
        trial.NotifyWhenEnded(Notify, () => { });

        Assert.Equal(["Your Pro trial has ended"], _notices);
    }

    [Fact]
    public void RunningTrial_IsNotNotified()
    {
        ProTrial trial = Trial(firstRun: _time.GetUtcNow() - TimeSpan.FromDays(2));

        trial.NotifyWhenEnded(Notify, () => { });

        Assert.Empty(_notices);
    }

    [Fact]
    public void ProBuyer_IsNotNotified()
    {
        _license.PurchaseProAsync(0);
        ProTrial trial = Trial(firstRun: _time.GetUtcNow() - TimeSpan.FromDays(30));

        trial.NotifyWhenEnded(Notify, () => { });

        Assert.Empty(_notices);
    }

    [Fact]
    public void WithoutTheIcon_ItTriesAgainNextTime()
    {
        ProTrial trial = Trial(firstRun: _time.GetUtcNow() - TimeSpan.FromDays(30));

        trial.NotifyWhenEnded((_, _, _) => false, () => { });
        trial.NotifyWhenEnded(Notify, () => { });

        Assert.Single(_notices);
    }

    private ProTrial Trial(DateTimeOffset firstRun)
    {
        string path = Path.Combine(_dir.Path, "state.json");
        JsonFiles.WriteState(path, new AppStateData { FirstRunUtc = firstRun });
        var state = new AppState(_time, new NoFirstRunMarker(), path);
        var trial = new ProTrial(_app, _license, state, new NoUpgrade(), _time);
        trial.Attach(fakeStore: null);
        return trial;
    }

    private bool Notify(string title, string text, Action open)
    {
        _notices.Add(title);
        return true;
    }

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class NoUpgrade : IUpgradeService
    {
        public void ShowUpgrade(string feature)
        {
        }

        public Task PurchaseAsync() => Task.CompletedTask;

        public Task ShowTrialEndedAsync(int appSwitches, int scheduleSwitches) => Task.CompletedTask;

        public Task RateAsync() => Task.CompletedTask;

        public Task<bool> TipAsync(string offerToken) => Task.FromResult(false);
    }
}
