using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Config;
using DispHub.Core.Effects;
using DispHub.Core.Sharing;
using DispHub.Localization;
using DispHub.Platform.Store;
using DispHub.Services;
using Wpf.Ui.Controls;

namespace DispHub.ViewModels;

/// <summary>
/// Screen effects: a gallery of looks rendered on a photo, a before and after preview, and every setting of the
/// current look grouped by what it does. Changes show on screen straight away.
/// </summary>
public sealed partial class EffectsViewModel : PageViewModel
{
    private const string AllCategory = "all";
    private const string SavedCategory = "saved";
    private const int MaxPreviewFps = 24;

    private readonly AppController _app;
    private readonly EffectsService _effects;
    private readonly LookPreviewService _previews;
    private readonly IUpgradeService _upgrade;
    private readonly List<LookTile> _allTiles = [];
    private readonly DispatcherTimer _animation = new(DispatcherPriority.Background);
    private readonly DispatcherTimer _copiedTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DateTime _started = DateTime.UtcNow;
    private bool _loading;
    private bool _shown;
    private bool _rendering;
    private bool _renderPending;
    private int _thumbsVersion;

    public EffectsViewModel(AppController app, EffectsService effects, LookPreviewService previews, IUpgradeService upgrade)
        : base(Loc.Get("Nav_Effects"), SymbolRegular.Sparkle24)
    {
        _app = app;
        _effects = effects;
        _previews = previews;
        _upgrade = upgrade;
        Groups = [.. Enum.GetValues<LookGroup>().Select(g => new LookGroupViewModel(this, g))];
        FrameRates =
        [
            new(30, Loc.Get("Effects_Fps30")),
            new(60, Loc.Get("Effects_Fps60")),
            new(0, Loc.Get("Effects_FpsDisplay")),
        ];
        Pictures = [.. Controls.ColorPreview.Pictures.Select(p => new PictureChoice(p, Loc.Get($"Preview_{p}")))];
        Picture = Pictures.FirstOrDefault(p => p.Key == _app.Config.Ui.PreviewPicture) ?? Pictures[0];
        _animation.Tick += (_, _) => RenderPreview();
        _copiedTimer.Tick += (_, _) =>
        {
            _copiedTimer.Stop();
            LinkCopied = false;
        };
        _effects.Changed += (_, _) => RefreshStatus();
        _app.ConfigChanged += (_, _) =>
        {
            if (!_loading)
                Load();
        };
        BuildTiles();
        Load();
    }

    public IReadOnlyList<LookGroupViewModel> Groups { get; }

    public IReadOnlyList<FrameRateChoice> FrameRates { get; }

    public IReadOnlyList<PictureChoice> Pictures { get; }

    public ObservableCollection<LookCategoryChip> Categories { get; } = [];

    public ObservableCollection<LookTile> Tiles { get; } = [];

    /// <summary>The look applied on top of the photo; updated in place as settings change.</summary>
    public WriteableBitmap Preview { get; } = new(LookPreviewService.LargeWidth, LookPreviewService.LargeHeight, 96, 96, PixelFormats.Bgra32, null);

    [ObservableProperty] public partial ImageSource? Original { get; set; }
    [ObservableProperty] public partial double ComparePosition { get; set; } = 0.5;
    [ObservableProperty] public partial PictureChoice Picture { get; set; }

    [ObservableProperty] public partial bool Enabled { get; set; }
    [ObservableProperty] public partial LookCategoryChip? SelectedCategory { get; set; }
    [ObservableProperty] public partial LookTile? SelectedTile { get; set; }
    [ObservableProperty] public partial string CurrentName { get; set; } = "";
    [ObservableProperty] public partial string CurrentHint { get; set; } = "";
    [ObservableProperty] public partial bool IsModified { get; set; }
    [ObservableProperty] public partial bool HasLook { get; set; }
    [ObservableProperty] public partial bool CanReset { get; set; }

