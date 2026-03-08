// SPDX-License-Identifier: GPL-3.0-or-later
using DisplayHub.Pages;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace DisplayHub;

public partial class SettingsWindow : FluentWindow
{
    private ScrollViewer? _contentScrollViewer;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdatePowerButtonVisual();
        MainWindow.DisplayPowerChanged += _ => Dispatcher.Invoke(UpdatePowerButtonVisual);
    }

    private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(MainWindow.SettingsManager.AppTheme);
        RootNavigation.Navigate(typeof(HomePage));

        RootNavigation.Navigated += (_, _) => ResetScrollPosition();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (MainWindow.SettingsManager.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        MainWindow.DisplayManager?.ResetToDefault();
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
            ? "Display Power: On (click to turn off)"
            : "Display Power: Off (click to turn on)";

        PowerIcon.Foreground = active
            ? (Brush)FindResource("TextFillColorPrimaryBrush")
            : Brushes.OrangeRed;

        PowerButton.Opacity = active ? 1.0 : 0.8;
    }

    /// <summary>
    /// Reset scroll to top on page navigation (FluentFlyout pattern).
    /// </summary>
    private void ResetScrollPosition()
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                _contentScrollViewer ??= FindScrollableScrollViewer(RootNavigation);
                _contentScrollViewer?.ScrollToVerticalOffset(0);
            }
            catch { /* ignore scroll reset failures */ }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Traverse visual tree to find NavigationView's internal ScrollViewer.
    /// </summary>
    private static ScrollViewer? FindScrollableScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv && sv.ScrollableHeight > 0)
                return sv;

            var result = FindScrollableScrollViewer(child);
            if (result != null)
                return result;
        }
        return null;
    }

    public static void ApplyTheme(int theme)
    {
        var appTheme = theme switch
        {
            1 => Wpf.Ui.Appearance.ApplicationTheme.Light,
            2 => Wpf.Ui.Appearance.ApplicationTheme.Dark,
            _ => Wpf.Ui.Appearance.ApplicationTheme.Unknown,
        };
        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(appTheme);
    }
}
