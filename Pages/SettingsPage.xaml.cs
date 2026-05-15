// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DispHub.Constants;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace DispHub.Pages;

public partial class SettingsPage : Page, INavigationAware
{
    private bool _isLoaded;

    // Accent color options with name and brush
    public class AccentColorItem
    {
        public string Name { get; set; } = "";
        public Brush Color { get; set; } = Brushes.Transparent;
        public int Index { get; set; }
    }

    private static readonly AccentColorItem[] AccentColorItems =
    [
        new() { Index = 0, Name = "System", Color = new LinearGradientBrush(Colors.Orange, Colors.Purple, 0) },
        new() { Index = 1, Name = "Blue", Color = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)) },
        new() { Index = 2, Name = "Teal", Color = new SolidColorBrush(Color.FromRgb(0x00, 0x99, 0xBC)) },
        new() { Index = 3, Name = "Green", Color = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0x6A)) },
        new() { Index = 4, Name = "Yellow", Color = new SolidColorBrush(Color.FromRgb(0xFF, 0xB9, 0x00)) },
        new() { Index = 5, Name = "Orange", Color = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)) },
        new() { Index = 6, Name = "Red", Color = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23)) },
        new() { Index = 7, Name = "Magenta", Color = new SolidColorBrush(Color.FromRgb(0xEA, 0x00, 0x5E)) },
        new() { Index = 8, Name = "Purple", Color = new SolidColorBrush(Color.FromRgb(0x88, 0x17, 0x98)) },
        new() { Index = 9, Name = "Iris", Color = new SolidColorBrush(Color.FromRgb(0x6B, 0x69, 0xD6)) },
        new() { Index = 10, Name = "Brown", Color = new SolidColorBrush(Color.FromRgb(0x8E, 0x56, 0x2E)) },
        new() { Index = 11, Name = "Gray", Color = new SolidColorBrush(Color.FromRgb(0x76, 0x76, 0x76)) },
    ];

    // Color values for applying accent (must match AccentColorItems order)
    private static readonly Color[] AccentColors =
    [
        default,                           // 0: System
        Color.FromRgb(0x00, 0x78, 0xD4),   // 1: Blue
        Color.FromRgb(0x00, 0x99, 0xBC),   // 2: Teal
        Color.FromRgb(0x00, 0xCC, 0x6A),   // 3: Green
        Color.FromRgb(0xFF, 0xB9, 0x00),   // 4: Yellow
        Color.FromRgb(0xFF, 0x8C, 0x00),   // 5: Orange
        Color.FromRgb(0xE8, 0x11, 0x23),   // 6: Red
        Color.FromRgb(0xEA, 0x00, 0x5E),   // 7: Magenta
        Color.FromRgb(0x88, 0x17, 0x98),   // 8: Purple
        Color.FromRgb(0x6B, 0x69, 0xD6),   // 9: Iris
        Color.FromRgb(0x8E, 0x56, 0x2E),   // 10: Brown
        Color.FromRgb(0x76, 0x76, 0x76),   // 11: Gray
    ];

    public SettingsPage()
    {
        InitializeComponent();
        AccentColorComboBox.ItemsSource = AccentColorItems;
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        StartWithWindowsToggle.IsChecked = MainWindow.SettingsManager.StartWithWindows;
        ThemeComboBox.SelectedIndex = MainWindow.SettingsManager.AppTheme;
        TrayLeftClickComboBox.SelectedIndex = MainWindow.SettingsManager.TrayLeftClickBehavior;

        if (MainWindow.SettingsManager.CloseToTray)
            CloseToTrayRadio.IsChecked = true;
        else
            CloseAppRadio.IsChecked = true;

        UpdateMasterToggleLabel();

        // Set accent color selection
        int accentIndex = MainWindow.SettingsManager.AccentColor;
        if (accentIndex < 0 || accentIndex >= AccentColorItems.Length)
        {
            accentIndex = 0;
            MainWindow.SettingsManager.AccentColor = 0;
        }

        AccentColorComboBox.SelectedIndex = accentIndex;

        _isLoaded = true;
    }

    public Task OnNavigatedToAsync() => Task.CompletedTask;
    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        int theme = ThemeComboBox.SelectedIndex;
        MainWindow.SettingsManager.AppTheme = theme;
        SettingsWindow.ApplyTheme(theme);
        // Re-apply accent color after theme change
        ApplyAccentColor(MainWindow.SettingsManager.AccentColor);
    }

    private void AccentColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        int accentIndex = AccentColorComboBox.SelectedIndex;
        if (accentIndex < 0) return;

        MainWindow.SettingsManager.AccentColor = accentIndex;
        ApplyAccentColor(accentIndex);
    }

    /// <summary>
    /// Applies the accent color using WPF UI's ApplicationAccentColorManager.
    /// This properly updates all accent-related resources in the application.
    /// </summary>
    public static void ApplyAccentColor(int accentIndex)
    {
        if (accentIndex <= 0 || accentIndex >= AccentColors.Length)
        {
            ApplicationAccentColorManager.ApplySystemAccent();
        }
        else
        {
            var accentColor = AccentColors[accentIndex];
            // Keep custom accent exact across all accent resource variants.
            ApplicationAccentColorManager.Apply(accentColor, accentColor, accentColor, accentColor);
        }

        // Refresh configured theme resources without overriding accent.
        switch (MainWindow.SettingsManager.AppTheme)
        {
            case 1:
                ApplicationThemeManager.Apply(ApplicationTheme.Light, updateAccent: false);
                break;
            case 2:
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, updateAccent: false);
                break;
            default:
                ApplicationThemeManager.ApplySystemTheme(updateAccent: false);
                break;
        }
    }

    public static Color GetAccentColor(int accentIndex)
    {
        if (accentIndex <= 0 || accentIndex >= AccentColors.Length)
            return ApplicationAccentColorManager.GetColorizationColor();
        return AccentColors[accentIndex];
    }

    private void CloseBehavior_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.CloseToTray = CloseToTrayRadio.IsChecked == true;
    }

    private void StartWithWindows_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.StartWithWindows = StartWithWindowsToggle.IsChecked == true;
    }

    private void TrayLeftClick_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        MainWindow.SettingsManager.TrayLeftClickBehavior = TrayLeftClickComboBox.SelectedIndex;
    }

    private void MasterToggleKeybind_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() != true) return;

        MainWindow.SettingsManager.MasterToggleKey = (uint)dialog.VirtualKeyCode;
        MainWindow.SettingsManager.MasterToggleMod = dialog.Modifiers;
        UpdateMasterToggleLabel();

        if (Application.Current.MainWindow is MainWindow mw)
            mw.UpdateMasterToggleHotkey();
    }

    private void UpdateMasterToggleLabel()
    {
        var key = MainWindow.SettingsManager.MasterToggleKey;
        var mod = MainWindow.SettingsManager.MasterToggleMod;
        MasterToggleKeybind.Content = key == 0 ? "None" : FormatKeybind(key, mod);
    }

    private static string FormatKeybind(uint vk, uint mod)
    {
        if (vk == 0) return "None";
        var parts = new List<string>();
        if ((mod & AppConstants.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mod & AppConstants.MOD_ALT) != 0) parts.Add("Alt");
        if ((mod & AppConstants.MOD_SHIFT) != 0) parts.Add("Shift");
        Key key = KeyInterop.KeyFromVirtualKey((int)vk);
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }
}
