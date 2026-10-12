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
        Loaded += (_, _) => _theme.Attach(this, _app.Config.Ui);
        SizeChanged += (_, e) => viewModel.SetNarrow(e.NewSize.Width < NarrowWidth);
        Activated += async (_, _) => await review.OnWindowActivatedAsync(this);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedPage))
                PageScroller.ResetToTop();
        };
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
