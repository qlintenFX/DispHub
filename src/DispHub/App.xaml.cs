using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DispHub.Controls;
using DispHub.Core.Config;
using DispHub.Core.Sharing;
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            await StartAsync(e.Args);
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Startup failed");
            System.Windows.MessageBox.Show(
                $"DispHub couldn't start.\n\n{ex.Message}\n\nLogs: {AppPaths.LogDirectory}",
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
        if (!_instance.IsFirstInstance)
        {
            _instance.SendToFirstInstance(args);
            Shutdown();
            return;
        }

        _services = ConfigureServices(args);
        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("DispHub {Version} starting (packaged: {Packaged})", AboutViewModel.Version, PackageInfo.IsPackaged);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        SessionEnding += (_, _) => RequestExit();

        await _services.GetRequiredService<ILicenseService>().InitializeAsync();

        _controller = _services.GetRequiredService<AppController>();
        _services.GetRequiredService<ThemeService>().Apply(_controller.Config.Ui.Theme);

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
        HwndSource.FromHwnd(handle).AddHook((nint _, int msg, nint wParam, nint _, ref bool handled) =>
        {
            handled = _router.HandleMessage((uint)msg, (nuint)wParam);
            return 0;
        });
        _router.DisplayChanged += (_, _) => _controller.OnDisplayChanged();
        _router.Resumed += (_, _) => Dispatcher.BeginInvoke(_controller.OnResumed, DispatcherPriority.Background);
        _router.SessionUnlocked += (_, _) => _controller.OnResumed();
        _router.TaskbarCreated += (_, _) => _tray?.Reregister();
        HotkeyBox.RecordingChanged += (_, recording) =>
        {
            if (recording)
                _controller.SuspendHotkeys();
            else
                _controller.ResumeHotkeys();
        };

        _controller.Start(hotkeys);
        _tray = new TrayService(_controller, ShowMainWindow, RequestExit);
        _tray.Show();
        _services.GetRequiredService<OsdService>().Attach();
        _services.GetRequiredService<ReviewPrompt>().Attach();

        _instance.Activated += (_, forwarded) => Dispatcher.BeginInvoke(() => HandleActivation(forwarded));
        _instance.StartListening();

        try
        {
            ProtocolRegistration.EnsureRegistered(SharedPreset.Scheme);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _logger.LogWarning(ex, "Couldn't register disphub:// links");
        }

        if (FindPresetLink(args) is not null)
            HandleActivation(args);
        else if (!LaunchInfo.IsStartupLaunch(args))
            ShowMainWindow();
        else
            MemoryTrim.Schedule();
    }

    private static ServiceProvider ConfigureServices(string[] args)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(new FileLoggerProvider(AppPaths.LogDirectory)));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new ConfigStore(AppPaths.DataDirectory));
        services.AddSingleton<IDisplayBackend, WindowsDisplayBackend>();
        services.AddSingleton<IColorApplier>(sp => new ColorApplier(
            sp.GetRequiredService<IDisplayBackend>(), AppPaths.JournalPath, sp.GetRequiredService<ILogger<ColorApplier>>()));
        services.AddSingleton<IForegroundWatcher, ForegroundWatcher>();
        services.AddSingleton<IAutostart, Autostart>();
        services.Add(CreateLicenseService(args));
        services.AddSingleton<AppController>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<OsdService>();
        services.AddSingleton<ReviewPrompt>();
        services.AddSingleton<IUpgradeService>(sp => new UpgradeService(
            sp.GetRequiredService<ILicenseService>(), () => sp.GetRequiredService<MainWindow>()));
        services.AddSingleton<ProfilesViewModel>();
        services.AddSingleton<GalleryService>();
        services.AddSingleton<PresetsViewModel>();
        services.AddSingleton<AutomationViewModel>();
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

    private void ShowMainWindow() => _services?.GetRequiredService<MainWindow>().ShowAndActivate();

    private void HandleActivation(string[] args)
    {
        if (args.Contains("--exit", StringComparer.OrdinalIgnoreCase))
        {
            RequestExit();
            return;
        }
        if (args.Contains(Autostart.StartupArgument, StringComparer.OrdinalIgnoreCase))
            return;
        ShowMainWindow();
        if (FindPresetLink(args) is { } link)
            _ = OfferPresetAsync(link);
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
                Content = "This link isn't a valid DispHub preset.",
                CloseButtonText = "OK",
                Owner = window,
            }.ShowDialogAsync();
            return;
        }

        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Add this preset?",
            Content = $"{preset.Name}\n{ProfileItemViewModel.Describe(preset.Color)}\n\n" +
                      "It will be added as a new profile and switched on so you can see it.",
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            Owner = window,
        };
        if (await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary &&
            _services.GetRequiredService<PresetsViewModel>().Add(preset))
        {
            _services.GetRequiredService<MainViewModel>().Navigate<ProfilesViewModel>();
        }
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
            Shutdown();
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
