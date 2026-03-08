using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Appearance;

namespace DisplayHub.Controls;

public partial class TaskbarWidgetControl : UserControl
{
    public event Action? WidgetClicked;

    public TaskbarWidgetControl()
    {
        InitializeComponent();

        MainBorder.SizeChanged += (_, _) =>
        {
            MainBorder.Clip = new RectangleGeometry(
                new Rect(0, 0, MainBorder.ActualWidth, MainBorder.ActualHeight), 6, 6);
        };

        Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
    }

    public void UpdateState(string profileName, bool isActive, bool dcMode, bool backgroundBlur)
    {
        ProfileText.Text = dcMode ? "Dynamic" : profileName;

        // Text color: white when active, gray when powered off
        TextBrush.Color = isActive ? Colors.White : Color.FromRgb(140, 140, 140);

        // Background blur effect (semi-transparent background)
        if (backgroundBlur)
        {
            bool isDark = ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark;
            BackgroundBrush.Color = isDark
                ? Color.FromArgb(40, 255, 255, 255)
                : Color.FromArgb(30, 0, 0, 0);
        }
        else
        {
            BackgroundBrush.Color = Color.FromArgb(0, 0, 0, 0);
        }
    }

    private void Border_MouseEnter(object sender, MouseEventArgs e)
    {
        bool isDark = ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark;
        var hoverColor = isDark
            ? Color.FromArgb(30, 255, 255, 255)
            : Color.FromArgb(20, 0, 0, 0);

        var borderColor = isDark
            ? Color.FromArgb(60, 255, 255, 255)
            : Color.FromArgb(40, 0, 0, 0);

        var colorAnim = new ColorAnimation(hoverColor, TimeSpan.FromMilliseconds(120));
        BackgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
        TopBorder.BorderBrush = new SolidColorBrush(borderColor);
    }

    private void Border_MouseLeave(object sender, MouseEventArgs e)
    {
        bool blur = MainWindow.SettingsManager.TaskbarWidgetBackgroundBlur;
        bool isDark = ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark;

        var restColor = blur
            ? (isDark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0))
            : Color.FromArgb(0, 0, 0, 0);

        var colorAnim = new ColorAnimation(restColor, TimeSpan.FromMilliseconds(150));
        BackgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
        TopBorder.BorderBrush = null;
    }

    private void Border_Click(object sender, MouseButtonEventArgs e)
    {
        WidgetClicked?.Invoke();
    }
}
