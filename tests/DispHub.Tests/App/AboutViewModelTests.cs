// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Config;
using DispHub.Platform.Store;
using DispHub.Services;
using DispHub.Tests.Core;
using DispHub.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispHub.Tests.App;

public sealed class AboutViewModelTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly AppController _app;

    public AboutViewModelTests()
    {
        _app = new AppController(new ConfigStore(_dir.Path), new AppControllerTests.FakeColors(), new FakeLicenseService(startAsPro: true), new AppControllerTests.FakeForeground(), NullLogger<AppController>.Instance);
    }

    public void Dispose()
    {
        _app.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public async Task Tips_ShowTheStorePricesAndThankAfterwards()
    {
        var license = new FakeLicenseService(startAsPro: true);
        var upgrade = new FakeUpgrade(license);
        var about = new AboutViewModel(_app, license, upgrade);

        about.OnShown();
        await WaitForAsync(() => about.HasTips);
        Assert.Equal(["tip-small", "tip-medium", "tip-large"], about.Tips.Select(t => t.OfferToken));

        await about.TipCommand.ExecuteAsync(about.Tips[1]);

        Assert.True(about.Thanked);
        Assert.Equal("tip-medium", upgrade.Tipped);
    }

    [Fact]
    public async Task Tips_StayHiddenOutsideTheStore()
    {
        var license = new UnlockedLicenseService();
        var about = new AboutViewModel(_app, license, new FakeUpgrade(license));

        about.OnShown();
        await Task.Yield();

        Assert.False(about.HasTips);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition());
    }

    private sealed class FakeUpgrade(ILicenseService license) : IUpgradeService
    {
        public string? Tipped { get; private set; }

        public void ShowUpgrade(string feature)
        {
        }

        public Task PurchaseAsync() => Task.CompletedTask;

        public Task ShowTrialEndedAsync(int appSwitches, int scheduleSwitches) => Task.CompletedTask;

        public Task RateAsync() => Task.CompletedTask;

        public async Task<bool> TipAsync(string offerToken)
        {
            Tipped = offerToken;
            return await license.TipAsync(offerToken, 0) == PurchaseResult.Purchased;
        }
    }
}
