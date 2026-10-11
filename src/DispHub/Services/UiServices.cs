using System.Windows;
using System.Windows.Interop;
using DispHub.Core.Licensing;
using DispHub.Core.Model;
using DispHub.Platform.Store;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace DispHub.Services;

public interface IUpgradeService
{
    /// <summary>Explains that <paramref name="feature"/> is part of Pro and offers the upgrade.</summary>
    void ShowUpgrade(string feature);

    Task PurchaseAsync();
}

public sealed class UpgradeService(ILicenseService license, Func<MainWindow> mainWindow) : IUpgradeService
{
    public async void ShowUpgrade(string feature)
    {
        if (!license.CanPurchase)
            return;

        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "DispHub Pro",
            Content = $"{feature} is part of DispHub Pro.\n\n" +
                      "Pro is a one-time purchase that unlocks:\n" +
                      "• Per-app profiles that switch automatically\n" +
                      "• Schedules with smooth fades\n" +
                      "• Different settings per display\n" +
                      $"• More than {FeatureGate.FreeProfileLimit} profiles\n\n" +
                      "It also supports development of DispHub.",
            PrimaryButtonText = "Get Pro",
            CloseButtonText = "Not now",
            Owner = ShowMainWindow(),
        };
        if (await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary)
            await PurchaseAsync();
    }

    public async Task PurchaseAsync()
    {
        MainWindow window = ShowMainWindow();
        PurchaseResult result = await license.PurchaseProAsync(new WindowInteropHelper(window).Handle);
        if (result == PurchaseResult.Failed)
        {
            var box = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Purchase didn't go through",
                Content = "The Microsoft Store couldn't complete the purchase. Check your connection and try again.",
                CloseButtonText = "OK",
                Owner = window,
            };
            await box.ShowDialogAsync();
        }
    }

    private MainWindow ShowMainWindow()
    {
        MainWindow window = mainWindow();
        window.ShowAndActivate();
        return window;
    }
}

public sealed class ThemeService
{
    private Window? _window;
    private bool _watching;

    public void Apply(ThemePreference preference)
    {
        switch (preference)
        {
            case ThemePreference.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica);
                break;
            case ThemePreference.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme(updateAccent: true);
                break;
        }

        // Follow live Windows theme changes only when the user chose "System". The watcher needs a loaded window.
        if (_window is not { IsLoaded: true })
            return;
        if (_watching && preference != ThemePreference.System)
        {
            SystemThemeWatcher.UnWatch(_window);
            _watching = false;
        }
        else if (!_watching && preference == ThemePreference.System)
        {
            SystemThemeWatcher.Watch(_window, WindowBackdropType.Mica, updateAccents: true);
            _watching = true;
        }
    }

    /// <summary>Call once the window has loaded.</summary>
    public void Attach(Window window, ThemePreference preference)
    {
        _window = window;
        Apply(preference);
    }
}
