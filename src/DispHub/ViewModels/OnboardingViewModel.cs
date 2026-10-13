using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform.Shell;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>
/// The first-run walkthrough: what DispHub does, where its icon lives and what a left or right click on it does,
/// the hotkeys, and the power button. Shown once; the choices in it are ordinary settings.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    public const int StepCount = 4;

    private readonly AppController _app;
    private readonly IAutostart _autostart;
    private bool _loading = true;

    public OnboardingViewModel(AppController app, IAutostart autostart)
    {
        _app = app;
        _autostart = autostart;
        TrayLeftClick = app.Config.Ui.TrayLeftClick;
        foreach (Profile profile in app.Config.Profiles.Where(p => p.Hotkey is not null).Take(4))
            Hotkeys.Add(new HotkeyLine(profile.Name, profile.Hotkey!));
        if (app.Config.Hotkeys.TogglePause is { } onOff)
            Hotkeys.Add(new HotkeyLine(Loc.Get("Settings_Pause"), onOff));
        for (int i = 0; i < StepCount; i++)
            Dots.Add(new StepDot(i == 0));
        _loading = false;
        _ = LoadAutostartAsync();
    }

    /// <summary>The walkthrough is done (finished or closed).</summary>
    public event EventHandler? Finished;

    public IReadOnlyList<TrayClickAction> TrayActions { get; } = [TrayClickAction.QuickPanel, TrayClickAction.OpenWindow, TrayClickAction.ShowProfileMenu];

    public ObservableCollection<HotkeyLine> Hotkeys { get; } = [];

    public ObservableCollection<StepDot> Dots { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirst), nameof(IsLast), nameof(IsWelcome), nameof(IsTray), nameof(IsHotkeys), nameof(IsPower))]
    public partial int Step { get; set; }

    [ObservableProperty] public partial TrayClickAction TrayLeftClick { get; set; }
    [ObservableProperty] public partial bool LaunchAtLogin { get; set; }

    public bool IsFirst => Step == 0;
    public bool IsLast => Step == StepCount - 1;
    public bool IsWelcome => Step == 0;
    public bool IsTray => Step == 1;
    public bool IsHotkeys => Step == 2;
    public bool IsPower => Step == 3;

    partial void OnStepChanged(int value)
    {
        for (int i = 0; i < Dots.Count; i++)
            Dots[i].IsCurrent = i == value;
    }

    partial void OnTrayLeftClickChanged(TrayClickAction value)
    {
        if (_loading)
            return;
        _app.Config.Ui.TrayLeftClick = value;
        _app.SettingsChanged();
    }

    async partial void OnLaunchAtLoginChanged(bool value)
    {
        if (_loading)
            return;
        try
        {
            await _autostart.SetEnabledAsync(value);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            // Settings shows why; the walkthrough just moves on.
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (IsLast)
            Finish();
        else
            Step++;
    }

    [RelayCommand]
    private void Back()
    {
        if (!IsFirst)
            Step--;
    }

    /// <summary>Marks the walkthrough as seen, whether it was finished or closed.</summary>
    [RelayCommand]
    public void Finish()
    {
        if (_app.Config.Ui.OnboardingDone)
            return;
        _app.Config.Ui.OnboardingDone = true;
        _app.SettingsChanged();
        Finished?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadAutostartAsync()
    {
        try
        {
            AutostartState state = await _autostart.GetStateAsync();
            _loading = true;
            LaunchAtLogin = state == AutostartState.Enabled;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
        }
        finally
        {
            _loading = false;
        }
    }
}

public sealed record HotkeyLine(string Name, string Keys);

public sealed partial class StepDot(bool isCurrent) : ObservableObject
{
    [ObservableProperty] public partial bool IsCurrent { get; set; } = isCurrent;
}
