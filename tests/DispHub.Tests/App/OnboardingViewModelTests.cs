// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using DispHub.Core.Config;
using DispHub.Platform.Shell;
using DispHub.Platform.Store;
using DispHub.Services;
using DispHub.Tests.Core;
using DispHub.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispHub.Tests.App;

public sealed class OnboardingViewModelTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly AppController _app;
    private readonly OnboardingViewModel _walkthrough;
    private readonly List<bool> _finished = [];

    public OnboardingViewModelTests()
    {
        _app = new AppController(new ConfigStore(_dir.Path), new AppControllerTests.FakeColors(), new FakeLicenseService(startAsPro: true), new AppControllerTests.FakeForeground(), NullLogger<AppController>.Instance);
        _app.Start(new AppControllerTests.FakeHotkeys());
        _walkthrough = new OnboardingViewModel(_app, new FakeAutostart());
        _walkthrough.Finished += (_, tour) => _finished.Add(tour);
    }

    public void Dispose()
    {
        _app.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public void TheLastStep_OffersTheTour()
    {
        for (int i = 1; i < OnboardingViewModel.StepCount; i++)
            _walkthrough.NextCommand.Execute(null);
        Assert.True(_walkthrough.IsLast);

        _walkthrough.NextCommand.Execute(null);

        Assert.Equal([true], _finished);
        Assert.True(_app.Config.Ui.OnboardingDone);
    }

    [Fact]
    public void ClosingEarly_CountsAsSeen_Once()
    {
        _walkthrough.NextCommand.Execute(null);

        _walkthrough.Complete(tour: false);
        _walkthrough.Complete(tour: false);

        Assert.Equal([false], _finished);
        Assert.True(_app.Config.Ui.OnboardingDone);
    }

    [Fact]
    public void EffectsSwitch_IsTheSettingsSwitch()
    {
        _walkthrough.ShowEffects = !_app.Config.Ui.ShowEffects;

        Assert.Equal(_walkthrough.ShowEffects, _app.Config.Ui.ShowEffects);
    }

    private sealed class FakeAutostart : IAutostart
    {
        public Task<AutostartState> GetStateAsync() => Task.FromResult(AutostartState.Disabled);

        public Task<AutostartState> SetEnabledAsync(bool enabled) => Task.FromResult(enabled ? AutostartState.Enabled : AutostartState.Disabled);
    }
}
