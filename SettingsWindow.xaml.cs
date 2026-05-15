// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Pages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace DispHub;

public partial class SettingsWindow : FluentWindow
{
    private ScrollViewer? _contentScrollViewer;
    private readonly Action<bool> _powerChangedHandler;

    public SettingsWindow()
    {
        InitializeComponent();
        _powerChangedHandler = _ => Dispatcher.Invoke(UpdatePowerButtonVisual);
        Loaded += (_, _) => UpdatePowerButtonVisual();
        MainWindow.DisplayPowerChanged += _powerChangedHandler;
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Apply theme first, then accent color
        ApplyTheme(MainWindow.SettingsManager.AppTheme);
        // Small delay to ensure theme is applied before accent
        Dispatcher.BeginInvoke(() => SettingsPage.ApplyAccentColor(MainWindow.SettingsManager.AccentColor), 
            System.Windows.Threading.DispatcherPriority.Loaded);
        RootNavigation.Navigated += (_, _) => ResetScrollPosition();
        RootNavigation.Navigate(typeof(HomePage));
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        MainWindow.DisplayPowerChanged -= _powerChangedHandler;

        if (MainWindow.SettingsManager.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        MainWindow.DisplayManager?.ResetToDefault();
        MainWindow.DisplayManager?.Dispose();
        MainWindow.HotkeyManager?.Dispose();
        Application.Current.Shutdown();
    }

    private void PowerToggle_Click(object sender, RoutedEventArgs e)
    {
        MainWindow.ToggleDisplayPower();
    }

    private void UpdatePowerButtonVisual()
    {
        bool active = MainWindow.IsDisplayActive;
        PowerButton.ToolTip = active
            ? "DispHub: Active — click to disable"
            : "DispHub: Inactive — click to enable";

        if (active)
        {
            PowerIcon.Symbol = SymbolRegular.Power24;
            PowerIcon.Foreground = (Brush)FindResource("TextFillColorPrimaryBrush");
            PowerButton.Opacity = 1.0;
        }
        else
        {
            PowerIcon.Symbol = SymbolRegular.Power24;
            PowerIcon.Foreground = Brushes.OrangeRed;
            PowerButton.Opacity = 0.85;
        }
    }

    private void ResetScrollPosition()
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (_contentScrollViewer == null)
                    _contentScrollViewer = FindContentScrollViewer(RootNavigation);

                _contentScrollViewer?.ScrollToVerticalOffset(0);
            }
            catch { }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static ScrollViewer? FindContentScrollViewer(DependencyObject root)
    {
        var contentPresenter = FindDescendant<NavigationViewContentPresenter>(root);
        return contentPresenter == null ? null : FindDescendant<ScrollViewer>(contentPresenter);
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var nested = FindDescendant<T>(child);
            if (nested != null)
                return nested;
        }

        return null;
    }

    public static void ApplyTheme(int theme)
    {
        switch (theme)
        {
            case 1:
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                    Wpf.Ui.Appearance.ApplicationTheme.Light,
                    updateAccent: false
                );
                break;

            case 2:
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                    Wpf.Ui.Appearance.ApplicationTheme.Dark,
                    updateAccent: false
                );
                break;

            default:
                // Follow Windows light/dark while preserving current accent selection.
                Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme(updateAccent: false);
                break;
        }
    }
}
