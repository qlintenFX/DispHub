using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DispHub.Controls;
using DispHub.Localization;
using DispHub.ViewModels;

namespace DispHub.Views;

/// <summary>
/// The walkthrough window. Moving between steps slides the old one out and the new one in; the tray step shows
/// left-click and right-click in turn, and the effects step sweeps between the photo before and after.
/// </summary>
public partial class OnboardingWindow : Wpf.Ui.Controls.FluentWindow
{
    private static readonly TimeSpan Slide = TimeSpan.FromMilliseconds(320);
    private readonly OnboardingViewModel _viewModel;
    private readonly DispatcherTimer _clicks = new() { Interval = TimeSpan.FromSeconds(2.6) };
    private bool _rightClick;
    private int _from;

    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        viewModel.Finished += (_, _) => Close();
        viewModel.PropertyChanging += OnStepChanging;
        viewModel.PropertyChanged += OnStepChanged;
        _clicks.Tick += (_, _) => ShowClick(!_rightClick);
        // Closing it early counts as seen: it isn't shown again.
        Closed += (_, _) =>
        {
            _clicks.Stop();
            viewModel.Complete(tour: false);
        };
    }

    private static bool Animate => !SmoothScrollViewer.ReduceMotion;

    /// <summary>Keeps a picture of the step that is about to go, to slide it away.</summary>
    private void OnStepChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName != nameof(OnboardingViewModel.Step) || !Animate || Step.ActualWidth <= 0)
            return;
        _from = _viewModel.Step;
        DpiScale dpi = VisualTreeHelper.GetDpi(Step);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(Step.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(Step.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(Step);
        bitmap.Freeze();
        Snapshot.Source = bitmap;
        Snapshot.Width = Step.ActualWidth;
        Snapshot.Height = Step.ActualHeight;
        Snapshot.Visibility = Visibility.Visible;
    }

    private void OnStepChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OnboardingViewModel.Step))
            return;
        UpdateLivePictures();
        if (Snapshot.Visibility != Visibility.Visible)
            return;
        // Forward: the old step leaves to the left and the new one comes from the right; back is the mirror.
        double direction = _viewModel.Step > _from ? 1 : -1;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var leave = new DoubleAnimation(0, -40 * direction, Slide) { EasingFunction = ease };
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fadeOut.Completed += (_, _) => Snapshot.Visibility = Visibility.Collapsed;
        Snapshot.RenderTransform.BeginAnimation(TranslateTransform.XProperty, leave);
        Snapshot.BeginAnimation(OpacityProperty, fadeOut);
        Step.RenderTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(40 * direction, 0, Slide) { EasingFunction = ease });
        Step.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, Slide) { EasingFunction = ease });
    }

    /// <summary>Starts or stops the moving pictures for the step on show.</summary>
    private void UpdateLivePictures()
    {
        if (_viewModel.IsTray)
        {
            ShowClick(false);
            if (Animate)
                _clicks.Start();
        }
        else
        {
            _clicks.Stop();
        }

        if (_viewModel.IsEffects && Animate)
        {
            // The line sweeps slowly from one side to the other and back.
            var sweep = new DoubleAnimation(70, 360, TimeSpan.FromSeconds(2.8))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Before.BeginAnimation(WidthProperty, sweep);
        }
        else
        {
            Before.BeginAnimation(WidthProperty, null);
        }
    }

    /// <summary>Shows what a left or a right click does, and lights up its row.</summary>
    private void ShowClick(bool right)
    {
        _rightClick = right;
        double to = right ? 1 : 0;
        if (Animate)
            RightClickPicture.BeginAnimation(OpacityProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(260)));
        else
            RightClickPicture.Opacity = to;
        ClickChip.Text = Loc.Get(right ? "Onboarding_RightClick" : "Onboarding_LeftClick");
        LeftClickRow.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, right ? "CardStrokeColorDefaultBrush" : "AccentFillColorDefaultBrush");
        RightClickRow.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, right ? "AccentFillColorDefaultBrush" : "CardStrokeColorDefaultBrush");
    }
}
