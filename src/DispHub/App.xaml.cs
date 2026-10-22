// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DispHub.Controls;
using DispHub.Core.Config;
using DispHub.Core.Model;
using DispHub.Core.Sharing;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Platform.Shell;
using DispHub.Platform.Store;
using DispHub.Services;
using DispHub.ViewModels;
using DispHub.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DispHub;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1001", Justification = "Disposed in RequestExit; WPF owns the Application lifetime.")]
public partial class App : Application
{
    private ServiceProvider? _services;
    private SingleInstance? _instance;
    private AppController? _controller;
    private TrayService? _tray;
    private ShellMessageRouter? _router;
    private ILogger<App>? _logger;
    private bool _exiting;
    private bool _restart;
    private bool _themeChangePending;
    private bool _fresh;

    /// <summary>A trial run as a brand-new user: empty settings, and nothing about it written to Windows.</summary>
    private const string FreshArgument = "--fresh";

    /// <summary>Shows the first-run walkthrough again, keeping the settings.</summary>
    private const string WalkthroughArgument = "--walkthrough";

    /// <summary>Opens DispHub with the guided tour.</summary>
    private const string TourArgument = "--tour";

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Before anything reads the settings or starts a log.
        _fresh = Has(e.Args, FreshArgument);
        if (_fresh)
            AppPaths.UseDataDirectory(FreshDataDirectory());

