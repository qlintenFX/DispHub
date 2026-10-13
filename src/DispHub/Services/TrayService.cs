using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform.Shell;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;
using MenuItem = System.Windows.Controls.MenuItem;

namespace DispHub.Services;

/// <summary>The notification area icon and its menu. Messages arrive through the hidden host window.</summary>
public sealed class TrayService : IDisposable
{
    private const int MaxMenuProfiles = 12;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    private readonly AppController _app;
    private readonly nint _host;
    private readonly Action _openWindow;
    private readonly Action _exit;
    private readonly ILogger _logger;
    private readonly TrayIcon _icon;
    private readonly byte[] _normalIcon = LoadResource("Assets/DispHub.ico");
    private readonly byte[] _pausedIcon = LoadResource("Assets/DispHub-paused.ico");
    private readonly DispatcherTimer _retry = new() { Interval = RetryInterval };
    private readonly Func<Views.QuickPanel> _createPanel;
    private Views.QuickPanel? _panel;
    private int _retries;
    private bool _paused;

    public TrayService(AppController app, ShellMessageRouter router, nint hostWindow, Action openWindow, Action exit, Func<Views.QuickPanel> createPanel, ILogger logger)
    {
        _createPanel = createPanel;
        ArgumentNullException.ThrowIfNull(router);
        _app = app;
        _host = hostWindow;
        _openWindow = openWindow;
        _exit = exit;
        _logger = logger;
        _icon = new TrayIcon(hostWindow, _normalIcon, logger);
        _icon.Action += OnAction;
        router.Tray = _icon;
        router.TaskbarCreated += (_, _) => Show();
        router.DpiChanged += (_, _) => _icon.RefreshIcon();
        router.DisplayChanged += (_, _) => _icon.RefreshIcon();
        _retry.Tick += (_, _) => Show();
        _app.StateChanged += (_, _) => Refresh();
        _app.ConfigChanged += (_, _) => Refresh();
        _app.QuickPanelRequested += (_, _) => (_panel ??= _createPanel()).Toggle();
    }

    /// <summary>Adds the icon. Right after sign-in the taskbar may not be ready yet, so it keeps trying for a minute.</summary>
    public void Show()
    {
        _paused = !_app.Config.IsPaused; // Forces the icon choice below.
        if (_icon.Show())
        {
            _retry.Stop();
            _retries = 0;
            Refresh();
            return;
        }
        if (++_retries <= 30)
            _retry.Start();
        else
            _retry.Stop();
    }

    /// <summary>
    /// Tells the user where DispHub went when its window closes. The first time it's a Windows notification that
    /// opens the taskbar settings; later only if the icon is hidden in the overflow area.
    /// </summary>
    public void ShowStillRunningHint(OsdService osd)
    {
        ArgumentNullException.ThrowIfNull(osd);
        if (!_app.Config.Ui.TrayHintShown && _icon.IsAdded)
        {
            _icon.ShowNotification(Loc.Get("Tray_StillRunningTitle"), Loc.Get("Tray_StillRunningText"));
            _app.Config.Ui.TrayHintShown = true;
            _app.SettingsChanged();
        }
        // Notifications don't show during Do not disturb, so also say it on screen while the icon is hidden.
        if (TrayIcon.IsOnTaskbar() != true)
            osd.ShowHint(Loc.Get("Osd_StillRunning"), Loc.Get("Osd_StillRunningHint"));
    }

    public void Dispose()
    {
        _retry.Stop();
        _icon.Dispose();
    }

    private void OnAction(object? sender, TrayIconAction action)
    {
        switch (action)
        {
            case TrayIconAction.Select when _app.Config.Ui.TrayLeftClick == TrayClickAction.QuickPanel:
                (_panel ??= _createPanel()).Toggle();
                break;
            case TrayIconAction.Select when _app.Config.Ui.TrayLeftClick == TrayClickAction.ShowProfileMenu:
            case TrayIconAction.ContextMenu:
                ShowMenu();
                break;
            case TrayIconAction.Select:
            case TrayIconAction.DoubleClick:
                _openWindow();
                break;
            case TrayIconAction.NotificationClicked:
                OpenTaskbarSettings();
                break;
        }
    }

    private void Refresh()
    {
        Profile? active = _app.Current?.ProfileId is { } id ? _app.FindProfile(id) : null;
        _icon.SetTooltip(_app.Config.IsPaused ? Loc.Get("Tray_Paused") : Loc.Format("Tray_Active", active?.Name ?? Loc.Get("Tray_Ready")));
        if (_paused != _app.Config.IsPaused)
        {
            _paused = _app.Config.IsPaused;
            _icon.SetIcon(_paused ? _pausedIcon : _normalIcon);
        }
    }

    private void ShowMenu()
    {
        // The menu only closes when clicking elsewhere if DispHub owns the foreground; clicking the icon allows that.
        WindowHelpers.BringToFront(_host);
        ContextMenu menu = BuildMenu();
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private ContextMenu BuildMenu()
    {
        Profile? active = _app.Current?.ProfileId is { } activeId ? _app.FindProfile(activeId) : null;
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
            menu.Items.Add(Item(Loc.Get("Tray_MoreProfiles"), SymbolRegular.MoreHorizontal24, _openWindow));

        menu.Items.Add(new Separator());
        menu.Items.Add(_app.Config.IsPaused
            ? Item(Loc.Get("Tray_Resume"), SymbolRegular.Power24, () => _app.TogglePause(ChangeSource.Tray))
            : Item(Loc.Get("Tray_Pause"), SymbolRegular.Power24, () => _app.TogglePause(ChangeSource.Tray)));
        menu.Items.Add(Item(Loc.Get("Tray_Open"), SymbolRegular.Open24, _openWindow));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(Loc.Get("Tray_Exit"), SymbolRegular.Dismiss24, _exit));
        return menu;
    }

    private static MenuItem Item(string header, SymbolRegular symbol, Action action)
    {
        var item = new MenuItem { Header = header, Icon = new SymbolIcon { Symbol = symbol } };
        item.Click += (_, _) => action();
        return item;
    }

    private void OpenTaskbarSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Couldn't open the taskbar settings");
        }
    }

    private static byte[] LoadResource(string path)
    {
        using Stream stream = Application.GetResourceStream(new Uri($"pack://application:,,,/{path}")).Stream;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
