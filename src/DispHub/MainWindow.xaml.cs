// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform.Shell;
using DispHub.Services;
using DispHub.ViewModels;
using Wpf.Ui.Controls;

namespace DispHub;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly AppController _app;
    private readonly ThemeService _theme;
    private bool _rendered;
    private bool _placed;
    private bool _tourPending;
    private IReadOnlyList<SearchResult>? _searchIndex;

    /// <summary>Below this width the navigation shows icons only.</summary>
    private const double NarrowWidth = 960;

    public MainWindow(MainViewModel viewModel, AppController app, ThemeService theme, ReviewPrompt review, ProTrial trial)
    {
        _app = app;
        _theme = theme;
        DataContext = viewModel;
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _theme.Attach(this);
            Place();
        };
        LocationChanged += (_, _) => Remember();
        StateChanged += (_, _) => Remember();
        ContentRendered += (_, _) =>
        {
            _rendered = true;
            if (_tourPending)
            {
                _tourPending = false;
                Dispatcher.BeginInvoke(BeginTour, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        };
        SizeChanged += (_, e) =>
        {
            viewModel.SetNarrow(e.NewSize.Width < NarrowWidth);
            Remember();
        };
        viewModel.Home.TourRequested += (_, _) => StartTour();
        TourOverlay.PageRequested += (_, page) =>
            viewModel.SelectedPage = viewModel.AllPages.FirstOrDefault(p => p.GetType() == page) ?? viewModel.SelectedPage;
        // The tour ends where it started.
        TourOverlay.Finished += (_, _) => viewModel.Navigate<HomeViewModel>();
        Activated += async (_, _) =>
        {
            viewModel.OnActivated();
            await trial.OnWindowActivatedAsync();
            await review.OnWindowActivatedAsync(this);
        };
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedPage))
                ScrollToTop();
        };
    }

    /// <summary>
    /// A new page starts at its top, without the last page's scroll still gliding. Smooth scrolling is switched off
    /// and on around the jump, so it starts again from the top rather than from where the glide was heading.
    /// </summary>
    private void ScrollToTop()
    {
        PageScroller.BeginAnimation(SmoothScrollBehavior.AnimatedVerticalOffsetProperty, null);
        bool smooth = SmoothScrollBehavior.GetIsEnabled(PageScroller);
        if (smooth)
            PageScroller.SetCurrentValue(SmoothScrollBehavior.IsEnabledProperty, false);
        PageScroller.ScrollToVerticalOffset(0);
        if (smooth)
            PageScroller.SetCurrentValue(SmoothScrollBehavior.IsEnabledProperty, true);
    }

    /// <summary>The guided tour of this window, on the Home page.</summary>
    public void StartTour()
    {
        ShowAndActivate();
        ((MainViewModel)DataContext).Navigate<HomeViewModel>();
        // Once the window and the Home page are on screen: on a first showing that's after the first render.
        if (_rendered)
            Dispatcher.BeginInvoke(BeginTour, System.Windows.Threading.DispatcherPriority.ContextIdle);
        else
            _tourPending = true;
    }

    /// <summary>The important parts, page by page.</summary>
    private void BeginTour() => TourOverlay.Start(
    [
        new("power", Loc.Get("Tour_PowerTitle"), Loc.Get("Tour_PowerText"), typeof(HomeViewModel)),
        new("now", Loc.Get("Tour_NowTitle"), Loc.Get("Tour_NowText"), typeof(HomeViewModel)),
        new("profiles", Loc.Get("Tour_ProfilesTitle"), Loc.Get("Tour_ProfilesText"), typeof(ProfilesViewModel)),
        new("schedule", Loc.Get("Tour_ScheduleTitle"), Loc.Get("Tour_ScheduleText"), typeof(ScheduleViewModel)),
        new("apps", Loc.Get("Tour_AppsTitle"), Loc.Get("Tour_AppsText"), typeof(AppsViewModel)),
        new("hotkeys", Loc.Get("Tour_HotkeysTitle"), Loc.Get("Tour_HotkeysText"), typeof(SettingsViewModel)),
    ], this);

    private void OnSearchFocused(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e) =>
        _searchIndex ??= SettingsSearch.Index(((MainViewModel)DataContext).AllPages);

    private void OnSearchTextChanged(Wpf.Ui.Controls.AutoSuggestBox sender, Wpf.Ui.Controls.AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != Wpf.Ui.Controls.AutoSuggestionBoxTextChangeReason.UserInput)
            return;
        // Otherwise the box filters its own (empty) list afterwards and the results vanish.
        args.Handled = true;
        _searchIndex ??= SettingsSearch.Index(((MainViewModel)DataContext).AllPages);
        IReadOnlyList<SearchResult> results = SettingsSearch.Find(_searchIndex, sender.Text);
        sender.ItemsSource = results;
        sender.IsSuggestionListOpen = results.Count > 0;
    }

    private void OnSearchSubmitted(Wpf.Ui.Controls.AutoSuggestBox sender, Wpf.Ui.Controls.AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (_searchIndex is not null && SettingsSearch.Find(_searchIndex, args.QueryText) is [var best, ..])
            Open(best);
    }

    private void OnSearchChosen(Wpf.Ui.Controls.AutoSuggestBox sender, Wpf.Ui.Controls.AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchResult result)
            Open(result);
    }

    /// <summary>Shows the result's page, and scrolls to its setting and lights it up.</summary>
    private void Open(SearchResult result)
    {
        SearchBox.IsSuggestionListOpen = false;
        SearchBox.Text = "";
        ((MainViewModel)DataContext).SelectedPage = result.Page;
        if (result.Setting is not { } setting)
            return;
        Dispatcher.BeginInvoke(() =>
        {
            if (FindCard(PageScroller, setting) is { } card)
            {
                // With room around it, so it doesn't land half hidden at the window's edge.
                card.BringIntoView(new Rect(0, -120, card.ActualWidth, card.ActualHeight + 240));
                TourOverlay.Highlight(card);
            }
        }, System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private static Controls.SettingsCard? FindCard(DependencyObject parent, string header)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Controls.SettingsCard { IsVisible: true } card && card.Header == header)
                return card;
            if (FindCard(child, header) is { } found)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Opens where it was last time, made to fit if the screen changed; the first time, at its own size if that
    /// fits the monitor (at most <see cref="WindowFit.MaxShare"/> of it), centred, as Windows' own apps open.
    /// </summary>
    private void Place()
    {
        nint handle = new WindowInteropHelper(this).Handle;
        WindowBounds? saved = _app.Config.Ui.Window;
        Rectangle? last = saved is null ? null : new Rectangle(saved.Left, saved.Top, saved.Width, saved.Height);
        (Rectangle work, double scale) = WindowHelpers.WorkAreaFor(last);
        if (work.IsEmpty)
            return;

        // On a small screen the window still has to fit, below its usual minimum if need be.
        MinWidth = Math.Min(MinWidth, work.Width / scale);
        MinHeight = Math.Min(MinHeight, work.Height / scale);
        var minimum = new System.Drawing.Size((int)Math.Ceiling(MinWidth * scale), (int)Math.Ceiling(MinHeight * scale));
        Rectangle bounds = last is { } r
            ? WindowFit.Restore(r, work, minimum)
            : WindowFit.FirstOpen(work, new System.Drawing.Size((int)(Width * scale), (int)(Height * scale)), minimum);
        WindowHelpers.Move(handle, bounds);
        if (saved?.Maximized == true)
            WindowState = WindowState.Maximized;
        _placed = true;
    }

    /// <summary>Keeps the normal bounds and whether it's maximised, for next time.</summary>
    private void Remember()
    {
        if (!_placed || !IsVisible || WindowState == WindowState.Minimized)
            return;
        bool maximized = WindowState == WindowState.Maximized;
        WindowBounds? saved = _app.Config.Ui.Window;
        if (maximized)
        {
            // While maximised the window covers the screen; its normal bounds are the ones from before.
            if (saved is not null)
                _app.RememberWindow(saved with { Maximized = true });
            return;
        }
        Rectangle bounds = WindowHelpers.Bounds(new WindowInteropHelper(this).Handle);
        if (!bounds.IsEmpty)
            _app.RememberWindow(new WindowBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height, Maximized: false));
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