        // Set the language first and synchronously: WPF builds pages in dispatcher work that inherits the culture
        // that was current when it was queued, so setting it later inside async startup leaves pages in English.
        Loc.Apply(ConfigStore.PeekLanguage(AppPaths.DataDirectory));
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(Loc.Culture.IetfLanguageTag)));
        DefaultConfig.Names = (Loc.Get("Default_Standard"), Loc.Get("Default_Gaming"), Loc.Get("Default_Night"));

        try
        {
            await StartAsync(e.Args);
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Startup failed");
            System.Windows.MessageBox.Show(
                Loc.Format("Startup_Failed", ex.Message, AppPaths.LogDirectory),
                "DispHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            RequestExit();
        }
    }

    private async Task StartAsync(string[] commandLine)
    {
        string[] args = LaunchInfo.GetArguments(commandLine);

        if (args.Contains("--diagnose", StringComparer.OrdinalIgnoreCase))
        {
            RunHardwareProbe();
            return;
        }

        _instance = new SingleInstance();
        if (args.Contains("--exit", StringComparer.OrdinalIgnoreCase) && _instance.IsFirstInstance)
        {
            // Nothing to exit.
            RequestExit();
            return;
        }
        // A fresh run replaces the running DispHub, so the two never fight over the colours.
        if (_fresh && !_instance.IsFirstInstance)
            _instance.TakeOver(TimeSpan.FromSeconds(10));
        if (!_instance.IsFirstInstance)
        {
            _instance.SendToFirstInstance(args);
            Shutdown();
            return;
        }

        ComboBoxClickFix.Register();
        _services = ConfigureServices(args, _fresh);
        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("DispHub {Version} starting (packaged: {Packaged}, fresh: {Fresh})", AboutViewModel.Version, PackageInfo.IsPackaged, _fresh);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        SessionEnding += (_, _) => RequestExit();

        await _services.GetRequiredService<ILicenseService>().InitializeAsync();

        _controller = _services.GetRequiredService<AppController>();
        _services.GetRequiredService<ThemeService>().Apply(_controller.Config.Ui);
        ColorPreview.Use(_controller.Config.Ui.PreviewPicture);

        // The hidden host window owns the tray icon and receives hotkeys and system broadcasts.
        var host = new Window
        {
            Title = "DispHub",
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
        };
        MainWindow = host;
        nint handle = new WindowInteropHelper(host).EnsureHandle();
        var hotkeys = new HotkeyRegistrar(handle);
        _router = new ShellMessageRouter(handle, hotkeys);
        HwndSource.FromHwnd(handle).AddHook((nint _, int msg, nint wParam, nint lParam, ref bool handled) =>
        {
            handled = _router.HandleMessage((uint)msg, (nuint)wParam, lParam);
            return 0;
        });
        _router.DisplayChanged += (_, _) => _controller.OnDisplayChanged();
        _router.Resumed += (_, _) => Dispatcher.BeginInvoke(_controller.OnResumed, DispatcherPriority.Background);
        _router.SessionUnlocked += (_, _) => _controller.OnResumed();
        _router.ThemeChanged += (_, _) => OnWindowsThemeChanged();
        HotkeyBox.RecordingChanged += (_, recording) =>
        {
            if (recording)
                _controller.SuspendHotkeys();
            else
                _controller.ResumeHotkeys();
        };

        _controller.Start(hotkeys);
        _tray = new TrayService(_controller, _router, handle, ShowMainWindow, RequestExit, CreateQuickPanel, _services.GetRequiredService<ILogger<TrayService>>());
        _tray.Show();
        _services.GetRequiredService<OsdService>().Attach();
        _services.GetRequiredService<ReviewPrompt>().Attach();

        _instance.Activated += (_, forwarded) => Dispatcher.BeginInvoke(() => HandleActivation(forwarded));
        _instance.StartListening();

        try
        {
            if (!_fresh)
                ProtocolRegistration.EnsureRegistered(SharedPreset.Scheme);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _logger.LogWarning(ex, "Couldn't register disphub:// links");
        }

        bool startup = LaunchInfo.IsStartupLaunch(args);
        if (FindPresetLink(args) is not null)
            HandleActivation(args);
        else if (!_controller.Config.Ui.OnboardingDone || Has(args, WalkthroughArgument))
            ShowOnboarding(openWindowAfter: !startup);
        else if (Has(args, TourArgument))
            _services.GetRequiredService<MainWindow>().StartTour();
        else if (!startup)
            ShowMainWindow();
        else
            MemoryTrim.Schedule();
    }

    private static ServiceProvider ConfigureServices(string[] args, bool fresh)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(new FileLoggerProvider(AppPaths.LogDirectory)));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new ConfigStore(AppPaths.DataDirectory));
        services.AddSingleton<IDisplayBackend, WindowsDisplayBackend>();
        services.AddSingleton<IColorApplier>(sp => new ColorApplier(
            sp.GetRequiredService<IDisplayBackend>(), AppPaths.JournalPath, sp.GetRequiredService<ILogger<ColorApplier>>()));
        services.AddSingleton<IForegroundWatcher, ForegroundWatcher>();
        if (fresh)
            services.AddSingleton<IAutostart, TrialAutostart>();
        else
            services.AddSingleton<IAutostart, Autostart>();
        services.Add(CreateLicenseService(args));
        services.AddSingleton<AppController>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<OsdService>();
        services.AddSingleton<ReviewPrompt>();
        services.AddSingleton<IUpgradeService>(sp => new UpgradeService(
            sp.GetRequiredService<ILicenseService>(), () => sp.GetRequiredService<MainWindow>()));
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<ProfilesViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<AppsViewModel>();
        services.AddSingleton<DisplaysViewModel>();
        services.AddSingleton<QuickPanelViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    private static ServiceDescriptor CreateLicenseService(string[] args)
    {
        string? fake = args.FirstOrDefault(a => a.StartsWith("--fake-store=", StringComparison.OrdinalIgnoreCase));
        if (fake is not null)
            return ServiceDescriptor.Singleton<ILicenseService>(new FakeLicenseService(fake.EndsWith("=pro", StringComparison.OrdinalIgnoreCase)));
        return PackageInfo.IsStoreSigned
            ? ServiceDescriptor.Singleton<ILicenseService, StoreLicenseService>()
            : ServiceDescriptor.Singleton<ILicenseService, UnlockedLicenseService>();
    }

    private void ShowMainWindow()
    {
        if (_services is null)
            return;
        MainWindow window = _services.GetRequiredService<MainWindow>();
        bool wasHidden = !window.IsVisible;
        window.ShowAndActivate();
        if (wasHidden)
            _services.GetRequiredService<MainViewModel>().SelectedPage.OnShown();
    }

    private QuickPanel CreateQuickPanel()
    {
        QuickPanelViewModel viewModel = _services!.GetRequiredService<QuickPanelViewModel>();
        viewModel.OpenWindow = ShowMainWindow;
        var panel = new QuickPanel(viewModel);
        ThemeService theme = _services!.GetRequiredService<ThemeService>();
        panel.SourceInitialized += (_, _) => theme.Attach(panel, flyout: true);
        return panel;
    }

    /// <summary>
    /// The first-run walkthrough, then (unless DispHub started with Windows) the main window, with its tour when
    /// asked for.
    /// </summary>
    private void ShowOnboarding(bool openWindowAfter)
    {
        var viewModel = new OnboardingViewModel(_controller!, _services!.GetRequiredService<IAutostart>());
        // Before the window listens: it closes when the walkthrough finishes, and this must know the choice by then.
        bool tour = false;
        viewModel.Finished += (_, wanted) => tour = wanted;
        var window = new OnboardingWindow(viewModel);
        ThemeService theme = _services!.GetRequiredService<ThemeService>();
        window.SourceInitialized += (_, _) => theme.Attach(window);
        window.Closed += (_, _) =>
        {
            if (tour)
                _services!.GetRequiredService<MainWindow>().StartTour();
            else if (openWindowAfter)
                ShowMainWindow();
        };
        window.Show();
        window.Activate();
    }

    /// <summary>Windows sends several messages for one theme or accent change; follow them once.</summary>
    private void OnWindowsThemeChanged()
    {
        if (_themeChangePending)
            return;
        _themeChangePending = true;
        Dispatcher.BeginInvoke(() =>
        {
            _themeChangePending = false;
            _services?.GetRequiredService<ThemeService>().OnWindowsThemeChanged();
        }, DispatcherPriority.Background);
    }

    private void HandleActivation(string[] args)
    {
        if (args.Contains("--exit", StringComparer.OrdinalIgnoreCase))
        {
            RequestExit();
            return;
        }
        if (args.Contains(Autostart.StartupArgument, StringComparer.OrdinalIgnoreCase))
            return;
        if (Has(args, WalkthroughArgument))
        {
            ShowOnboarding(openWindowAfter: true);
            return;
        }
        if (Has(args, TourArgument))
        {
            _services?.GetRequiredService<MainWindow>().StartTour();
            return;
        }
        ShowMainWindow();
        if (FindPresetLink(args) is { } link)
            _ = OfferPresetAsync(link);
    }

    private static bool Has(IEnumerable<string> args, string argument) => args.Contains(argument, StringComparer.OrdinalIgnoreCase);

    /// <summary>An empty folder for a --fresh run: emptied at every start, so each run is a first run.</summary>
    private static string FreshDataDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DispHub-fresh");
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An earlier fresh run still has it open: this run gets a folder of its own.
            path = Path.Combine(Path.GetTempPath(), $"DispHub-fresh-{Environment.ProcessId}");
        }
        return path;
    }

    private static string? FindPresetLink(string[] args) =>
        args.FirstOrDefault(a => a.StartsWith(SharedPreset.Scheme + ":", StringComparison.OrdinalIgnoreCase));

    private async Task OfferPresetAsync(string link)
    {
        if (_services is null)
            return;
        MainWindow window = _services.GetRequiredService<MainWindow>();
        if (!SharedPreset.TryParse(link, out SharedPreset? preset))
        {
            await new Wpf.Ui.Controls.MessageBox
            {
                Title = "DispHub",
                Content = Loc.Get("Preset_InvalidLink"),
                CloseButtonText = Loc.Get("Common_OK"),
                Owner = window,
            }.ShowDialogAsync();
            return;
        }

        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = Loc.Get("Preset_AddTitle"),
            Content = Loc.Format("Preset_AddText", preset.Name, ProfileItemViewModel.Describe(preset.Color)),
            PrimaryButtonText = Loc.Get("Common_Add"),
            CloseButtonText = Loc.Get("Common_Cancel"),
            Owner = window,
        };
        if (await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary &&
            _services.GetRequiredService<ProfilesViewModel>().AddPreset(preset))
        {
            _services.GetRequiredService<MainViewModel>().Navigate<ProfilesViewModel>();
        }
    }

    /// <summary>The main window was closed to the notification area.</summary>
    public void OnMainWindowHidden()
    {
        if (_exiting)
            return;
        _services?.GetRequiredService<MainViewModel>().SelectedPage.OnHidden();
        if (_tray is not null && _services is not null)
            _tray.ShowStillRunningHint(_services.GetRequiredService<OsdService>());
        MemoryTrim.Schedule();
    }

    /// <summary>Quits and starts DispHub again, e.g. after changing the language.</summary>
    public void RequestRestart()
    {
        _restart = true;
        RequestExit();
    }

    /// <summary>Restores the original colours, saves and quits.</summary>
    public void RequestExit()
    {
        if (_exiting)
            return;
        _exiting = true;
        _logger?.LogInformation("Exiting");
        try
        {
            _controller?.Shutdown();
            _tray?.Dispose();
            _router?.Dispose();
            _instance?.Dispose();
            _services?.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while exiting");
        }
        finally
        {
            if (_restart)
                StartNewInstance();
            Shutdown();
        }
    }

    private void StartNewInstance()
    {
        try
        {
            // The Store version must be started through its app ID so it runs with its package identity.
            string? family = PackageInfo.IsPackaged ? global::Windows.ApplicationModel.Package.Current.Id.FamilyName : null;
            ProcessStartInfo start = family is not null
                ? new ProcessStartInfo("explorer.exe", $"shell:AppsFolder\\{family}!DispHub")
                : new ProcessStartInfo(Environment.ProcessPath!);
            start.UseShellExecute = true;
            Process.Start(start);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger?.LogError(ex, "Couldn't restart");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI exception");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger?.LogCritical(e.ExceptionObject as Exception, "Fatal error; restoring colours");
        if (_services?.GetService<IColorApplier>() is { } colors)
            colors.RestoreAll(TimeSpan.FromSeconds(2));
    }

    private void RunHardwareProbe()
    {
        string report = Diagnostics.ProbeHardware();
        string path = Path.Combine(AppPaths.LogDirectory, "hardware-probe.txt");
        Directory.CreateDirectory(AppPaths.LogDirectory);
        File.WriteAllText(path, report);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        Shutdown();
    }
}
