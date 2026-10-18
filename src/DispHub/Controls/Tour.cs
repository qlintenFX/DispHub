using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DispHub.Localization;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;

namespace DispHub.Controls;

/// <summary>Marks the parts of a window the tour points at.</summary>
public static class Tour
{
    public static readonly DependencyProperty StopProperty = DependencyProperty.RegisterAttached(
        "Stop", typeof(string), typeof(Tour), new PropertyMetadata(null));

    public static string? GetStop(DependencyObject element) => (string?)element?.GetValue(StopProperty);

    public static void SetStop(DependencyObject element, string? value) => element?.SetValue(StopProperty, value);

    /// <summary>The visible element marked <paramref name="stop"/> inside <paramref name="root"/>, if any.</summary>
    public static FrameworkElement? Find(DependencyObject root, string stop)
    {
        ArgumentNullException.ThrowIfNull(root);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { IsVisible: true } element && GetStop(element) == stop)
                return element;
            if (Find(child, stop) is { } found)
                return found;
        }
        return null;
    }
}

/// <summary>One stop of the tour: the part to point at (see <see cref="Tour.StopProperty"/>) and what to say.</summary>
public sealed record TourStep(string Stop, string Title, string Text);

/// <summary>
/// A guided tour over a window: everything dims except the part being explained, which glows with the accent colour
/// while a card beside it says what it does. The light moves from part to part. The highlighted part still works,
/// so it can be tried right away. Esc ends the tour; the arrow keys and Enter move through it.
/// </summary>
public sealed class TourOverlay : Canvas
{
    private const double Gap = 6;
    private const double Spacing = 12;
    private static readonly TimeSpan Glide = TimeSpan.FromMilliseconds(320);

    private readonly RectangleGeometry _hole = new() { RadiusX = 8, RadiusY = 8 };
    private readonly RectangleGeometry _all = new();
    private readonly System.Windows.Shapes.Path _dim;
    private readonly Border _ring;
    private readonly Border _card;
    private readonly TextBlock _count = new();
    private readonly TextBlock _title = new();
    private readonly TextBlock _text = new();
    private readonly Button _back = new();
    private readonly Button _next = new();
    private IReadOnlyList<TourStep> _steps = [];
    private DependencyObject? _root;
    private FrameworkElement? _target;
    private int _index;
    private bool _highlightOnly;
    private Rect _from;
    private Rect _shown;
    private DateTime _movedAt;

