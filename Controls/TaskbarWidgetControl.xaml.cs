using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DisplayHub.Controls;

public partial class TaskbarWidgetControl : UserControl
{
    public event Action? WidgetClicked;

    public TaskbarWidgetControl()
    {
        InitializeComponent();
    }

    public void UpdateProfile(string profileName, bool isActive)
    {
        ProfileText.Text = isActive ? profileName : "Off";
        StatusDot.Fill = isActive
            ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // green
            : new SolidColorBrush(Color.FromRgb(255, 87, 34));   // orange-red
    }

    private void WidgetBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        var anim = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(150));
        WidgetBorder.Background.BeginAnimation(Brush.OpacityProperty, anim);
    }

    private void WidgetBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        var anim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(150));
        WidgetBorder.Background.BeginAnimation(Brush.OpacityProperty, anim);
    }

    private void WidgetBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        WidgetClicked?.Invoke();
    }
}
