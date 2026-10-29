// Copyright (c) 2026 The DispHub Authors
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Model;
using DispHub.Core.Rules;
using DispHub.Localization;
using DispHub.Platform.Shell;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>
/// Per-app profiles: while a program is in front its profile applies; switching away brings the normal one back.
/// Shows what's in front right now, and picks programs from the ones that are running.
/// </summary>
public sealed partial class AppsViewModel : PageViewModel
{
    private readonly AppController _app;
    private readonly IUpgradeService _upgrade;
    private bool _ownChange;

    public AppsViewModel(AppController app, IUpgradeService upgrade)
        : base(Loc.Get("Nav_Apps"), Wpf.Ui.Controls.SymbolRegular.AppsList24)
    {
        _app = app;
        _upgrade = upgrade;
        _app.ConfigChanged += (_, _) =>
        {
            if (!_ownChange)
                Load();
        };
        _app.ForegroundChanged += (_, _) => RefreshForeground();
        _app.StateChanged += (_, _) => RefreshForeground();
        Load();
    }

    public ObservableCollection<AppRuleItem> Rules { get; } = [];
    public ObservableCollection<ProfileChoice> ProfileChoices { get; } = [];
    public ObservableCollection<RunningAppItem> RunningApps { get; } = [];

    /// <summary>Another app rule can be added (the free tier has one).</summary>
    [ObservableProperty] public partial bool CanAddRule { get; set; }

    /// <summary>The free tier: say that one app is included and Pro does every app.</summary>
    [ObservableProperty] public partial bool ShowProHint { get; set; }
    [ObservableProperty] public partial bool HasRules { get; set; }
    [ObservableProperty] public partial bool IsPicking { get; set; }
    [ObservableProperty] public partial string PickerQuery { get; set; } = "";
    [ObservableProperty] public partial ImageSource? ForegroundIcon { get; set; }
    [ObservableProperty] public partial string ForegroundName { get; set; } = "";
    [ObservableProperty] public partial string ForegroundStatus { get; set; } = "";
    [ObservableProperty] public partial bool CanAddForeground { get; set; }

    private List<RunningProgram> _running = [];

    public override void OnShown() => RefreshForeground();

    partial void OnPickerQueryChanged(string value) => FilterRunning();

    [RelayCommand]
    private void OpenPicker()
    {
        if (!CanAddRule)
        {
            _upgrade.ShowUpgrade(Loc.Get("Upgrade_AppRules"));
            return;
        }
        _running = [.. ProgramInfo.WithWindows()];
        PickerQuery = "";
        FilterRunning();
        IsPicking = true;
    }

    [RelayCommand]
    private void ClosePicker() => IsPicking = false;

    [RelayCommand]
    private void AddRunning(RunningAppItem? item)
    {
        if (item is not null)
            Add(item.Path);
    }

