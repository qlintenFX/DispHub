using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using DispHub.Localization;
using DispHub.Platform.Shell;
using DispHub.Services;
using DispHub.ViewModels;

namespace DispHub;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly AppController _app;
    private readonly ThemeService _theme;

    /// <summary>Below this width the navigation shows icons only.</summary>
    private const double NarrowWidth = 960;

    public MainWindow(MainViewModel viewModel, AppController app, ThemeService theme, ReviewPrompt review)
    {
        _app = app;
        _theme = theme;
        DataContext = viewModel;
        InitializeComponent();
        SourceInitialized += (_, _) => _theme.Attach(this);
        SizeChanged += (_, e) => viewModel.SetNarrow(e.NewSize.Width < NarrowWidth);
        viewModel.Home.TourRequested += (_, _) => StartTour();
        Activated += async (_, _) => await review.OnWindowActivatedAsync(this);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedPage))
                PageScroller.ResetToTop();
        };
    }

    /// <summary>The guided tour of this window, on the Home page.</summary>
    public void StartTour()
    {
        ShowAndActivate();
        var viewModel = (MainViewModel)DataContext;
        viewModel.Navigate<HomeViewModel>();
        // After the Home page is laid out.
        Dispatcher.BeginInvoke(() => TourOverlay.Start(
        [
            new("power", Loc.Get("Tour_PowerTitle"), Loc.Get("Tour_PowerText")),
            new("now", Loc.Get("Tour_NowTitle"), Loc.Get("Tour_NowText")),
            new("switch", Loc.Get("Tour_SwitchTitle"), Loc.Get("Tour_SwitchText")),
            new("pages", Loc.Get("Tour_PagesTitle"), Loc.Get("Tour_PagesText")),
            new("settings", Loc.Get("Tour_SettingsTitle"), Loc.Get("Tour_SettingsText")),
        ], this), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void ShowAndActivate()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        WindowHelpers.BringToFront(new WindowInteropHelper(this).Handle);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        if (!_app.Config.Ui.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            ((App)Application.Current).RequestExit();
            return;
        }

        e.Cancel = true;
        Hide();
        ((App)Application.Current).OnMainWindowHidden();
    }
}
