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
        ApplyTheme(MainWindow.SettingsManager.AppTheme);
        RootNavigation.Navigate(typeof(HomePage));
        RootNavigation.Navigated += (_, _) => ResetScrollPosition();
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
            ? "Display Power: ON — click to turn off"
            : "Display Power: OFF — click to turn on";

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
                _contentScrollViewer ??= FindScrollableScrollViewer(RootNavigation);
                _contentScrollViewer?.ScrollToVerticalOffset(0);
            }
            catch { }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static ScrollViewer? FindScrollableScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv && sv.ScrollableHeight > 0)
                return sv;
            var result = FindScrollableScrollViewer(child);
            if (result != null) return result;
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