    [RelayCommand]
    private void AddForeground()
    {
        if (!CanAddRule)
        {
            _upgrade.ShowUpgrade(Loc.Get("Upgrade_AppRules"));
            return;
        }
        if (_app.ForegroundExecutable is { } path)
            Add(path);
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Loc.Get("Apps_ChooseProgram"),
            Filter = Loc.Get("Apps_ProgramsFilter"),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        };
        if (dialog.ShowDialog() == true)
            Add(dialog.FileName);
    }

    internal void Save(bool reload = false)
    {
        _ownChange = !reload;
        try
        {
            _app.SettingsChanged();
        }
        finally
        {
            _ownChange = false;
        }
        RefreshForeground();
    }

    internal void Remove(AppRule rule)
    {
        _app.Config.AppRules.Remove(rule);
        Save(reload: true);
    }

    private void Add(string path)
    {
        string executable = Path.GetFileName(path);
        if (_app.Config.AppRules.Exists(r => RuleEngine.Matches(r, path)))
        {
            IsPicking = false;
            return;
        }
        Profile profile = _app.Config.Profiles.FirstOrDefault(p => p.Id != _app.Config.ActiveProfileId) ?? _app.Config.Profiles[0];
        _app.Config.AppRules.Add(new AppRule { Executable = executable, Path = path, ProfileId = profile.Id });
        IsPicking = false;
        Save(reload: true);
    }

    private void FilterRunning()
    {
        RunningApps.Clear();
        string query = PickerQuery.Trim();
        foreach (RunningProgram program in _running.Where(p => query.Length == 0 ||
                     p.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                     p.WindowTitle.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                     Path.GetFileName(p.Path).Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            bool hasRule = _app.Config.AppRules.Exists(r => RuleEngine.Matches(r, program.Path));
            RunningApps.Add(new RunningAppItem(program.Path, program.Name, program.WindowTitle, AppIcons.For(program.Path), hasRule));
        }
    }

    private void Load()
    {
        CanAddRule = _app.Features.CanAddAppRule(_app.Config.AppRules.Count);
        ShowProHint = !_app.Features.HasPro;
        ProfileChoices.Clear();
        foreach (Profile profile in _app.Config.Profiles)
            ProfileChoices.Add(ProfileChoice.From(profile));
        Rules.Clear();
        for (int i = 0; i < _app.Config.AppRules.Count; i++)
            Rules.Add(new AppRuleItem(this, _app.Config.AppRules[i], ProfileChoices, isLocked: !_app.Features.CanUseAppRule(i)));
        HasRules = Rules.Count > 0;
        RefreshForeground();
    }

    private void RefreshForeground()
    {
        string? path = _app.ForegroundExecutable;
        int index = path is null ? -1 : _app.Config.AppRules.FindIndex(r => r.Enabled && RuleEngine.Matches(r, path));
        AppRule? match = index < 0 ? null : _app.Config.AppRules[index];
        bool works = _app.Features.CanUseAppRule(index);
        foreach (AppRuleItem item in Rules)
            item.IsActiveNow = match is not null && works && item.Is(match);
        if (path is null)
        {
            ForegroundIcon = null;
            ForegroundName = Loc.Get("Apps_NothingYet");
            ForegroundStatus = "";
            CanAddForeground = false;
            return;
        }
        ForegroundIcon = AppIcons.For(path);
        ForegroundName = ProgramInfo.DisplayName(path);
        Profile? profile = match is null ? null : RuleEngine.FindProfile(_app.Config, match.ProfileId);
        ForegroundStatus = profile is not null
            ? Loc.Format(works ? "Apps_ForegroundRule" : "Apps_ForegroundRulePro", profile.Name)
            : Loc.Get("Apps_ForegroundNoRule");
        CanAddForeground = match is null && !_app.Config.AppRules.Exists(r => RuleEngine.Matches(r, path));
    }
}

public sealed record RunningAppItem(string Path, string Name, string WindowTitle, ImageSource? Icon, bool HasRule)
{
    public string FileName => System.IO.Path.GetFileName(Path);
}

public sealed partial class AppRuleItem : ObservableObject
{
    private readonly AppsViewModel _owner;
    private readonly AppRule _rule;
    private readonly bool _ready;

    public AppRuleItem(AppsViewModel owner, AppRule rule, IReadOnlyList<ProfileChoice> profiles, bool isLocked = false)
    {
        _owner = owner;
        _rule = rule;
        IsLocked = isLocked;
        Profiles = profiles;
        string? path = rule.Path ?? (Path.IsPathRooted(rule.Executable) ? rule.Executable : null);
        Icon = AppIcons.For(path);
        Name = path is not null ? ProgramInfo.DisplayName(path) : Path.GetFileNameWithoutExtension(rule.Executable);
        Executable = Path.GetFileName(rule.Executable);
        Enabled = rule.Enabled;
        Profile = profiles.FirstOrDefault(p => p.Id == rule.ProfileId);
        _ready = true;
    }

    public IReadOnlyList<ProfileChoice> Profiles { get; }
    public ImageSource? Icon { get; }

    /// <summary>Past the free tier's one rule: kept, but it waits for Pro.</summary>
    public bool IsLocked { get; }
    public string Name { get; }
    public string Executable { get; }

    [ObservableProperty] public partial bool Enabled { get; set; }
    [ObservableProperty] public partial ProfileChoice? Profile { get; set; }
    [ObservableProperty] public partial bool IsActiveNow { get; set; }

    internal bool Is(AppRule rule) => ReferenceEquals(rule, _rule);

    partial void OnEnabledChanged(bool value)
    {
        if (!_ready || value == _rule.Enabled)
            return;
        _rule.Enabled = value;
        _owner.Save();
    }

    partial void OnProfileChanged(ProfileChoice? value)
    {
        if (!_ready || value is null || value.Id == _rule.ProfileId)
            return;
        _rule.ProfileId = value.Id;
        _owner.Save();
    }

    [RelayCommand]
    private void Remove() => _owner.Remove(_rule);
}
