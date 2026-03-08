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

    public void UpdateState(string profileName, bool isActive, bool dcMode)
    {
        if (dcMode)
            ProfileText.Text = "Dynamic";
        else
            ProfileText.Text = profileName;

        // Power off = dim the text, power on = full white
        TextBrush.Color = isActive ? Colors.White : Color.FromRgb(140, 140, 140);
    }

    private void Border_MouseEnter(object sender, MouseEventArgs e)
    {
        bool isDark = ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark;
        var hoverColor = isDark
            ? Color.FromArgb(20, 255, 255, 255)
            : Color.FromArgb(15, 0, 0, 0);

        var borderColor = isDark
            ? Color.FromArgb(60, 255, 255, 255)
            : Color.FromArgb(40, 0, 0, 0);

        var colorAnim = new ColorAnimation(hoverColor, TimeSpan.FromMilliseconds(120));
        ((SolidColorBrush)MainBorder.Background).BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);

        var opAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120));
        ((SolidColorBrush)MainBorder.Background).BeginAnimation(Brush.OpacityProperty, opAnim);

        TopBorder.BorderBrush = new SolidColorBrush(borderColor);
    }

    private void Border_MouseLeave(object sender, MouseEventArgs e)
    {
        var colorAnim = new ColorAnimation(Color.FromArgb(0, 255, 255, 255), TimeSpan.FromMilliseconds(150));
        ((SolidColorBrush)MainBorder.Background).BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);

        var opAnim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        ((SolidColorBrush)MainBorder.Background).BeginAnimation(Brush.OpacityProperty, opAnim);

        TopBorder.BorderBrush = null;
    }

    private void Border_Click(object sender, MouseButtonEventArgs e)
    {
        WidgetClicked?.Invoke();
    }
}
