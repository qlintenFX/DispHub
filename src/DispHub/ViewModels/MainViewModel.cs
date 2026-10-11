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
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ILicenseService _license;
    private readonly IUpgradeService _upgrade;

    public MainViewModel(
        ProfilesViewModel profiles,
        PresetsViewModel presets,
        AutomationViewModel automation,
        SettingsViewModel settings,
        AboutViewModel about,
        ILicenseService license,
        IUpgradeService upgrade)
    {
        _license = license;
        _upgrade = upgrade;
        Pages = [profiles, presets, automation, settings, about];
        SelectedPage = profiles;
        _license.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshLicense);
        RefreshLicense();
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    [ObservableProperty]
    public partial PageViewModel SelectedPage { get; set; }

    partial void OnSelectedPageChanged(PageViewModel value)
    {
        if (value is PresetsViewModel presets)
            _ = presets.EnsureLoadedAsync();
    }

    [ObservableProperty]
    public partial bool ShowUpgrade { get; set; }

    public void Navigate<T>()
        where T : PageViewModel => SelectedPage = Pages.OfType<T>().First();

    [RelayCommand]
    private Task Upgrade() => _upgrade.PurchaseAsync();

    private void RefreshLicense() => ShowUpgrade = _license.CanPurchase && !_license.IsPro;
}
