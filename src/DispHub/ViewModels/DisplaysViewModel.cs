// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Platform.Displays;
using DispHub.Platform.Gamma;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>Every connected display: what's on it, any problem worth knowing, and whether DispHub changes it.</summary>
public sealed partial class DisplaysViewModel : PageViewModel
{
    private readonly AppController _app;
    private readonly IUpgradeService _upgrade;

    public DisplaysViewModel(AppController app, IUpgradeService upgrade)
        : base(Loc.Get("Nav_Displays"), Wpf.Ui.Controls.SymbolRegular.Desktop24)
    {
        _app = app;
        _upgrade = upgrade;
        _app.MonitorsChanged += (_, _) => Load();
        _app.ModeAbilitiesChanged += (_, _) => Load();
        _app.StateChanged += (_, _) => Load();
        _app.PowerChanged += (_, _) => Load();
        _app.MonitorLevelsChanged += (_, _) =>
        {
            foreach (DisplayItem item in Displays)
                item.UpdateLevel();
        };
        Load();
    }

    public ObservableCollection<DisplayItem> Displays { get; } = [];

    public override void OnShown()
    {
        Load();
        // A monitor's own buttons may have changed its brightness; the sliders follow once it has been read.
        _app.RereadMonitorLevels();
    }

    private void Load()
    {
        Displays.Clear();
        foreach (MonitorStatus status in _app.Monitors)
            Displays.Add(new DisplayItem(_app, status, _upgrade));
    }
}

public sealed partial class DisplayItem : ObservableObject
{
    private readonly AppController _app;
    private readonly IUpgradeService _upgrade;
    private readonly string _key;
    private readonly string _mode;

    public DisplayItem(AppController app, MonitorStatus status, IUpgradeService upgrade)
    {
        _app = app;
        _upgrade = upgrade;
        _key = status.Monitor.Key;
        MonitorInfo monitor = status.Monitor;
        Name = monitor.Name;
        IsPrimary = monitor.IsPrimary;
        DisplayMode? mode = DisplayModes.Current(monitor.DeviceName);
        _mode = mode is { } m ? string.Create(CultureInfo.CurrentCulture, $"{m.Width} × {m.Height} · {m.RefreshHz} Hz") : "";
        IsHdr = status.State == MonitorColorState.Hdr;
        IsLimited = status.State == MonitorColorState.Limited;
        NoVibrance = !status.SupportsVibrance;
        Included = !(app.Config.Monitors.TryGetValue(_key, out MonitorConfig? config) && config.Excluded);
        UpdateSummary();
        UpdateLevel();

        // Laptop screens, as laptop makers' own apps do: desktops are on mains power.
        IReadOnlyList<int> rates = monitor.IsInternal && app.ModeAbilities.TryGetValue(_key, out ModeAbilities? abilities)
            ? abilities.RefreshRates
            : [];
        int? low = RefreshRates.Pick(rates, RefreshChoice.Lowest);
        HasBatteryRefresh = low is not null && low != RefreshRates.Pick(rates, RefreshChoice.Highest);
        BatteryRefreshText = low is { } hz ? Loc.Format("Displays_LowRefreshOnBattery", hz) : "";
        CanUseDisplayModes = app.Features.DisplayModes;
        _lowRefreshOnBattery = config?.LowRefreshOnBattery == true;
    }

    /// <summary>A laptop screen with a lower refresh rate to save battery.</summary>
    public bool HasBatteryRefresh { get; }

    public string BatteryRefreshText { get; }

    public bool CanUseDisplayModes { get; }

    private bool _lowRefreshOnBattery;

    public bool LowRefreshOnBattery
    {
        get => _lowRefreshOnBattery;
        set
        {
            if (SetProperty(ref _lowRefreshOnBattery, value))
                _app.SetLowRefreshOnBattery(_key, value);
        }
    }

    [RelayCommand]
    private void UnlockBatteryRefresh() => _upgrade.ShowUpgrade(Loc.Get("Upgrade_DisplayModes"));

    /// <summary>The monitor's own brightness can be set over DDC/CI: the card shows its slider.</summary>
    [ObservableProperty] public partial bool HasLevel { get; set; }

    [ObservableProperty] public partial int Level { get; set; }

    private bool _readingLevel;

    /// <summary>Shows the level the monitor reports (or was last set to), without sending it back.</summary>
    public void UpdateLevel()
    {
        _readingLevel = true;
        try
        {
            HasLevel = _app.MonitorLevels.TryGetValue(_key, out int level);
            if (HasLevel)
                Level = level;
        }
        finally
        {
            _readingLevel = false;
        }
    }

    partial void OnLevelChanged(int value)
    {
        if (!_readingLevel)
            _app.SetMonitorLevel(_key, value);
    }

    public string Name { get; }
    public bool IsPrimary { get; }

    /// <summary>Windows HDR is on: gamma changes are paused there.</summary>
    public bool IsHdr { get; }

    /// <summary>Windows only allowed a milder version of the profile.</summary>
    public bool IsLimited { get; }

    public bool NoVibrance { get; }

    [ObservableProperty] public partial bool Included { get; set; }
    [ObservableProperty] public partial string Summary { get; set; } = "";

    partial void OnIncludedChanged(bool value)
    {
        if (!_app.Config.Monitors.TryGetValue(_key, out MonitorConfig? config))
        {
            config = new MonitorConfig { Name = Name };
            _app.Config.Monitors[_key] = config;
        }
        if (config.Excluded == !value)
            return;
        config.Excluded = !value;
        _app.SettingsChanged();
        UpdateSummary();
    }

    [RelayCommand]
    private void AllowFullRange()
    {
        if (GammaRange.TryEnableExtended())
            _app.OnDisplayChanged();
    }

    private void UpdateSummary()
    {
        string showing;
        if (!Included)
            showing = Loc.Get("Displays_LeftAlone");
        else if (_app.Config.IsOff)
            showing = Loc.Get("Displays_ShowingOff");
        else if (_app.Config.IsPaused)
            showing = Loc.Get("Displays_ShowingOriginal");
        else if (_app.Current?.Monitors.GetValueOrDefault(_key)?.ProfileId is { } id && _app.FindProfile(id) is { } profile)
            showing = Loc.Format("Displays_Showing", profile.Name);
        else
            showing = "";
        Summary = string.Join(" · ", new[] { _mode, showing }.Where(t => t.Length > 0));
    }
}
