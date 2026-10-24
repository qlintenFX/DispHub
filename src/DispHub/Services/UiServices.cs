// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows;
using System.Windows.Interop;
using DispHub.Core.Licensing;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform.Shell;
using DispHub.Platform.Store;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using WindowBackdrop = DispHub.Core.Model.WindowBackdrop;

namespace DispHub.Services;

public interface IUpgradeService
{
    /// <summary>Explains that <paramref name="feature"/> is part of Pro and offers the upgrade.</summary>
    void ShowUpgrade(string feature);

    Task PurchaseAsync();

    /// <summary>Shows the Microsoft Store's rating dialog.</summary>
    Task RateAsync();
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

    public async Task RateAsync()
    {
        if (license.CanPurchase)
            await license.RequestReviewAsync(new WindowInteropHelper(ShowMainWindow()).Handle);
    }

    private MainWindow ShowMainWindow()
    {
        MainWindow window = mainWindow();
        window.ShowAndActivate();
        return window;
    }
}

/// <summary>
/// Applies the Appearance settings (theme, accent colour, window material) to DispHub's windows.
/// </summary>
/// <remarks>
/// Kept deliberately small. WPF-UI swaps the light and dark colours; DispHub sets the accent shades and asks
/// Windows for the material itself. WPF-UI's window helpers only reach Application.MainWindow (DispHub's hidden
/// host), and changing a FluentWindow's backdrop after it is created rebuilds its window chrome, which can throw
/// while the window loads and leave the title bar buttons dead.
/// </remarks>
public sealed class ThemeService
{
    private readonly List<FluentWindow> _windows = [];
    private readonly HashSet<FluentWindow> _flyouts = [];
    private readonly ILicenseService _license;
    private UiConfig _ui = new();

    public ThemeService(ILicenseService license)
    {
        _license = license;
        // Windows got the new theme colours: match the window material (dark or light Mica).
        ApplicationThemeManager.Changed += (_, _) => UpdateWindows();
        // The quick panel's opacity is Pro: buying it (or losing it) changes the panel.
        _license.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(UpdateWindows);
    }

    public void Apply(UiConfig ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        ApplicationTheme theme = ui.Theme switch
        {
            ThemePreference.Light => ApplicationTheme.Light,
            ThemePreference.Dark => ApplicationTheme.Dark,
            _ => WindowsTheme(),
        };

        // Accent first, then reload the theme colours: WPF-UI's switch, slider and check box brushes read the accent
        // only when its theme colours load, so a new accent alone would leave them in the old colour.
        ApplyAccent(theme);
        ApplicationThemeManager.Apply(theme, WindowBackdropType.None, updateAccent: false);
        ApplyAccent(theme); // Picks the fill shade for the theme that is now on.
        UpdateWindows();
    }

    /// <summary>Redraws the window backgrounds, e.g. after the quick panel's opacity changed.</summary>
    public void RefreshWindows() => UpdateWindows();

    /// <summary>Windows switched light or dark mode, or its accent colour.</summary>
    public void OnWindowsThemeChanged() => Apply(_ui);

    /// <summary>
    /// Call from the window's SourceInitialized, before it first shows. A flyout (the quick panel, on-screen messages)
    /// gets acrylic, as Windows' own flyouts do; other windows get the Background setting's material.
    /// </summary>
    public void Attach(FluentWindow window, bool flyout = false)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_windows.Contains(window))
            return;
        _windows.Add(window);
        if (flyout)
            _flyouts.Add(window);
        window.Closed += (_, _) =>
        {
            _windows.Remove(window);
            _flyouts.Remove(window);
        };
        UpdateWindow(window);
    }

    private static ApplicationTheme WindowsTheme()
    {
        SystemThemeManager.UpdateSystemThemeCache();
        return ApplicationThemeManager.GetSystemTheme() switch
        {
            SystemTheme.Dark or SystemTheme.Glow or SystemTheme.CapturedMotion => ApplicationTheme.Dark,
            SystemTheme.HC1 or SystemTheme.HC2 or SystemTheme.HCBlack or SystemTheme.HCWhite => ApplicationTheme.HighContrast,
            _ => ApplicationTheme.Light,
        };
    }

    private void UpdateWindows()
    {
        foreach (FluentWindow window in _windows)
            UpdateWindow(window);
    }

    private void ApplyAccent(ApplicationTheme theme)
    {
        if (_ui.AccentColor is not { } hex || theme == ApplicationTheme.HighContrast)
        {
            // Windows' own shades of its accent, as Settings and File Explorer use them.
            ApplicationAccentColorManager.Apply(ApplicationAccentColorManager.GetColorizationColor(), theme, systemGlassColor: false, systemAccentColor: true);
            return;
        }
        (string primary, string secondary, string tertiary) = Accents.Shades(hex, dark: theme == ApplicationTheme.Dark);
        ApplicationAccentColorManager.Apply(ToColor(hex), ToColor(primary), ToColor(secondary), ToColor(tertiary));
    }

    /// <summary>
    /// The flyout acrylic's tint: Windows' flyout grey for the theme, as solid as the opacity setting says (Pro) or
    /// the default.
    /// </summary>
    private System.Drawing.Color FlyoutTint(bool dark)
    {
        double opacity = new FeatureGate(_license.IsPro).PanelOpacity ? _ui.PanelOpacity : UiConfig.DefaultPanelOpacity;
        int alpha = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        return dark ? System.Drawing.Color.FromArgb(alpha, 0x20, 0x20, 0x20) : System.Drawing.Color.FromArgb(alpha, 0xF3, 0xF3, 0xF3);
    }

    /// <summary>Windows' "Transparency effects" switch (Settings > Personalisation > Colours).</summary>
    private static bool TransparencyEffectsOn()
    {
        try
        {
            return new global::Windows.UI.ViewManagement.UISettings().AdvancedEffectsEnabled;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return true;
        }
    }

    private void UpdateWindow(FluentWindow window)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
            return;
        ApplicationTheme theme = ApplicationThemeManager.GetAppTheme();

        // Materials need Windows 11 22H2; high contrast always gets a plain background.
        WindowBackdrop backdrop = theme == ApplicationTheme.HighContrast || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)
            ? WindowBackdrop.Solid
            : _ui.Backdrop;
        bool dark = theme == ApplicationTheme.Dark;
        bool flyout = _flyouts.Contains(window);
        // Acrylic that blurs the windows behind is only for flyouts: Windows draws it plainly when transparency
        // effects are off, and so does DispHub.
        bool acrylic = flyout && backdrop != WindowBackdrop.Solid && TransparencyEffectsOn();
        if (backdrop == WindowBackdrop.Solid || (flyout && !acrylic))
        {
            window.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ApplicationBackgroundBrush");
        }
        else
        {
            window.Background = System.Windows.Media.Brushes.Transparent;
            if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
                target.BackgroundColor = System.Windows.Media.Colors.Transparent;
        }
        WindowHelpers.SetMaterial(handle, flyout ? WindowBackdrop.Solid : backdrop, dark);
        if (flyout)
            WindowHelpers.SetAcrylic(handle, acrylic ? FlyoutTint(dark) : null);
    }

    private static System.Windows.Media.Color ToColor(string hex) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
}
