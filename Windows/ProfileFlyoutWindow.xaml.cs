// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Media.Animation;

namespace DispHub.Windows;

public partial class ProfileFlyoutWindow : Window
{
    private CancellationTokenSource? _cts;
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
        Left = (SystemParameters.WorkArea.Width - Width) / 2;
    }

    public async Task ShowProfileFlyout(string profileName)
    {
        ProfileNameText.Text = profileName;
        PositionWindow();

        if (_isHiding)
        {
            _isHiding = false;
            double targetTop = SystemParameters.WorkArea.Height - Height - 12;
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

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            int duration = MainWindow.SettingsManager.FlyoutDuration;
            await Task.Delay(duration, token);

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
        catch (TaskCanceledException) { }
    }
}