    public TourOverlay()
    {
        Visibility = Visibility.Collapsed;
        Focusable = true;
        _dim = new System.Windows.Shapes.Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)),
            Data = new CombinedGeometry(GeometryCombineMode.Exclude, _all, _hole),
        };
        _ring = new Border
        {
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
        };
        _ring.SetResourceReference(Border.BorderBrushProperty, "AccentFillColorDefaultBrush");
        _ring.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 };

        _count.SetResourceReference(StyleProperty, "Hint");
        _title.FontSize = 16;
        _title.FontWeight = FontWeights.SemiBold;
        _title.Margin = new Thickness(0, 2, 0, 6);
        _title.TextWrapping = TextWrapping.Wrap;
        _title.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _text.TextWrapping = TextWrapping.Wrap;
        _text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

        var skip = new Button { Content = Loc.Get("Tour_Skip"), Appearance = ControlAppearance.Transparent };
        skip.Click += (_, _) => Finish();
        _back.Content = Loc.Get("Tour_Back");
        _back.Margin = new Thickness(0, 0, 8, 0);
        _back.Click += (_, _) => Show(_index - 1);
        _next.Appearance = ControlAppearance.Primary;
        _next.Click += (_, _) => Show(_index + 1);
        var buttons = new DockPanel { Margin = new Thickness(0, 16, 0, 0), LastChildFill = false };
        DockPanel.SetDock(skip, Dock.Left);
        DockPanel.SetDock(_next, Dock.Right);
        DockPanel.SetDock(_back, Dock.Right);
        buttons.Children.Add(skip);
        buttons.Children.Add(_next);
        buttons.Children.Add(_back);

        _card = new Border
        {
            Width = 320,
            Padding = new Thickness(20, 16, 20, 16),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 4, Opacity = 0.35 },
            Child = new StackPanel { Children = { _count, _title, _text, buttons } },
        };
        _card.SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorTertiaryBrush");
        _card.SetResourceReference(Border.BorderBrushProperty, "SurfaceStrokeColorFlyoutBrush");

        Children.Add(_dim);
        Children.Add(_ring);
        Children.Add(_card);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
                CompositionTarget.Rendering += OnRendering;
            else
                CompositionTarget.Rendering -= OnRendering;
        };
    }

    /// <summary>The tour ended, finished or skipped.</summary>
    public event EventHandler? Finished;

    public bool IsRunning => Visibility == Visibility.Visible;

    /// <summary>Starts the tour over the elements inside <paramref name="root"/>. Steps whose part isn't shown are skipped.</summary>
    public void Start(IReadOnlyList<TourStep> steps, DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(steps);
        _steps = steps;
        _root = root;
        _target = null;
        _highlightOnly = false;
        _dim.Visibility = Visibility.Visible;
        _card.Visibility = Visibility.Visible;
        _ring.BeginAnimation(OpacityProperty, null);
        _ring.Opacity = 1;
        _shown = Rect.Empty;
        _index = 0;
        if (TryFindResource("SystemAccentColorSecondary") is Color accent)
            ((DropShadowEffect)_ring.Effect).Color = accent;
        Visibility = Visibility.Visible;
        Focus();
        Show(0);
    }

    /// <summary>Lights up <paramref name="element"/> for a moment, without dimming anything (a search result).</summary>
    public void Highlight(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (IsRunning && !_highlightOnly)
            return;
        _highlightOnly = true;
        _target = element;
        _shown = Rect.Empty;
        _dim.Visibility = Visibility.Collapsed;
        _card.Visibility = Visibility.Collapsed;
        Visibility = Visibility.Visible;
        if (TryFindResource("SystemAccentColorSecondary") is Color accent)
            ((DropShadowEffect)_ring.Effect).Color = accent;
        var fade = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(1, TimeSpan.Zero));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(1, TimeSpan.FromSeconds(1.4)));
        fade.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0, TimeSpan.FromSeconds(2)));
        fade.Completed += (_, _) =>
        {
            if (!_highlightOnly)
                return;
            Visibility = Visibility.Collapsed;
            _target = null;
        };
        _ring.BeginAnimation(OpacityProperty, fade);
    }

    public void Finish()
    {
        if (!IsRunning || _highlightOnly)
            return;
        Visibility = Visibility.Collapsed;
        _target = null;
        Finished?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnKeyDown(e);
        if (_highlightOnly)
            return;
        switch (e.Key)
        {
            case Key.Escape:
                Finish();
                break;
            case Key.Right or Key.Enter or Key.Space:
                Show(_index + 1);
                break;
            case Key.Left:
                Show(_index - 1);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void Show(int index)
    {
        if (index < 0)
            return;
        if (index >= _steps.Count)
        {
            Finish();
            return;
        }
        // Skip parts that aren't on screen, in the direction of travel.
        int step = index >= _index ? 1 : -1;
        FrameworkElement? target = null;
        while (index >= 0 && index < _steps.Count && (target = Tour.Find(_root!, _steps[index].Stop)) is null)
            index += step;
        if (target is null)
        {
            if (index >= _steps.Count)
                Finish();
            return;
        }

        _index = index;
        _target = target;
        target.BringIntoView();
        _from = _shown;
        _movedAt = DateTime.UtcNow;

        TourStep current = _steps[index];
        _count.Text = Loc.Format("Tour_Count", index + 1, _steps.Count);
        _title.Text = current.Title;
        _text.Text = current.Text;
        _back.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
        _next.Content = Loc.Get(index == _steps.Count - 1 ? "Tour_Done" : "Tour_Next");
        Focus();
    }

    /// <summary>Follows the part every frame, so scrolling and resizing keep it lit; glides when moving on.</summary>
    private void OnRendering(object? sender, EventArgs e)
    {
        if (_target is null || !_target.IsVisible || ActualWidth <= 0)
            return;
        Rect goal;
        try
        {
            goal = _target.TransformToVisual(this).TransformBounds(new Rect(_target.RenderSize));
        }
        catch (InvalidOperationException)
        {
            // The part left the window (its page changed).
            return;
        }
        goal.Inflate(Gap, Gap);

        double t = SmoothScrollViewer.ReduceMotion || _from.IsEmpty ? 1
            : Math.Clamp((DateTime.UtcNow - _movedAt).TotalMilliseconds / Glide.TotalMilliseconds, 0, 1);
        double ease = 1 - Math.Pow(1 - t, 3);
        _shown = t >= 1 ? goal : new Rect(
            Lerp(_from.X, goal.X, ease), Lerp(_from.Y, goal.Y, ease),
            Lerp(_from.Width, goal.Width, ease), Lerp(_from.Height, goal.Height, ease));

        _all.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
        _hole.Rect = _shown;
        SetLeft(_ring, _shown.X);
        SetTop(_ring, _shown.Y);
        _ring.Width = _shown.Width;
        _ring.Height = _shown.Height;
        if (!_highlightOnly)
            PlaceCard(goal);
    }

    /// <summary>Puts the card below the part, or above, or beside it, whichever fits; always inside the window.</summary>
    private void PlaceCard(Rect part)
    {
        _card.Measure(new Size(_card.Width, double.PositiveInfinity));
        Size card = _card.DesiredSize;
        double x, y;
        if (part.Bottom + Spacing + card.Height <= ActualHeight)
            (x, y) = (part.Left, part.Bottom + Spacing);
        else if (part.Top - Spacing - card.Height >= 0)
            (x, y) = (part.Left, part.Top - Spacing - card.Height);
        else if (part.Right + Spacing + card.Width <= ActualWidth)
            (x, y) = (part.Right + Spacing, part.Top);
        else
            (x, y) = (part.Left - Spacing - card.Width, part.Top);
        SetLeft(_card, Math.Clamp(x, Spacing, Math.Max(Spacing, ActualWidth - card.Width - Spacing)));
        SetTop(_card, Math.Clamp(y, Spacing, Math.Max(Spacing, ActualHeight - card.Height - Spacing)));
    }

    private static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
