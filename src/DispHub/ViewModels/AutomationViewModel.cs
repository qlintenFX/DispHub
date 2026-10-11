using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Model;
using DispHub.Services;

namespace DispHub.ViewModels;

public sealed partial class AutomationViewModel : PageViewModel
{
    private readonly AppController _app;
    private readonly IUpgradeService _upgrade;
    private bool _loading;

    public AutomationViewModel(AppController app, IUpgradeService upgrade)
        : base("Automation", Wpf.Ui.Controls.SymbolRegular.Flash24)
    {
        _app = app;
        _upgrade = upgrade;
        _app.ConfigChanged += (_, _) => Load();
        Load();
    }

    public ObservableCollection<AppRuleViewModel> AppRules { get; } = [];

    public ObservableCollection<ScheduleSlotViewModel> Slots { get; } = [];

    public ObservableCollection<ProfileChoice> ProfileChoices { get; } = [];

    [ObservableProperty]
    public partial bool IsPro { get; set; }

    [ObservableProperty]
    public partial bool ScheduleEnabled { get; set; }

    partial void OnScheduleEnabledChanged(bool value)
    {
        if (_loading)
            return;
        _app.Config.Schedule.Enabled = value;
        _app.SettingsChanged();
    }

    [RelayCommand]
    private Task Upgrade() => _upgrade.PurchaseAsync();

    [RelayCommand]
    private void AddAppRule(string? executable)
    {
        if (!IsPro)
        {
            _upgrade.ShowUpgrade("Per-app profiles");
            return;
        }

        string? path = executable ?? PickExecutable();
        if (string.IsNullOrWhiteSpace(path))
            return;
        var rule = new AppRule
        {
            Executable = Path.GetFileName(path),
            ProfileId = _app.Config.Profiles.Count > 1 ? _app.Config.Profiles[1].Id : _app.Config.Profiles[0].Id,
        };
        _app.Config.AppRules.Add(rule);
        _app.SettingsChanged();
    }

    [RelayCommand]
    private void AddSlot()
    {
        if (!IsPro)
        {
            _upgrade.ShowUpgrade("Schedules");
            return;
        }

        var slots = _app.Config.Schedule.Slots;
        TimeOnly start = slots.Count == 0 ? new TimeOnly(7, 0) : slots.Max(s => s.Start).AddHours(2);
        Guid profile = slots.Count == 0
            ? _app.Config.Profiles[0].Id
            : _app.Config.Profiles.FirstOrDefault(p => p.Name.Contains("night", StringComparison.OrdinalIgnoreCase))?.Id ?? _app.Config.Profiles[^1].Id;
        slots.Add(new ScheduleSlot { Start = start, ProfileId = profile, FadeMinutes = slots.Count == 0 ? 0 : 30 });
        if (slots.Count >= 1)
            _app.Config.Schedule.Enabled = true;
        _app.SettingsChanged();
    }

    /// <summary>Programs with a visible window, for the "add app" menu.</summary>
    public static IReadOnlyList<string> RunningApps()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero && process.Id != Environment.ProcessId)
                        names.Add(process.ProcessName + ".exe");
                }
                catch (InvalidOperationException)
                {
                }
            }
        }
        names.ExceptWith(["explorer.exe", "ApplicationFrameHost.exe", "TextInputHost.exe", "SystemSettings.exe"]);
        return names.ToList();
    }

    private static string? PickExecutable()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a program",
            Filter = "Programs (*.exe)|*.exe",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void Load()
    {
        _loading = true;
        try
        {
            IsPro = _app.Features.AppRules;
            ScheduleEnabled = _app.Config.Schedule.Enabled;

            ProfileChoices.Clear();
            foreach (Profile profile in _app.Config.Profiles)
                ProfileChoices.Add(new ProfileChoice(profile.Id, profile.Name));

            AppRules.Clear();
            foreach (AppRule rule in _app.Config.AppRules)
                AppRules.Add(new AppRuleViewModel(_app, rule, ProfileChoices));

            Slots.Clear();
            foreach (ScheduleSlot slot in _app.Config.Schedule.Slots.OrderBy(s => s.Start))
                Slots.Add(new ScheduleSlotViewModel(_app, slot, ProfileChoices));
        }
        finally
        {
            _loading = false;
        }
    }
}

