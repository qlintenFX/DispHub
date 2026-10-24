// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using DispHub.Core.Color;
using DispHub.Core.Config;
using DispHub.Platform.Store;
using DispHub.Services;
using DispHub.Tests.Core;
using DispHub.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace DispHub.Tests.App;

public sealed class QuickPanelViewModelTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly AppController _app;
    private readonly QuickPanelViewModel _panel;

    public QuickPanelViewModelTests()
    {
        _app = new AppController(new ConfigStore(_dir.Path), new AppControllerTests.FakeColors(), new FakeLicenseService(startAsPro: true), new AppControllerTests.FakeForeground(), NullLogger<AppController>.Instance);
        _app.Start(new AppControllerTests.FakeHotkeys());
        _panel = new QuickPanelViewModel(_app);
    }

    public void Dispose()
    {
        _app.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public void WarmthSlider_IsAtTheLeft_ForNeutralColours()
    {
        _panel.Warmth = ColorSettings.NeutralTemperatureK;

        Assert.Equal(0, _panel.WarmthLevel);
    }

    [Fact]
    public void WarmthSlider_MovingRight_LowersTheTemperature()
    {
        _panel.WarmthLevel = 2900;

        Assert.Equal(3600, _panel.Warmth);
    }

    [Fact]
    public void WarmthSlider_AtTheRight_IsTheWarmest()
    {
        _panel.WarmthLevel = QuickPanelViewModel.WarmthRange;

        Assert.Equal(QuickPanelViewModel.WarmestK, _panel.Warmth);
    }

    [Fact]
    public void WarmthSlider_Follows_WhenTheTemperatureChanges()
    {
        var changed = new List<string?>();
        ((INotifyPropertyChanged)_panel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _panel.Warmth = 4000;

        Assert.Contains(nameof(QuickPanelViewModel.WarmthLevel), changed);
        Assert.Equal(2500, _panel.WarmthLevel);
    }
}
