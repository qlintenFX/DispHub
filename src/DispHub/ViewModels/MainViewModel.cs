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

    public MainViewModel(
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
        _license = license;
        _upgrade = upgrade;
        Pages = [profiles, schedule, apps, effects, displays];
        FooterPages = [settings, about];
        settings.SetStartPages(Pages);
        SelectedPage = AllPages.FirstOrDefault(p => p.Key == app.Config.Ui.StartPage) ?? profiles;
        _license.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshLicense);
        _app.ConfigChanged += (_, _) => RefreshLayout();
        RefreshLicense();
        RefreshLayout();
    }

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

    public void Navigate<T>()
        where T : PageViewModel => SelectedPage = AllPages.OfType<T>().First();

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
        IsNavExpanded = !_app.Config.Ui.CompactNavigation;
        NavWidth = new System.Windows.GridLength(IsNavExpanded ? 220 : 64);
        TextScale = _app.Config.Ui.TextScale;
        ShowUpgradeCard = ShowUpgrade && IsNavExpanded;
    }
}
