using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Platform.Store;
using DispHub.Services;

namespace DispHub.ViewModels;

public sealed partial class AboutViewModel : PageViewModel
{
    public const string RepositoryUrl = "https://github.com/qlintenFX/DispHub";

    private readonly AppController _app;
    private readonly ILicenseService _license;
    private readonly IUpgradeService _upgrade;

    public AboutViewModel(AppController app, ILicenseService license, IUpgradeService upgrade)
        : base(Loc.Get("Nav_About"), Wpf.Ui.Controls.SymbolRegular.Info24)
    {
        _app = app;
        _license = license;
        _upgrade = upgrade;
        _license.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(RefreshLicense);
        RefreshLicense();
    }

    public static string Version { get; } =
        typeof(AboutViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "1.0.0";

    [ObservableProperty]
    public partial string Edition { get; set; } = "";

    [ObservableProperty]
    public partial bool CanUpgrade { get; set; }

    [ObservableProperty]
    public partial string? CopiedMessage { get; set; }

    [RelayCommand]
    private Task Upgrade() => _upgrade.PurchaseAsync();

    [RelayCommand]
    private static void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    [RelayCommand]
    private static void OpenLogs()
    {
        System.IO.Directory.CreateDirectory(AppPaths.LogDirectory);
        Process.Start(new ProcessStartInfo(AppPaths.LogDirectory) { UseShellExecute = true });
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        Clipboard.SetText(Diagnostics.Describe(_app.Monitors, Version));
        CopiedMessage = Loc.Get("About_Copied");
    }

    private void RefreshLicense()
    {
        Edition = _license.IsPro
            ? Loc.Get(_license.CanPurchase ? "About_EditionPro" : "About_EditionAll")
            : Loc.Get("About_EditionFree");
        CanUpgrade = _license.CanPurchase && !_license.IsPro;
    }
}
