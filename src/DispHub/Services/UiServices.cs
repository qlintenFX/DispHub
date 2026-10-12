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

/// <summary>
/// Applies the Appearance settings (theme, accent colour, backdrop) to every DispHub window. WPF-UI only updates
/// Application.MainWindow, which in DispHub is the hidden host window, so each visible window is registered here.
/// </summary>
public sealed class ThemeService
{
    private readonly List<(FluentWindow Window, bool Flyout)> _windows = [];
    private FluentWindow? _main;
    private bool _watching;
    private UiConfig _ui = new();

    public ThemeService()
    {
        // The Windows theme or accent changed while following it.
        ApplicationThemeManager.Changed += (_, _) => UpdateWindows();
    }

    /// <summary>Windows' own accent colour palette, in the order Settings shows it.</summary>
    public static IReadOnlyList<string> AccentPalette { get; } =
    [
        "#FFB900", "#FF8C00", "#F7630C", "#CA5010", "#DA3B01", "#EF6950", "#D13438", "#FF4343",
        "#E74856", "#E81123", "#EA005E", "#C30052", "#E3008C", "#BF0077", "#C239B3", "#9A0089",
        "#0078D4", "#0063B1", "#8E8CD8", "#6B69D6", "#8764B8", "#744DA9", "#B146C2", "#881798",
        "#0099BC", "#2D7D9A", "#00B7C3", "#038387", "#00B294", "#018574", "#00CC6A", "#10893E",
        "#7A7574", "#5D5A58", "#68768A", "#515C6B", "#567C73", "#486860", "#498205", "#107C10",
        "#767676", "#4C4A48", "#69797E", "#4A5459", "#647C64", "#525E54", "#847545", "#7E735F",
    ];

    public void Apply(UiConfig ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        WindowBackdropType backdrop = Backdrop(ui.Backdrop, flyout: false);
        bool systemAccent = ParseAccent(ui.AccentColor) is null;
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
        if (ParseAccent(ui.AccentColor) is { } accent)
            ApplicationAccentColorManager.Apply(accent, ApplicationThemeManager.GetAppTheme());
        UpdateWindows();

        // Follow live Windows theme changes only when the user chose "System". The watcher needs a loaded window.
        if (_main is not { IsLoaded: true })
            return;
        if (_watching)
        {
            SystemThemeWatcher.UnWatch(_main);
            _watching = false;
        }
        if (ui.Theme == ThemePreference.System)
        {
            SystemThemeWatcher.Watch(_main, backdrop, updateAccents: systemAccent);
            _watching = true;
        }
    }

    /// <summary>The main window; call once it has loaded.</summary>
    public void Attach(FluentWindow window, UiConfig ui)
    {
        _main = window;
        Register(window, flyout: false);
        Apply(ui);
    }

    /// <summary>A panel such as the quick panel: acrylic like Windows' own flyouts, unless backdrops are off.</summary>
    public void AttachFlyout(FluentWindow window)
    {
        Register(window, flyout: true);
        if (window.IsLoaded)
            UpdateWindows();
        else
            window.Loaded += (_, _) => UpdateWindows();
    }

    public void Reapply() => Apply(_ui);

    /// <summary>A colour from settings, or null for none or an invalid one.</summary>
    public static System.Windows.Media.Color? ParseAccent(string? hex)
    {
        if (hex is null || !System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9A-Fa-f]{6}$"))
            return null;
        return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
    }

    private void Register(FluentWindow window, bool flyout)
    {
        if (_windows.Any(w => w.Window == window))
            return;
        _windows.Add((window, flyout));
        window.Closed += (_, _) => _windows.RemoveAll(w => w.Window == window);
    }

    private void UpdateWindows()
    {
        ApplicationTheme theme = ApplicationThemeManager.GetAppTheme();
        foreach ((FluentWindow window, bool flyout) in _windows)
        {
            if (new WindowInteropHelper(window).Handle == 0)
                continue;
            WindowBackdropType backdrop = Backdrop(_ui.Backdrop, flyout);
            if (window.WindowBackdropType != backdrop)
                window.WindowBackdropType = backdrop;
            // Sets the backdrop and, just as important, the dark title bar and dark backdrop tint.
            WindowBackgroundManager.UpdateBackground(window, theme, backdrop);
        }
    }

    private static WindowBackdropType Backdrop(Core.Model.WindowBackdrop backdrop, bool flyout) => backdrop switch
    {
        Core.Model.WindowBackdrop.Solid => WindowBackdropType.None,
        _ when flyout => WindowBackdropType.Acrylic,
        Core.Model.WindowBackdrop.MicaAlt => WindowBackdropType.Tabbed,
        _ => WindowBackdropType.Mica,
    };
}
