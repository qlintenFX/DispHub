using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using DispHub.Platform.Shell;
using DispHub.Services;
using DispHub.ViewModels;

namespace DispHub;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly AppController _app;
    private readonly ThemeService _theme;
    private readonly OsdService _osd;

    public MainWindow(MainViewModel viewModel, AppController app, ThemeService theme, OsdService osd, ReviewPrompt review)
    {
        _app = app;
        _theme = theme;
        _osd = osd;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => _theme.Attach(this, _app.Config.Ui.Theme);
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
        _osd.ShowHint("DispHub is still running", "Find it in the notification area");
        MemoryTrim.Schedule();
    }
}
