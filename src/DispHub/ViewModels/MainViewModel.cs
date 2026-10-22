// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Platform.Store;
using DispHub.Services;
using Wpf.Ui.Controls;

namespace DispHub.ViewModels;

public abstract class PageViewModel(string title, SymbolRegular icon) : ObservableObject
{
    public string Title { get; } = title;
    public SymbolRegular Icon { get; } = icon;

    /// <summary>Stable name for settings such as the start page.</summary>
    public string Key => GetType().Name.Replace("ViewModel", "", StringComparison.Ordinal);

    /// <summary>Called when the page is shown.</summary>
    public virtual void OnShown()
    {
    }

    /// <summary>Called when another page replaces this one.</summary>
    public virtual void OnHidden()
    {
    }
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppController _app;
    private readonly ILicenseService _license;
    private readonly IUpgradeService _upgrade;
    private readonly EffectsViewModel _effects;
    private readonly IReadOnlyList<PageViewModel> _allPages;
    private bool _isNarrow;

    public MainViewModel(
        HomeViewModel home,
        ProfilesViewModel profiles,
        ScheduleViewModel schedule,
        AppsViewModel apps,
        EffectsViewModel effects,
        DisplaysViewModel displays,
        SettingsViewModel settings,
        AboutViewModel about,
        AppController app,
        ILicenseService license,
        IUpgradeService upgrade)
    {
        _app = app;
        Home = home;
        _license = license;
        _upgrade = upgrade;
        _effects = effects;
        _allPages = [home, profiles, schedule, apps, effects, displays];
        Pages = [.. _allPages];
        FooterPages = [settings, about];
        settings.SetStartPages(_allPages);
        SelectedPage = AllPages.FirstOrDefault(p => p.Key == app.Config.Ui.StartPage) ?? home;
        home.NavigateRequested += (_, page) => SelectedPage = AllPages.FirstOrDefault(p => p.GetType() == page) ?? SelectedPage;
        _license.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshLicense);
        _app.ConfigChanged += (_, _) => RefreshLayout();
        _app.PowerChanged += (_, _) => IsOn = !_app.Config.IsOff;
        _app.ConfigChanged += (_, _) => IsOn = !_app.Config.IsOff;
        IsOn = !_app.Config.IsOff;
        RefreshLicense();
        RefreshLayout();
    }

    public HomeViewModel Home { get; }

    [ObservableProperty]
    public partial bool IsNavExpanded { get; set; } = true;

    [ObservableProperty]
    public partial System.Windows.GridLength NavWidth { get; set; } = new(220);

    [ObservableProperty]
    public partial double TextScale { get; set; } = 1;

    [ObservableProperty]
    public partial bool ShowUpgradeCard { get; set; }

    /// <summary>The main features, at the top of the navigation. Effects only shows when turned on in Settings.</summary>
    public System.Collections.ObjectModel.ObservableCollection<PageViewModel> Pages { get; }

    /// <summary>Settings and About, at the bottom.</summary>
    public IReadOnlyList<PageViewModel> FooterPages { get; }

    public IEnumerable<PageViewModel> AllPages => Pages.Concat(FooterPages);

    [ObservableProperty]
    public partial PageViewModel SelectedPage { get; set; }

    /// <summary>The selection in the top list; null while a footer page is shown.</summary>
    public PageViewModel? SelectedTopPage
    {
        get => Pages.Contains(SelectedPage) ? SelectedPage : null;
        set
        {
            if (value is not null)
                SelectedPage = value;
        }
    }

    public PageViewModel? SelectedFooterPage
    {
        get => FooterPages.Contains(SelectedPage) ? SelectedPage : null;
        set
        {
            if (value is not null)
                SelectedPage = value;
        }
    }

    partial void OnSelectedPageChanged(PageViewModel oldValue, PageViewModel newValue)
    {
        oldValue?.OnHidden();
        PageViewModel value = newValue;
        OnPropertyChanged(nameof(SelectedTopPage));
        OnPropertyChanged(nameof(SelectedFooterPage));
        value.OnShown();
    }

    [ObservableProperty]
    public partial bool ShowUpgrade { get; set; }

    public void Navigate<T>()
        where T : PageViewModel => SelectedPage = AllPages.OfType<T>().FirstOrDefault() ?? Pages[0];

    /// <summary>
    /// The window is narrow: show the navigation as icons, like Windows' own apps, so the page keeps its room.
    /// </summary>
    public void SetNarrow(bool narrow)
    {
        if (narrow == _isNarrow)
            return;
        _isNarrow = narrow;
        RefreshLayout();
    }

    /// <summary>DispHub is on: the power button in the title bar, on every page, shows it.</summary>
    [ObservableProperty] public partial bool IsOn { get; set; }

    [RelayCommand]
    private void TogglePower() => _app.ToggleOff(ChangeSource.Editor);

    [RelayCommand]
    private Task Upgrade() => _upgrade.PurchaseAsync();

    private void RefreshLicense()
    {
        ShowUpgrade = _license.CanPurchase && !_license.IsPro;
        ShowUpgradeCard = ShowUpgrade && IsNavExpanded;
    }

    private void RefreshLayout()
    {
        Controls.SmoothScrollViewer.ReduceMotion = _app.Config.Ui.ReduceMotion;
        IsNavExpanded = !_app.Config.Ui.CompactNavigation && !_isNarrow;
        NavWidth = new System.Windows.GridLength(IsNavExpanded ? 220 : 64);
        TextScale = _app.Config.Ui.TextScale;
        ShowUpgradeCard = ShowUpgrade && IsNavExpanded;

        bool showEffects = _app.Config.Ui.ShowEffects;
        if (showEffects && !Pages.Contains(_effects))
        {
            Pages.Insert(_allPages.ToList().IndexOf(_effects), _effects);
        }
        else if (!showEffects && Pages.Contains(_effects))
        {
            if (SelectedPage == _effects)
                SelectedPage = Pages[0];
            Pages.Remove(_effects);
        }
    }
}