public sealed record ProfileChoice(Guid Id, string Name);

public sealed partial class AppRuleViewModel : ObservableObject
{
    private readonly AppController _app;
    private readonly AppRule _rule;

    public AppRuleViewModel(AppController app, AppRule rule, IReadOnlyList<ProfileChoice> choices)
    {
        _app = app;
        _rule = rule;
        Choices = choices;
        Executable = rule.Executable;
        Enabled = rule.Enabled;
        Profile = choices.FirstOrDefault(c => c.Id == rule.ProfileId);
    }

    public IReadOnlyList<ProfileChoice> Choices { get; }

    [ObservableProperty]
    public partial string Executable { get; set; }

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    [ObservableProperty]
    public partial ProfileChoice? Profile { get; set; }

    partial void OnEnabledChanged(bool value)
    {
        if (_rule.Enabled == value)
            return;
        _rule.Enabled = value;
        _app.SettingsChanged();
    }

    partial void OnProfileChanged(ProfileChoice? value)
    {
        if (value is null || _rule.ProfileId == value.Id)
            return;
        _rule.ProfileId = value.Id;
        _app.SettingsChanged();
    }

    partial void OnExecutableChanged(string value) => CommitExecutable();

    private void CommitExecutable()
    {
        if (_rule is null)
            return;
        string trimmed = Executable.Trim();
        if (trimmed.Length == 0 || trimmed == _rule.Executable)
            return;
        _rule.Executable = trimmed;
        _app.SettingsChanged();
    }

    [RelayCommand]
    private void Remove()
    {
        _app.Config.AppRules.Remove(_rule);
        _app.SettingsChanged();
    }
}

public sealed partial class ScheduleSlotViewModel : ObservableObject
{
    private readonly AppController _app;
    private readonly ScheduleSlot _slot;

    public ScheduleSlotViewModel(AppController app, ScheduleSlot slot, IReadOnlyList<ProfileChoice> choices)
    {
        _app = app;
        _slot = slot;
        Choices = choices;
        Start = slot.Start.ToString("HH:mm", CultureInfo.InvariantCulture);
        FadeMinutes = slot.FadeMinutes;
        Profile = choices.FirstOrDefault(c => c.Id == slot.ProfileId);
    }

    public IReadOnlyList<ProfileChoice> Choices { get; }

    [ObservableProperty]
    public partial string Start { get; set; }

    [ObservableProperty]
    public partial bool StartIsInvalid { get; set; }

    [ObservableProperty]
    public partial double FadeMinutes { get; set; }

    [ObservableProperty]
    public partial ProfileChoice? Profile { get; set; }

    partial void OnProfileChanged(ProfileChoice? value)
    {
        if (value is null || _slot.ProfileId == value.Id)
            return;
        _slot.ProfileId = value.Id;
        _app.SettingsChanged();
    }

    partial void OnFadeMinutesChanged(double value)
    {
        int minutes = (int)Math.Clamp(Math.Round(value), 0, 180);
        if (_slot.FadeMinutes == minutes)
            return;
        _slot.FadeMinutes = minutes;
        _app.SettingsChanged();
    }

    partial void OnStartChanged(string value) => CommitStart();

    private void CommitStart()
    {
        if (_slot is null)
            return;
        if (!TimeOnly.TryParse(Start, CultureInfo.CurrentCulture, out TimeOnly time) &&
            !TimeOnly.TryParse(Start, CultureInfo.InvariantCulture, out time))
        {
            StartIsInvalid = true;
            return;
        }
        StartIsInvalid = false;
        if (_slot.Start == time)
            return;
        _slot.Start = time;
        _app.SettingsChanged();
    }

    [RelayCommand]
    private void Remove()
    {
        _app.Config.Schedule.Slots.Remove(_slot);
        _app.SettingsChanged();
    }
}
