// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

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
    private bool _rendered;
    private bool _tourPending;
    private IReadOnlyList<SearchResult>? _searchIndex;

    /// <summary>Below this width the navigation shows icons only.</summary>
    private const double NarrowWidth = 960;

    public MainWindow(MainViewModel viewModel, AppController app, ThemeService theme, ReviewPrompt review)
    {
        _app = app;
        _theme = theme;
        DataContext = viewModel;
        InitializeComponent();
        SourceInitialized += (_, _) => _theme.Attach(this);
        ContentRendered += (_, _) =>
        {
            _rendered = true;
            if (_tourPending)
            {
                _tourPending = false;
                Dispatcher.BeginInvoke(BeginTour, System.Windows.Threading.DispatcherPriority.ContextIdle);
            }
        };
        SizeChanged += (_, e) => viewModel.SetNarrow(e.NewSize.Width < NarrowWidth);
        viewModel.Home.TourRequested += (_, _) => StartTour();
        TourOverlay.PageRequested += (_, page) =>
            viewModel.SelectedPage = viewModel.AllPages.FirstOrDefault(p => p.GetType() == page) ?? viewModel.SelectedPage;
        // The tour ends where it started.
        TourOverlay.Finished += (_, _) => viewModel.Navigate<HomeViewModel>();
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
