using System.Windows;
using System.Windows.Interop;
using DisplayHub.Services.Display;
using DisplayHub.Services.Hotkeys;
using DisplayHub.Services.Logging;
using DisplayHub.Services.Profiles;
using DisplayHub.Services.Settings;

namespace DisplayHub;

public partial class MainWindow : Window
{
    public static ProfileManager ProfileManager { get; private set; } = null!;
    public static DisplayManager DisplayManager { get; private set; } = null!;
    public static HotkeyManager HotkeyManager { get; private set; } = null!;
    public static DynamicControls DynamicControls { get; private set; } = null!;
    public static SettingsManager SettingsManager { get; private set; } = null!;

    private SettingsWindow? _settingsWindow;
    private HwndSource? _hwndSource;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Initialize(Constants.AppConstants.LogFileName);
        Logger.Log("MainWindow_Loaded");

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(WndProc);

        SettingsManager = new SettingsManager();
        SettingsWindow.ApplyTheme(SettingsManager.AppTheme);
        ProfileManager = new ProfileManager();
        DisplayManager = new DisplayManager(VibranceServiceFactory.Create());
        HotkeyManager = new HotkeyManager(helper.Handle);
        DynamicControls = new DynamicControls(DisplayManager);

        foreach (var profile in ProfileManager.Profiles)
            HotkeyManager.RegisterHotkey(profile);

        if (SettingsManager.DynamicControlsEnabled)
        {
            DynamicControls.IsEnabled = true;
            DynamicControls.RegisterHotkeys(HotkeyManager);
        }

        HotkeyManager.HotkeyPressed += OnProfileHotkeyPressed;

        OpenSettingsWindow();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (DynamicControls.IsEnabled && DynamicControls.ProcessHotkey(id))
            {
                handled = true;
                return IntPtr.Zero;
            }
            HotkeyManager.ProcessHotkey(wParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnProfileHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        DisplayManager.ApplySettings(e.Profile.Gamma, e.Profile.Contrast, e.Profile.Vibrance);
        Logger.Log($"Hotkey applied profile: {e.Profile.Name}");
    }

    public void OpenSettingsWindow()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
        _settingsWindow.Focus();
    }

    private void TrayIcon_LeftClick(object sender, RoutedEventArgs e) => OpenSettingsWindow();
    private void TrayOpen_Click(object sender, RoutedEventArgs e) => OpenSettingsWindow();

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        DisplayManager?.ResetToDefault();
        HotkeyManager?.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        base.OnClosed(e);
    }
}
