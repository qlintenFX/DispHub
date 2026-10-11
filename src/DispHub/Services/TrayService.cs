using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DispHub.Core.Model;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace DispHub.Services;

/// <summary>The notification area icon and its menu. Its parent is the hidden host window.</summary>
public sealed class TrayService : IDisposable
{
    private const int MaxMenuProfiles = 12;
    private readonly AppController _app;
    private readonly Action _openWindow;
    private readonly Action _exit;
    private readonly NotifyIcon _icon;

    public TrayService(AppController app, Action openWindow, Action exit)
    {
        _app = app;
        _openWindow = openWindow;
        _exit = exit;
        _icon = new NotifyIcon
        {
            Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/DispHub.ico")),
            FocusOnLeftClick = false,
            MenuOnRightClick = true,
            TooltipText = "DispHub",
        };
        _icon.LeftClick += OnLeftClick;
        _icon.LeftDoubleClick += OnLeftDoubleClick;
        _app.StateChanged += (_, _) => Refresh();
        _app.ConfigChanged += (_, _) => Refresh();
    }

    public void Show()
    {
        Refresh();
        _icon.Register();
    }

    /// <summary>Explorer restarted and forgot every tray icon; add ours back.</summary>
    public void Reregister()
    {
        _icon.Unregister();
        _icon.Register();
    }

    public void Dispose()
    {
        _icon.Unregister();
        _icon.Dispose();
    }

    private void OnLeftDoubleClick([NotNull] NotifyIcon sender, RoutedEventArgs e) => _openWindow();

    private void OnLeftClick([NotNull] NotifyIcon sender, RoutedEventArgs e)
    {
        if (_app.Config.Ui.TrayLeftClick == TrayClickAction.ShowProfileMenu && _icon.Menu is { } menu)
        {
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }
        else
        {
            _openWindow();
        }
    }

    private void Refresh()
    {
        Profile? active = _app.Current?.ProfileId is { } id ? _app.FindProfile(id) : null;
        _icon.TooltipText = _app.Config.IsPaused ? "DispHub · Paused" : $"DispHub · {active?.Name ?? "Ready"}";
        _icon.Menu = BuildMenu(active);
    }

    private ContextMenu BuildMenu(Profile? active)
    {
        var menu = new ContextMenu();
        foreach (Profile profile in _app.Config.Profiles.Take(MaxMenuProfiles))
        {
            Guid id = profile.Id;
            var item = new MenuItem
            {
                Header = profile.Name,
                IsCheckable = true,
                IsChecked = !_app.Config.IsPaused && active?.Id == id,
                InputGestureText = profile.Hotkey ?? "",
            };
            item.Click += (_, _) => _app.ActivateProfile(id, ChangeSource.Tray);
            menu.Items.Add(item);
        }
        if (_app.Config.Profiles.Count > MaxMenuProfiles)
            menu.Items.Add(Item("More profiles…", SymbolRegular.MoreHorizontal24, _openWindow));

        menu.Items.Add(new Separator());
        menu.Items.Add(_app.Config.IsPaused
            ? Item("Resume", SymbolRegular.Play24, () => _app.TogglePause(ChangeSource.Tray))
            : Item("Pause (original colours)", SymbolRegular.Pause24, () => _app.TogglePause(ChangeSource.Tray)));
        menu.Items.Add(Item("Open DispHub", SymbolRegular.Open24, _openWindow));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Exit", SymbolRegular.Dismiss24, _exit));
        return menu;
    }

    private static MenuItem Item(string header, SymbolRegular symbol, Action action)
    {
        var item = new MenuItem { Header = header, Icon = new SymbolIcon { Symbol = symbol } };
        item.Click += (_, _) => action();
        return item;
    }
}
