using System.Windows;
using System.Windows.Interop;
using DispHub.Core.Licensing;
using DispHub.Core.Model;
using DispHub.Localization;
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
            Content = Loc.Format("Upgrade_Body", feature, FeatureGate.FreeProfileLimit),
            PrimaryButtonText = Loc.Get("Common_GetPro"),
            CloseButtonText = Loc.Get("Upgrade_NotNow"),
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
                Title = Loc.Get("Upgrade_FailedTitle"),
                Content = Loc.Get("Upgrade_FailedText"),
                CloseButtonText = Loc.Get("Common_OK"),
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

/// <summary>Applies the Appearance settings: theme, accent colour and window backdrop.</summary>
public sealed class ThemeService
{
    private Wpf.Ui.Controls.FluentWindow? _window;
    private bool _watching;
    private UiConfig _ui = new();

    /// <summary>Accent colours to choose from: Windows' own palette.</summary>
    public static IReadOnlyList<string> AccentPalette { get; } =
    [
        "#0078D4", "#0099BC", "#00B294", "#10893E", "#498205", "#FFB900", "#F7630C", "#DA3B01",
        "#E81123", "#E3008C", "#B146C2", "#744DA9", "#5D5A58", "#68768A",
    ];

    public void Apply(UiConfig ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        WindowBackdropType backdrop = Backdrop(ui.Backdrop);
        bool systemAccent = ui.AccentColor is null;
        switch (ui.Theme)
        {
            case ThemePreference.Light:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, backdrop, updateAccent: systemAccent);
                break;
            case ThemePreference.Dark:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, backdrop, updateAccent: systemAccent);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme(updateAccent: systemAccent);
                break;
        }
        if (ui.AccentColor is { } hex && System.Windows.Media.ColorConverter.ConvertFromString(hex) is System.Windows.Media.Color accent)
            ApplicationAccentColorManager.Apply(accent, ApplicationThemeManager.GetAppTheme());

        if (_window is not { IsLoaded: true })
            return;
        if (_window.WindowBackdropType != backdrop)
            _window.WindowBackdropType = backdrop;

        // Follow live Windows theme changes only when the user chose "System". The watcher needs a loaded window.
        if (_watching)
        {
            SystemThemeWatcher.UnWatch(_window);
            _watching = false;
        }
        if (ui.Theme == ThemePreference.System)
        {
            SystemThemeWatcher.Watch(_window, backdrop, updateAccents: systemAccent);
            _watching = true;
        }
    }

    /// <summary>Call once the window has loaded.</summary>
    public void Attach(Wpf.Ui.Controls.FluentWindow window, UiConfig ui)
    {
        _window = window;
        window.WindowBackdropType = Backdrop(ui.Backdrop);
        Apply(ui);
    }

    public void Reapply() => Apply(_ui);

    private static WindowBackdropType Backdrop(Core.Model.WindowBackdrop backdrop) => backdrop switch
    {
        Core.Model.WindowBackdrop.MicaAlt => WindowBackdropType.Tabbed,
        Core.Model.WindowBackdrop.Acrylic => WindowBackdropType.Acrylic,
        Core.Model.WindowBackdrop.Solid => WindowBackdropType.None,
        _ => WindowBackdropType.Mica,
    };
}
