using System.Windows;
using System.Windows.Media.Animation;

namespace DisplayHub.Windows;

public partial class ProfileFlyoutWindow : Window
{
    private CancellationTokenSource _cts = new();
    private bool _isHiding = true;
    private const int DisplayDurationMs = 1800;
    private const int AnimationDurationMs = 250;

    public ProfileFlyoutWindow()
    {
        InitializeComponent();
        WindowStartupLocation = WindowStartupLocation.Manual;
        Top = -9999;
        Opacity = 0;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
    }

    private void PositionWindow()
    {
        Left = (SystemParameters.WorkArea.Width / 2) - (Width / 2);
    }

    public async void ShowProfileFlyout(string profileName, bool isActive)
    {
        ProfileNameText.Text = profileName;

        // Accent bar visibility based on active state
        AccentBar.Opacity = isActive ? 1.0 : 0.3;

        PositionWindow();

        if (_isHiding)
        {
            _isHiding = false;
            // Slide up from below workarea
            double targetTop = SystemParameters.WorkArea.Height - Height - 8;
            Top = SystemParameters.WorkArea.Height;
            Opacity = 0;
            Show();

            var moveAnim = new DoubleAnimation(targetTop, TimeSpan.FromMilliseconds(AnimationDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fadeAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(AnimationDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(TopProperty, moveAnim);
            BeginAnimation(OpacityProperty, fadeAnim);
        }

        // Reset timer
        _cts.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            await Task.Delay(DisplayDurationMs, token);

            // Slide down to hide
            _isHiding = true;
            double hideTarget = SystemParameters.WorkArea.Height;
            var moveOut = new DoubleAnimation(hideTarget, TimeSpan.FromMilliseconds(AnimationDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(AnimationDurationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            BeginAnimation(TopProperty, moveOut);
            BeginAnimation(OpacityProperty, fadeOut);

            await Task.Delay(AnimationDurationMs);
            if (_isHiding)
            {
                Hide();
                Top = -9999;
            }
        }
        catch (TaskCanceledException)
        {
            // New profile switch came in, flyout stays visible
        }
    }
}
