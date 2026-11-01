// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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

    /// <summary>The trial is over: what Pro did this week, and the offer to keep it.</summary>
    Task ShowTrialEndedAsync(int appSwitches, int scheduleSwitches);

    /// <summary>Shows the Microsoft Store's rating dialog.</summary>
    Task RateAsync();

    /// <summary>Leaves a tip through the Store. True once it went through.</summary>
    Task<bool> TipAsync(string offerToken);
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
            Content = Body(Loc.Format("Upgrade_Body", feature, FeatureGate.FreeProfileLimit)),
            PrimaryButtonText = Loc.Get("Common_GetPro"),
            CloseButtonText = Loc.Get("Upgrade_NotNow"),
            Owner = ShowMainWindow(),
        };
        if (await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary)
            await PurchaseAsync();
    }

    public async Task ShowTrialEndedAsync(int appSwitches, int scheduleSwitches)
    {
        if (!license.CanPurchase || license.IsPro)
            return;

        // Say what Pro actually did for them; people value what they'd lose more than what they could gain.
        string body = appSwitches + scheduleSwitches > 0
            ? Loc.Format("Trial_EndedUsage", appSwitches, scheduleSwitches)
            : Loc.Get("Trial_EndedGeneric");
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = Loc.Get("Trial_EndedTitle"),
            Content = Body(body + Environment.NewLine + Environment.NewLine + Loc.Format("Trial_EndedFree", FeatureGate.FreeProfileLimit)),
            PrimaryButtonText = Loc.Get("Common_GetPro"),
            CloseButtonText = Loc.Get("Trial_ContinueFree"),
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
            await ShowFailedAsync(window);
    }

    public async Task<bool> TipAsync(string offerToken)
    {
        MainWindow window = ShowMainWindow();
        PurchaseResult result = await license.TipAsync(offerToken, new WindowInteropHelper(window).Handle);
        if (result == PurchaseResult.Failed)
            await ShowFailedAsync(window);
        return result == PurchaseResult.Purchased;
    }

    private static async Task ShowFailedAsync(MainWindow window)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = Loc.Get("Upgrade_FailedTitle"),
            Content = Body(Loc.Get("Upgrade_FailedText")),
            CloseButtonText = Loc.Get("Common_OK"),
            Owner = window,
        };
        await box.ShowDialogAsync();
    }

    public async Task RateAsync()
    {
        if (license.CanPurchase)
            await license.RequestReviewAsync(new WindowInteropHelper(ShowMainWindow()).Handle);
    }

    /// <summary>A message's text, left-aligned: the message box would justify it, with gaps between words.</summary>
    private static System.Windows.Controls.TextBlock Body(string text) => new()
    {
        Text = text,
        TextAlignment = TextAlignment.Left,
        TextWrapping = TextWrapping.Wrap,
    };

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
    /// <summary>
    /// Windows can draw materials (Mica, acrylic) behind windows: Windows 11 22H2 and later. Elsewhere windows get a
    /// plain background, and the settings for materials are hidden.
    /// </summary>
    public static bool HasMaterials { get; set; } = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    /// <summary>For the settings that only matter with materials; read once, when a page is made.</summary>
    public static Visibility MaterialsVisibility => HasMaterials ? Visibility.Visible : Visibility.Collapsed;

    private readonly List<FluentWindow> _windows = [];
    private readonly HashSet<FluentWindow> _flyouts = [];
    private readonly ILicenseService _license;
    private UiConfig _ui = new();
    private Action? _slide;

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

    /// <summary>
    /// Gives drop-down lists and menus the flyouts' acrylic, as Windows 11 draws its own. Call once at start-up.
    /// </summary>
    public void AttachPopups()
    {
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ComboBox), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnComboBoxLoaded));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ContextMenu), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnContextMenuLoaded));
        EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ContextMenu), System.Windows.Controls.ContextMenu.OpenedEvent, new RoutedEventHandler(OnContextMenuOpened));
    }

    // WPF-UI draws these popups in a see-through window with a 30-point margin for its own shadow, and slides the list
    // in inside that window. A blur behind the window would fill the margin, and the window would show before the list
    // arrives. So on Windows 11 the window is the list's size, Windows rounds it, the window stays hidden until its
    // first frame is drawn, and then the whole window slides in.
    private void OnComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        var comboBox = (System.Windows.Controls.ComboBox)sender;
        if (!HasMaterials || comboBox.Template?.FindName("Popup", comboBox) is not Popup popup || popup.Child is not EffectThicknessDecorator decorator
            || comboBox.Template.FindName("DropDownBorder", comboBox) is not Border border || popup.Tag is PopupAcrylic)
            return;
        popup.Tag = PopupAcrylic.Instance;
        popup.AllowsTransparency = false;
        Unshadow(decorator, border);
        PresentationSource.AddSourceChangedHandler(popup.Child, OnPopupSourceChanged);
        popup.Opened += (_, _) =>
        {
            if (PresentationSource.FromVisual(popup.Child) is HwndSource source)
                ShowPopup(source, border, "ComboBoxDropDownBackground", above: popup.Child.PointToScreen(default).Y < comboBox.PointToScreen(default).Y);
        };
    }

    private void OnContextMenuLoaded(object sender, RoutedEventArgs e)
    {
        if (HasMaterials && PresentationSource.FromVisual((Visual)sender) is HwndSource source)
            WindowHelpers.Cloak(source.Handle, true);
    }

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        var menu = (System.Windows.Controls.ContextMenu)sender;
        if (!HasMaterials || PresentationSource.FromVisual(menu) is not HwndSource source)
            return;
        if (VisualTreeHelper.GetChildrenCount(menu) == 0 || VisualTreeHelper.GetChild(menu, 0) is not EffectThicknessDecorator decorator
            || decorator.Child is not Border border)
        {
            WindowHelpers.Cloak(source.Handle, false);
            return;
        }
        Unshadow(decorator, border);
        // Menus open below the pointer, or above it near the bottom of the screen.
        ShowPopup(source, border, "ContextMenuBackground", above: Mouse.GetPosition(menu).Y > menu.ActualHeight / 2);
    }

    private static void OnPopupSourceChanged(object sender, SourceChangedEventArgs e)
    {
        if (e.NewSource is HwndSource source)
            WindowHelpers.Cloak(source.Handle, true);
    }

    private static void Unshadow(EffectThicknessDecorator decorator, Border border)
    {
        decorator.Thickness = default;
        border.Effect = null;
        // WPF-UI slides the list in inside its window, from 90 points up; the decorator moves it back by as much. The
        // template's transform is frozen, so the slide would animate a copy: the border gets one of its own to animate.
        if (border.RenderTransform is TranslateTransform && decorator.RenderTransform is not TranslateTransform)
        {
            var inner = new TranslateTransform();
            border.RenderTransform = inner;
            var back = new TranslateTransform();
            BindingOperations.SetBinding(back, TranslateTransform.YProperty, new Binding(nameof(TranslateTransform.Y)) { Source = inner, Converter = Negate.Instance });
            decorator.RenderTransform = back;
        }
    }

    private void ShowPopup(HwndSource source, Border border, string background, bool above)
    {
        WindowHelpers.SetRoundCorners(source.Handle);
        if (PopupAcrylicOn())
        {
            border.Background = Brushes.Transparent;
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            WindowHelpers.SetAcrylic(source.Handle, FlyoutTint(ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark));
        }
        else
        {
            border.SetResourceReference(Border.BackgroundProperty, background);
            WindowHelpers.SetAcrylic(source.Handle, null);
        }
        SlideIn(source, above, animate: !_ui.ReduceMotion && SystemParameters.ClientAreaAnimation);
    }

    // Shown once its first frame is drawn, then slides 16 points out from where it opened, easing out over 200 ms.
    private void SlideIn(HwndSource source, bool above, bool animate)
    {
        _slide?.Invoke();
        System.Drawing.Rectangle end = WindowHelpers.Bounds(source.Handle);
        double distance = animate && !end.IsEmpty ? 16 * source.CompositionTarget.TransformToDevice.M22 * (above ? 1 : -1) : 0;
        void Offset(double y)
        {
            if (!source.IsDisposed && !end.IsEmpty)
                WindowHelpers.MoveTo(source.Handle, end.X, end.Y + (int)Math.Round(y));
        }
        Offset(distance);
        Stopwatch? clock = null;
        void Finish()
        {
            CompositionTarget.Rendering -= Step;
            _slide = null;
            if (!source.IsDisposed)
                WindowHelpers.Cloak(source.Handle, false);
            Offset(0);
        }
        void Step(object? s, EventArgs e)
        {
            if (clock is null)
            {
                // This frame draws the popup; it shows from the next one.
                clock = new Stopwatch();
                return;
            }
            if (!clock.IsRunning)
            {
                if (!source.IsDisposed)
                    WindowHelpers.Cloak(source.Handle, false);
                clock.Start();
            }
            double t = source.IsDisposed ? 1 : Math.Min(clock.Elapsed.TotalMilliseconds / 200, 1);
            if (t >= 1)
                Finish();
            else
                Offset(distance * Math.Pow(1 - t, 3));
        }
        _slide = Finish;
        CompositionTarget.Rendering += Step;
    }

    private sealed class Negate : IValueConverter
    {
        public static readonly Negate Instance = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => -(double)value;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => -(double)value;
    }

    private bool PopupAcrylicOn() =>
        HasMaterials && ApplicationThemeManager.GetAppTheme() != ApplicationTheme.HighContrast
        && _ui.Backdrop != WindowBackdrop.Solid && TransparencyEffectsOn();

    // Marks a drop-down whose popup is set up.
    private sealed class PopupAcrylic
    {
        public static readonly PopupAcrylic Instance = new();
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

        // High contrast always gets a plain background.
        WindowBackdrop backdrop = theme == ApplicationTheme.HighContrast || !HasMaterials
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