    [ObservableProperty] public partial bool ShowStatus { get; set; }
    [ObservableProperty] public partial string StatusTitle { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial InfoBarSeverity StatusSeverity { get; set; }
    [ObservableProperty] public partial bool ShowUpgrade { get; set; }
    [ObservableProperty] public partial bool PreviewFailed { get; set; }

    [ObservableProperty] public partial bool CanSaveLooks { get; set; }
    [ObservableProperty] public partial string NewLookName { get; set; } = "";
    [ObservableProperty] public partial bool LinkCopied { get; set; }

    [ObservableProperty] public partial bool HideOverFullScreen { get; set; }
    [ObservableProperty] public partial bool HideFromCapture { get; set; }
    [ObservableProperty] public partial bool MainDisplayOnly { get; set; }
    [ObservableProperty] public partial FrameRateChoice? FrameRate { get; set; }

    internal Look CurrentLook => _app.Config.Effects.Look;

    public override void OnShown()
    {
        _shown = true;
        Load();
        _ = PreparePreviewsAsync();
    }

    public override void OnHidden()
    {
        _shown = false;
        _animation.Stop();
    }

    /// <summary>The main window closed: let go of the previews' GPU and memory.</summary>
    public void ReleasePreviews()
    {
        OnHidden();
        foreach (LookTile tile in _allTiles)
            tile.Thumbnail = null;
        Original = null;
        _previews.Release();
    }

    /// <summary>A look from a share link: saved (Pro) or put in place of the current one.</summary>
    public void Import(SharedLook shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        EffectsConfig effects = _app.Config.Effects;
        if (_app.Features.SavedLooks)
        {
            var saved = new SavedLook { Name = shared.Name, Look = shared.Look, BasedOn = shared.BasedOn };
            effects.SavedLooks.Add(saved);
            (effects.Preset, effects.Look) = (saved.Key, saved.Look);
        }
        else
        {
            (effects.Preset, effects.Look) = (shared.BasedOn, shared.Look);
        }
        effects.Enabled = true;
        BuildTiles();
        Commit();
        _ = RenderThumbsAsync();
    }

    internal void SetParameter(LookParameter parameter, double value)
    {
        if (_loading)
            return;
        EffectsConfig effects = _app.Config.Effects;
        Look changed = parameter.Set(effects.Look, value);
        if (changed == effects.Look)
            return;
        effects.Look = changed;
        Commit();
        RefreshEditor();
        RefreshCurrent();
    }

    internal void SelectLook(LookTile tile)
    {
        EffectsConfig effects = _app.Config.Effects;
        (effects.Preset, effects.Look) = (tile.Id, tile.Look);
        effects.Enabled = true;
        Commit();
        Load();
    }

    internal void DeleteSaved(LookTile tile)
    {
        EffectsConfig effects = _app.Config.Effects;
        effects.SavedLooks.RemoveAll(s => s.Key == tile.Id);
        foreach (Core.Model.Profile profile in _app.Config.Profiles.Where(p => p.Effect == tile.Id))
            profile.Effect = null;
        if (effects.Preset == tile.Id)
            effects.Preset = null;
        BuildTiles();
        Commit();
        _app.SettingsChanged();
        Load();
    }

    partial void OnEnabledChanged(bool value)
    {
        if (_loading || value == _app.Config.Effects.Enabled)
            return;
        _effects.Toggle();
        Load();
    }

    partial void OnSelectedCategoryChanged(LookCategoryChip? value)
    {
        foreach (LookCategoryChip chip in Categories)
            chip.IsSelected = chip == value;
        FilterTiles();
    }

    partial void OnSelectedTileChanged(LookTile? value)
    {
        if (!_loading && value is not null && value.Id != _app.Config.Effects.Preset)
            SelectLook(value);
    }

    partial void OnPictureChanged(PictureChoice value)
    {
        if (_shown)
            _ = PreparePreviewsAsync(force: true);
    }

    partial void OnHideOverFullScreenChanged(bool value) => SaveOptions();

    partial void OnHideFromCaptureChanged(bool value) => SaveOptions();

    partial void OnMainDisplayOnlyChanged(bool value) => SaveOptions();

    partial void OnFrameRateChanged(FrameRateChoice? value) => SaveOptions();

    [RelayCommand]
    private void NextPicture() => Picture = Pictures[(Pictures.ToList().IndexOf(Picture) + 1) % Pictures.Count];

    [RelayCommand]
    private void PreviousPicture() => Picture = Pictures[(Pictures.ToList().IndexOf(Picture) + Pictures.Count - 1) % Pictures.Count];

    /// <summary>Back to the look as it was before any changes.</summary>
    [RelayCommand]
    private void Reset()
    {
        EffectsConfig effects = _app.Config.Effects;
        if (LookLibrary.Find(effects, effects.Preset) is not { } original)
            return;
        effects.Look = original;
        Commit();
        Load();
    }

    /// <summary>Every setting back to neutral, to build a look from scratch.</summary>
    [RelayCommand]
    private void Clear()
    {
        EffectsConfig effects = _app.Config.Effects;
        (effects.Preset, effects.Look) = (null, Look.Neutral);
        Commit();
        Load();
    }

    [RelayCommand]
    private void SaveLook()
    {
        if (!_app.Features.SavedLooks)
        {
            _upgrade.ShowUpgrade(Loc.Get("Upgrade_SavedLooks"));
            return;
        }
        EffectsConfig effects = _app.Config.Effects;
        string fallback = Loc.Format("Effects_MyLook", effects.SavedLooks.Count + 1);
        var saved = new SavedLook
        {
            Name = ConfigNormalizer.CleanName(NewLookName, fallback),
            Look = effects.Look,
            BasedOn = LookLibrary.Origin(effects, effects.Preset)?.Id,
        };
        effects.SavedLooks.Add(saved);
        effects.Preset = saved.Key;
        NewLookName = "";
        BuildTiles();
        Commit();
        SelectedCategory = Categories.FirstOrDefault(c => c.Key == SavedCategory);
        Load();
        _ = RenderThumbsAsync();
    }

    [RelayCommand]
    private void CopyLink()
    {
        EffectsConfig effects = _app.Config.Effects;
        string link = new SharedLook(_effects.NameOf(effects.Preset), effects.Look, LookLibrary.Origin(effects, effects.Preset)?.Id).ToLink();
        try
        {
            Clipboard.SetText(link);
            LinkCopied = true;
            _copiedTimer.Stop();
            _copiedTimer.Start();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // The clipboard is busy (another app has it open); the user can try again.
        }
    }

    [RelayCommand]
    private Task Upgrade() => _upgrade.PurchaseAsync();

    private void Commit()
    {
        _loading = true;
        try
        {
            _effects.Apply();
            _app.EffectsChanged();
        }
        finally
        {
            _loading = false;
        }
        RenderPreview();
        UpdateAnimation();
    }

    private void SaveOptions()
    {
        if (_loading)
            return;
        EffectsConfig effects = _app.Config.Effects;
        effects.HideOverFullScreen = HideOverFullScreen;
        effects.HideFromCapture = HideFromCapture;
        effects.MainDisplayOnly = MainDisplayOnly;
        effects.MaxFps = FrameRate?.Fps ?? 60;
        Commit();
    }

    private void Load()
    {
        _loading = true;
        try
        {
            EffectsConfig effects = _app.Config.Effects;
            Enabled = effects.Enabled;
            HideOverFullScreen = effects.HideOverFullScreen;
            HideFromCapture = effects.HideFromCapture;
            MainDisplayOnly = effects.MainDisplayOnly;
            FrameRate = FrameRates.FirstOrDefault(f => f.Fps == effects.MaxFps) ?? FrameRates[1];
            CanSaveLooks = _app.Features.SavedLooks;
            if (_allTiles.Count(t => t.IsSaved) != effects.SavedLooks.Count)
                BuildTiles();
            bool pro = _app.Features.IsPro;
            foreach (LookTile tile in _allTiles)
            {
                tile.IsSelected = tile.Id == effects.Preset;
                tile.IsLocked = !pro && !_app.Features.CanShowLook(LookLibrary.Origin(effects, tile.Id));
            }
            SelectedTile = _allTiles.FirstOrDefault(t => t.IsSelected);
            RefreshEditor();
            RefreshCurrent();
            RefreshStatus();
        }
        finally
        {
            _loading = false;
        }
        if (_shown)
        {
            RenderPreview();
            UpdateAnimation();
        }
    }

    private void RefreshEditor()
    {
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            foreach (LookGroupViewModel group in Groups)
                group.Refresh(CurrentLook);
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private void RefreshCurrent()
    {
        EffectsConfig effects = _app.Config.Effects;
        Look? original = LookLibrary.Find(effects, effects.Preset);
        HasLook = !effects.Look.IsEmpty;
        CurrentName = HasLook ? _effects.NameOf(effects.Preset) : Loc.Get("Effects_NoLook");
        CurrentHint = !HasLook ? Loc.Get("Effects_NoLookHint")
            : Looks.Find(effects.Preset) is { } preset ? Loc.Get($"Look_{preset.Id}Hint")
            : effects.SavedLooks.Any(l => l.Key == effects.Preset) ? Loc.Get("Effects_SavedHint")
            : Loc.Get("Effects_CustomHint");
        IsModified = original is not null && original != effects.Look;
        CanReset = IsModified;
    }

    private void RefreshStatus()
    {
        EffectsConfig effects = _app.Config.Effects;
        (ShowStatus, StatusTitle, StatusText, StatusSeverity, ShowUpgrade) = _effects.State switch
        {
            EffectsState.NeedsPro => (true, Loc.Get("Effects_ProTitle"), Loc.Format("Effects_ProText", _effects.NameOf(_effects.CurrentId)), InfoBarSeverity.Informational, true),
            EffectsState.LayersOnly => (true, Loc.Get("Effects_LayersOnlyTitle"), Loc.Get("Effects_LayersOnlyText"), InfoBarSeverity.Warning, false),
            EffectsState.Waiting => (true, Loc.Get("Effects_WaitingTitle"), Loc.Get("Effects_WaitingText"), InfoBarSeverity.Informational, false),
            EffectsState.Failed => (true, Loc.Get("Effects_FailedTitle"), Loc.Get("Effects_FailedText"), InfoBarSeverity.Error, false),
            _ when _effects.FromProfile && _app.Current?.ProfileId is { } id && _app.FindProfile(id) is { } profile =>
                (true, Loc.Get("Effects_FromProfileTitle"), Loc.Format("Effects_FromProfileText", profile.Name), InfoBarSeverity.Informational, false),
            _ => (false, "", "", InfoBarSeverity.Informational, false),
        };
        if (_loading)
            return;
        bool wasLoading = _loading;
        _loading = true;
        Enabled = effects.Enabled;
        _loading = wasLoading;
    }

    private void BuildTiles()
    {
        _allTiles.Clear();
        foreach (LookPreset preset in Looks.All)
            _allTiles.Add(new LookTile(this, preset.Id, Loc.Get($"Look_{preset.Id}"), Loc.Get($"Look_{preset.Id}Hint"), preset.Category.ToString(), preset.Look, isSaved: false));
        foreach (SavedLook saved in _app.Config.Effects.SavedLooks)
            _allTiles.Add(new LookTile(this, saved.Key, saved.Name, Loc.Get("Effects_SavedHint"), SavedCategory, saved.Look, isSaved: true));

        string? selected = SelectedCategory?.Key;
        Categories.Clear();
        Categories.Add(new LookCategoryChip(AllCategory, Loc.Get("LookCategory_All")));
        foreach (LookCategory category in Enum.GetValues<LookCategory>())
            Categories.Add(new LookCategoryChip(category.ToString(), Loc.Get($"LookCategory_{category}")));
        if (_app.Config.Effects.SavedLooks.Count > 0)
            Categories.Add(new LookCategoryChip(SavedCategory, Loc.Get("LookCategory_Saved")));
        SelectedCategory = Categories.FirstOrDefault(c => c.Key == selected) ?? Categories[0];
        FilterTiles();
    }

    private void FilterTiles()
    {
        string key = SelectedCategory?.Key ?? AllCategory;
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            Tiles.Clear();
            foreach (LookTile tile in _allTiles.Where(t => key == AllCategory || t.Category == key))
                Tiles.Add(tile);
            SelectedTile = Tiles.FirstOrDefault(t => t.IsSelected);
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private async Task PreparePreviewsAsync(bool force = false)
    {
        if (!await _previews.SetPictureAsync(Picture.Key).ConfigureAwait(true))
        {
            PreviewFailed = true;
            return;
        }
        PreviewFailed = false;
        Original = _previews.Original;
        RenderPreview();
        UpdateAnimation();
        if (force || _allTiles.Any(t => t.Thumbnail is null))
            await RenderThumbsAsync().ConfigureAwait(true);
    }

    private async Task RenderThumbsAsync()
    {
        int version = ++_thumbsVersion;
        // The shown ones first, so the gallery fills in where the user is looking.
        foreach (LookTile tile in Tiles.Concat(_allTiles.Except(Tiles)).ToList())
        {
            BitmapSource? thumb = await _previews.RenderThumbAsync(tile.Look).ConfigureAwait(true);
            if (version != _thumbsVersion)
                return;
            tile.Thumbnail = thumb;
        }
    }

    /// <summary>Renders the large preview; while one is being drawn, only the newest request is kept.</summary>
    private async void RenderPreview()
    {
        if (!_shown)
            return;
        if (_rendering)
        {
            _renderPending = true;
            return;
        }
        _rendering = true;
        try
        {
            do
            {
                _renderPending = false;
                double seconds = (DateTime.UtcNow - _started).TotalSeconds;
                await _previews.RenderLargeAsync(CurrentLook, seconds, Preview).ConfigureAwait(true);
            }
            while (_renderPending && _shown);
        }
        finally
        {
            _rendering = false;
        }
    }

    /// <summary>Animated looks (grain, flicker, tape noise) move in the preview too.</summary>
    private void UpdateAnimation()
    {
        Look look = CurrentLook;
        if (_shown && look.IsAnimated && !_app.Config.Ui.ReduceMotion)
        {
            _animation.Interval = TimeSpan.FromSeconds(1.0 / Math.Min(look.GrainFps, MaxPreviewFps));
            _animation.Start();
        }
        else
        {
            _animation.Stop();
        }
    }

    /// <summary>Formats a value the way its unit reads in the editor.</summary>
    internal static string Format(LookParameter parameter, double value) => parameter.Unit switch
    {
        LookUnit.Percent => string.Create(CultureInfo.CurrentCulture, $"{value * 100:0} %"),
        LookUnit.Balance => string.Create(CultureInfo.CurrentCulture, $"{value * 100:+0;-0;0}"),
        LookUnit.Stops => string.Create(CultureInfo.CurrentCulture, $"{value:+0.00;-0.00;0}"),
        LookUnit.Degrees => string.Create(CultureInfo.CurrentCulture, $"{value:0}°"),
        LookUnit.Pixels => string.Create(CultureInfo.CurrentCulture, $"{value:0.#} px"),
        LookUnit.Fps => value <= 0 ? Loc.Get("Effects_Still") : string.Create(CultureInfo.CurrentCulture, $"{value:0} fps"),
        LookUnit.Steps => value <= 1 ? Loc.Get("Effects_Off") : string.Create(CultureInfo.CurrentCulture, $"{value:0}"),
        _ => OptionName(parameter, value),
    };

    internal static string OptionName(LookParameter parameter, double value) => parameter.Key switch
    {
        "palette" => Loc.Get($"Palette_{(RetroPalette)(int)value}"),
        "colorVision" => Loc.Get($"ColorVision_{(ColorVision)(int)value}"),
        "letterbox" => value <= 0 ? Loc.Get("Effects_Off") : string.Create(CultureInfo.CurrentCulture, $"{value:0.00} : 1"),
        _ => value.ToString(CultureInfo.CurrentCulture),
    };
}

/// <summary>One section of the editor, e.g. Colour or Film.</summary>
public sealed partial class LookGroupViewModel : ObservableObject
{
    public LookGroupViewModel(EffectsViewModel owner, LookGroup group)
    {
        Group = group;
        Name = Loc.Get($"LookGroup_{group}");
        Icon = group switch
        {
            LookGroup.Colour => SymbolRegular.Color24,
            LookGroup.Light => SymbolRegular.WeatherSunny24,
            LookGroup.Lens => SymbolRegular.Camera24,
            LookGroup.Film => SymbolRegular.Filmstrip24,
            LookGroup.Retro => SymbolRegular.Games24,
            LookGroup.Comfort => SymbolRegular.WeatherMoon24,
            _ => SymbolRegular.Accessibility24,
        };
        Settings = [.. LookParameters.All.Where(p => p.Group == group).Select(p => new LookSettingViewModel(owner, p))];
    }

    public LookGroup Group { get; }

    public string Name { get; }

    public SymbolRegular Icon { get; }

    public IReadOnlyList<LookSettingViewModel> Settings { get; }

    [ObservableProperty] public partial string ChangedText { get; set; } = "";

    internal void Refresh(Look look)
    {
        foreach (LookSettingViewModel setting in Settings)
            setting.Refresh(look);
        int changed = Settings.Count(s => s.IsChanged);
        ChangedText = changed == 0 ? "" : Loc.Format("Effects_Changed", changed);
    }
}

/// <summary>One setting: a slider, or a list for choices such as the palette.</summary>
public sealed partial class LookSettingViewModel : ObservableObject
{
    private readonly EffectsViewModel _owner;
    private bool _refreshing;

    public LookSettingViewModel(EffectsViewModel owner, LookParameter parameter)
    {
        _owner = owner;
        Parameter = parameter;
        Name = Loc.Get($"LookParam_{parameter.Key}");
        Options = parameter.Options?.Select(v => new LookOption(v, EffectsViewModel.OptionName(parameter, v))).ToList() ?? [];
    }

    public LookParameter Parameter { get; }

    public string Name { get; }

    public bool IsChoice => Parameter.Unit == LookUnit.Choice;

    /// <summary>One step of the slider as a fraction of its length, for the arrow keys.</summary>
    public double Step => Range > 0 ? Parameter.Step / Range : 0.01;

    public IReadOnlyList<LookOption> Options { get; }

    /// <summary>
    /// Where the slider is, from 0 to 1. The slider never sees the setting's own range, so WPF can't clamp a value
    /// while the bindings are still arriving and write the clamped value back.
    /// </summary>
    [ObservableProperty] public partial double Position { get; set; }

    [ObservableProperty] public partial double Value { get; set; }
    [ObservableProperty] public partial LookOption? SelectedOption { get; set; }
    [ObservableProperty] public partial string DisplayValue { get; set; } = "";
    [ObservableProperty] public partial bool IsApplicable { get; set; } = true;
    [ObservableProperty] public partial bool IsChanged { get; set; }

    internal void Refresh(Look look)
    {
        _refreshing = true;
        try
        {
            double value = Parameter.Get(look);
            Value = value;
            Position = Range > 0 ? (value - Parameter.Min) / Range : 0;
            SelectedOption = Options.FirstOrDefault(o => Math.Abs(o.Value - value) < 1e-6);
            DisplayValue = EffectsViewModel.Format(Parameter, value);
            IsApplicable = Parameter.Applies(look);
            IsChanged = Math.Abs(value - Parameter.Default) > 1e-9;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private double Range => Parameter.Max - Parameter.Min;

    partial void OnPositionChanged(double value)
    {
        if (_refreshing || IsChoice || Range <= 0)
            return;
        double raw = Parameter.Min + (Math.Clamp(value, 0, 1) * Range);
        double snapped = Parameter.Step > 0 ? Math.Round(raw / Parameter.Step) * Parameter.Step : raw;
        Value = Math.Round(snapped, 6);
        DisplayValue = EffectsViewModel.Format(Parameter, Value);
        _owner.SetParameter(Parameter, Value);
    }

    partial void OnSelectedOptionChanged(LookOption? value)
    {
        if (!_refreshing && value is not null)
            _owner.SetParameter(Parameter, value.Value);
    }

    [RelayCommand]
    private void Reset() => _owner.SetParameter(Parameter, Parameter.Default);
}

public sealed record LookOption(double Value, string Name)
{
    public override string ToString() => Name;
}

public sealed partial class LookCategoryChip(string key, string name) : ObservableObject
{
    public string Key { get; } = key;

    public string Name { get; } = name;

    [ObservableProperty] public partial bool IsSelected { get; set; }

    public override string ToString() => Name;
}

/// <summary>A look in the gallery, with a small picture of it.</summary>
public sealed partial class LookTile(EffectsViewModel owner, string id, string name, string hint, string category, Look look, bool isSaved) : ObservableObject
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public string Hint { get; } = hint;

    public string Category { get; } = category;

    public Look Look { get; } = look;

    public bool IsSaved { get; } = isSaved;

    [ObservableProperty] public partial ImageSource? Thumbnail { get; set; }
    [ObservableProperty] public partial bool IsSelected { get; set; }

    /// <summary>Needs Pro to show on screen (it can still be previewed).</summary>
    [ObservableProperty] public partial bool IsLocked { get; set; }

    [RelayCommand]
    private void Delete() => owner.DeleteSaved(this);
}

public sealed record FrameRateChoice(int Fps, string Name)
{
    public override string ToString() => Name;
}

public sealed record PictureChoice(string Key, string Name)
{
    public override string ToString() => Name;
}
