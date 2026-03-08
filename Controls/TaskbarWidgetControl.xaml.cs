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

    private static readonly Color GreenDot = Color.FromRgb(76, 175, 80);
    private static readonly Color OrangeDot = Color.FromRgb(255, 152, 0);
    private static readonly Color RedDot = Color.FromRgb(244, 67, 54);
    private static readonly Color BlueDot = Color.FromRgb(33, 150, 243);

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

    public void UpdateState(string profileName, bool isActive, bool dcMode)
    {
        if (!isActive)
        {
            ProfileText.Text = "Power Off";
            StatusDot.Fill = new SolidColorBrush(RedDot);
        }
        else if (dcMode)
        {
            ProfileText.Text = "Dynamic";
            StatusDot.Fill = new SolidColorBrush(BlueDot);
        }
        else
        {
            ProfileText.Text = profileName;
            StatusDot.Fill = new SolidColorBrush(GreenDot);
        }
    }

    private void Border_MouseEnter(object sender, MouseEventArgs e)
    {
        bool isDark = ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark;
        var hoverBrush = isDark
            ? new SolidColorBrush(Color.FromArgb(197, 255, 255, 255)) { Opacity = 0.075 }
            : new SolidColorBrush(Color.FromArgb(197, 0, 0, 0)) { Opacity = 0.06 };
        TopBorder.BorderBrush = isDark
            ? new SolidColorBrush(Color.FromArgb(93, 255, 255, 255)) { Opacity = 0.25 }
            : new SolidColorBrush(Color.FromArgb(93, 0, 0, 0)) { Opacity = 0.15 };

        var anim = new ColorAnimation(
            ((SolidColorBrush)hoverBrush).Color,
            TimeSpan.FromMilliseconds(120));
        ((SolidColorBrush)MainBorder.Background).BeginAnimation(
            SolidColorBrush.ColorProperty, anim);

        var opAnim = new DoubleAnimation(hoverBrush.Opacity, TimeSpan.FromMilliseconds(120));
        ((SolidColorBrush)MainBorder.Background).BeginAnimation(
            Brush.OpacityProperty, opAnim);
    }

    private void Border_MouseLeave(object sender, MouseEventArgs e)
    {
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        ((SolidColorBrush)MainBorder.Background).BeginAnimation(
            Brush.OpacityProperty, anim);
        TopBorder.BorderBrush = null;
    }

    private void Border_Click(object sender, MouseButtonEventArgs e)
    {
        WidgetClicked?.Invoke();
    }
}
