// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Localization;
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
    private bool _isNarrow;

    public MainViewModel(
        HomeViewModel home,
        ProfilesViewModel profiles,
        ScheduleViewModel schedule,
        AppsViewModel apps,
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
        Pages = [home, profiles, schedule, apps, displays];
        FooterPages = [settings, about];
        settings.SetStartPages(Pages);
        SelectedPage = AllPages.FirstOrDefault(p => p.Key == app.Config.Ui.StartPage) ?? home;
        home.NavigateRequested += (_, page) => SelectedPage = AllPages.FirstOrDefault(p => p.GetType() == page) ?? SelectedPage;
        _license.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshLicense);
        _app.LicenceChanged += (_, _) => RefreshLicense();
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

    /// <summary>The main features, at the top of the navigation.</summary>
    public IReadOnlyList<PageViewModel> Pages { get; }

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

    [ObservableProperty]
    public partial string UpgradeTitle { get; set; } = "DispHub Pro";

    [ObservableProperty]
    public partial string UpgradeText { get; set; } = "";

    /// <summary>Called when the window comes back, so the trial's days left stay current.</summary>
    public void OnActivated() => RefreshLicense();

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
        int days = _app.TrialEnds is { } end && _app.InTrial ? Core.Licensing.FeatureGate.TrialDaysLeft(end, DateTimeOffset.UtcNow) : 0;
        UpgradeTitle = days > 0 ? Loc.Get("Trial_CardTitle") : "DispHub Pro";
        UpgradeText = days switch
        {
            0 => Loc.Get("Pro_ShortPitch"),
            1 => Loc.Get("Trial_DayLeft"),
            _ => Loc.Format("Trial_DaysLeft", days),
        };
        ShowUpgradeCard = ShowUpgrade && IsNavExpanded;
    }

    private void RefreshLayout()
    {
        Controls.SmoothScrollViewer.ReduceMotion = _app.Config.Ui.ReduceMotion;
        IsNavExpanded = !_app.Config.Ui.CompactNavigation && !_isNarrow;
        NavWidth = new System.Windows.GridLength(IsNavExpanded ? 220 : 64);
        TextScale = _app.Config.Ui.TextScale;
        ShowUpgradeCard = ShowUpgrade && IsNavExpanded;
    }
}
