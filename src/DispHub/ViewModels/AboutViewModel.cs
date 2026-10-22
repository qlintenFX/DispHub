// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

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

    /// <summary>Diagnostics were just copied: the button says so for a moment.</summary>
    [ObservableProperty]
    public partial bool Copied { get; set; }

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
        _ = FlashCopiedAsync();
    }

    private async Task FlashCopiedAsync()
    {
        Copied = true;
        await Task.Delay(TimeSpan.FromSeconds(2));
        Copied = false;
    }

    private void RefreshLicense()
    {
        Edition = _license.IsPro
            ? Loc.Get(_license.CanPurchase ? "About_EditionPro" : "About_EditionAll")
            : Loc.Get("About_EditionFree");
        CanUpgrade = _license.CanPurchase && !_license.IsPro;
    }
}
