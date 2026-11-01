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
    public void FirstRun_StartsWithWindows()
    {
        var autostart = new FakeAutostart(AutostartState.Disabled);

        var walkthrough = new OnboardingViewModel(_app, autostart);

        Assert.True(walkthrough.LaunchAtLogin);
        Assert.Equal(AutostartState.Enabled, autostart.State);
    }

    [Theory]
    [InlineData(AutostartState.DisabledByUser)]
    [InlineData(AutostartState.DisabledByPolicy)]
    public void FirstRun_LeavesWindowsSettingAlone(AutostartState state)
    {
        var autostart = new FakeAutostart(state);

        var walkthrough = new OnboardingViewModel(_app, autostart);

        Assert.False(walkthrough.LaunchAtLogin);
        Assert.Equal(state, autostart.State);
    }

    [Fact]
    public void ShownAgain_KeepsTheChoice()
    {
        _app.Config.Ui.OnboardingDone = true;
        var autostart = new FakeAutostart(AutostartState.Disabled);

        var walkthrough = new OnboardingViewModel(_app, autostart);

        Assert.False(walkthrough.LaunchAtLogin);
        Assert.Equal(AutostartState.Disabled, autostart.State);
    }

    private sealed class FakeAutostart(AutostartState state = AutostartState.Disabled) : IAutostart
    {
        public AutostartState State { get; private set; } = state;

        public Task<AutostartState> GetStateAsync() => Task.FromResult(State);

        public Task<AutostartState> SetEnabledAsync(bool enabled)
        {
            State = enabled ? AutostartState.Enabled : AutostartState.Disabled;
            return Task.FromResult(State);
        }
    }
}
