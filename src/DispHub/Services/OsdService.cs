using DispHub.Core.Config;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Platform.Store;
using DispHub.Views;
using Wpf.Ui.Controls;

namespace DispHub.Services;

/// <summary>Shows the on-screen display when the profile changes from a hotkey or the tray.</summary>
public sealed class OsdService(AppController app)
{
    private OsdWindow? _window;
    private bool _hintShown;

    public void Attach() => app.StateChanged += (_, source) => OnStateChanged(source);

    /// <summary>A one-off message, shown at most once per session.</summary>
    public void ShowHint(string title, string detail)
    {
        if (_hintShown)
            return;
        _hintShown = true;
        _window ??= new OsdWindow();
        _window.Flash(title, detail, SymbolRegular.Info24, TimeSpan.FromSeconds(3), app.Config.Ui.OsdPosition);
    }

    /// <summary>Effects were turned on (with the look's name) or off from a hotkey.</summary>
    public void ShowEffects(string? lookName)
    {
        if (!app.Config.Ui.ShowOsd)
            return;
        _window ??= new OsdWindow();
        TimeSpan duration = TimeSpan.FromMilliseconds(app.Config.Ui.OsdDurationMs);
        _window.Flash(Loc.Get(lookName is null ? "Osd_EffectsOff" : "Osd_EffectsOn"), lookName ?? Loc.Get("Osd_EffectsOffDetail"),
            SymbolRegular.Sparkle24, duration, app.Config.Ui.OsdPosition);
    }

    private void OnStateChanged(ChangeSource source)
    {
        if (source is not (ChangeSource.Hotkey or ChangeSource.Tray) || !app.Config.Ui.ShowOsd)
            return;

        _window ??= new OsdWindow();
        TimeSpan duration = TimeSpan.FromMilliseconds(app.Config.Ui.OsdDurationMs);
        if (app.Config.IsPaused)
        {
            _window.Flash(Loc.Get("Osd_Paused"), Loc.Get("Summary_Original"), SymbolRegular.Pause24, duration, app.Config.Ui.OsdPosition);
            return;
        }

        Profile? profile = app.Current?.ProfileId is { } id ? app.FindProfile(id) : null;
        if (profile is not null)
            _window.Flash(profile.Name, ViewModels.ProfileItemViewModel.Describe(profile.Color), SymbolRegular.Color24, duration, app.Config.Ui.OsdPosition);
    }
}

/// <summary>
/// Asks for a Store rating once, after DispHub has proven useful (a week of use and real profile switching).
/// Only in the Store build, and only while the main window is open so the dialog isn't a surprise.
/// </summary>
public sealed class ReviewPrompt(AppController app, ILicenseService license)
{
    private static readonly TimeSpan MinimumUse = TimeSpan.FromDays(7);
    private const int MinimumSwitches = 10;
    private AppStateData _state = new();

    public void Attach()
    {
        _state = JsonFiles.ReadState(AppPaths.StatePath);
        if (_state.FirstRunUtc is null)
        {
            _state.FirstRunUtc = DateTimeOffset.UtcNow;
            Save();
        }

        app.StateChanged += (_, source) =>
        {
            // Only the first few switches matter for the rating prompt; stop writing the file after that.
            if (source is ChangeSource.Hotkey or ChangeSource.Tray && _state.ProfileSwitchCount < MinimumSwitches)
            {
                _state.ProfileSwitchCount++;
                Save();
            }
        };
    }

    /// <summary>Called whenever the main window is activated.</summary>
    public async Task OnWindowActivatedAsync(MainWindow window)
    {
        if (!license.CanPurchase || _state.ReviewPromptedUtc is not null || _state.FirstRunUtc is not { } first)
            return;
        if (DateTimeOffset.UtcNow - first < MinimumUse || _state.ProfileSwitchCount < MinimumSwitches)
            return;

        _state.ReviewPromptedUtc = DateTimeOffset.UtcNow;
        Save();
        await license.RequestReviewAsync(new System.Windows.Interop.WindowInteropHelper(window).Handle);
    }

    private void Save()
    {
        try
        {
            JsonFiles.WriteState(AppPaths.StatePath, _state);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
        }
    }
}
