using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DispHub.Platform.Shell;
using DispHub.ViewModels;

namespace DispHub.Views;

/// <summary>
/// The quick panel from the notification area, placed next to the taskbar like Windows' own flyouts. It closes when
/// it loses focus or on Esc.
/// </summary>
public partial class QuickPanel : Wpf.Ui.Controls.FluentWindow
{
    private const double Gap = 12;
    private readonly QuickPanelViewModel _viewModel;
    private DateTime _hiddenAt;

    public QuickPanel(QuickPanelViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        viewModel.CloseRequested += (_, _) => HideAnimated();
        Deactivated += (_, _) => HideAnimated();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                HideAnimated();
        };
        SourceInitialized += (_, _) => WindowHelpers.MakeToolWindow(new WindowInteropHelper(this).Handle);
    }

    /// <summary>Shows the panel, or hides it when it's open (a second click on the icon).</summary>
    public void Toggle()
    {
        // Clicking the icon first deactivates the panel; don't reopen it straight away.
        if (IsVisible || DateTime.UtcNow - _hiddenAt < TimeSpan.FromMilliseconds(250))
        {
            HideAnimated();
            return;
        }
        _viewModel.Refresh();
        Opacity = 0;
        Show();
        UpdateLayout();

        (Rectangle monitor, Rectangle work) = WindowHelpers.ScreenUnderCursor();
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        int width = (int)Math.Ceiling(ActualWidth * scale);
        int height = (int)Math.Ceiling(ActualHeight * scale);
        int gap = (int)(Gap * scale);
        bool taskbarTop = work.Top > monitor.Top;
        bool taskbarLeft = work.Left > monitor.Left;
        int x = taskbarLeft ? work.Left + gap : work.Right - width - gap;
        int y = taskbarTop ? work.Top + gap : work.Bottom - height - gap;
        WindowHelpers.PlaceOnTop(new WindowInteropHelper(this).Handle, new Rectangle(x, y, width, height));
        Activate();

        // Slide in from the taskbar.
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(taskbarTop ? -16 : 16, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
    }

    private void HideAnimated()
    {
        if (!IsVisible)
            return;
        _hiddenAt = DateTime.UtcNow;
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(100));
        fade.Completed += (_, _) =>
        {
            Hide();
            BeginAnimation(OpacityProperty, null);
        };
        BeginAnimation(OpacityProperty, fade);
    }
}
