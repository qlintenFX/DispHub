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
using DispHub.Services;
using Wpf.Ui.Controls;

namespace DispHub.ViewModels;

/// <summary>
/// Screen effects: a gallery of looks rendered on a photo, a before and after preview, and an editor that shows
/// only what the current look uses (strength, four basics, then each effect in it) with a menu to add more.
/// Everyone can try looks in the preview; showing them on screen is Pro.
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
    private readonly Dictionary<string, LookPartViewModel> _partCache = [];

    /// <summary>
    /// The effects shown in the editor. An effect stays, even turned down to nothing, until the user removes it
    /// or picks another look: it doesn't vanish under the slider.
    /// </summary>
    private readonly HashSet<string> _shownParts = [];
    private string? _shownPartsLook;
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
        Basics = [.. LookParts.Basics.Select(p => new LookSettingViewModel(this, p))];
        Library = [.. LookParts.All.GroupBy(p => p.Group).Select(g => new LibraryGroup(Loc.Get($"LookGroup_{g.Key}"), [.. g.Select(p => new LibraryEffect(this, p))]))];
        FrameRates =
        [
            new(0, Loc.Get("Effects_FpsDisplay")),
            new(60, Loc.Get("Effects_Fps60")),
            new(30, Loc.Get("Effects_Fps30")),
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
        _app.PowerChanged += (_, _) =>
        {
            SetSilently(() => Enabled = ShowsEnabled);
            RefreshStatus();
        };
        _app.ConfigChanged += (_, _) =>
        {
            if (!_loading)
                Load();
        };
        BuildTiles();
        Load();
    }

    public IReadOnlyList<LookSettingViewModel> Basics { get; }

    /// <summary>The effects in the current look, in editor order.</summary>
    public ObservableCollection<LookPartViewModel> Parts { get; } = [];

    /// <summary>Every effect, by group, to add to the look; the ones already in it are marked.</summary>
    public IReadOnlyList<LibraryGroup> Library { get; }

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
    [ObservableProperty] public partial bool IsPro { get; set; }
    [ObservableProperty] public partial LookCategoryChip? SelectedCategory { get; set; }
    [ObservableProperty] public partial LookTile? SelectedTile { get; set; }
    [ObservableProperty] public partial string CurrentName { get; set; } = "";
    [ObservableProperty] public partial string CurrentHint { get; set; } = "";
    [ObservableProperty] public partial bool IsModified { get; set; }
    [ObservableProperty] public partial bool HasLook { get; set; }
    [ObservableProperty] public partial double Strength { get; set; } = 1;
    [ObservableProperty] public partial string StrengthText { get; set; } = "100 %";

    [ObservableProperty] public partial bool ShowStatus { get; set; }
    [ObservableProperty] public partial string StatusTitle { get; set; } = "";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial InfoBarSeverity StatusSeverity { get; set; }
    [ObservableProperty] public partial bool PreviewFailed { get; set; }

    [ObservableProperty] public partial string NewLookName { get; set; } = "";
    [ObservableProperty] public partial bool LinkCopied { get; set; }

    /// <summary>What effects do over full-screen games and videos.</summary>
    public IReadOnlyList<FullScreenChoice> FullScreenChoices { get; } =
    [
        new(FullScreenEffects.Smooth, Loc.Get("Effects_GamesSmooth")),
        new(FullScreenEffects.Full, Loc.Get("Effects_GamesFull")),
        new(FullScreenEffects.Hide, Loc.Get("Effects_GamesHide")),
    ];

    [ObservableProperty] public partial FullScreenChoice? InFullScreen { get; set; }
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
        _app.Config.Ui.ShowEffects = true;
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
        effects.Strength = 1;
        effects.Enabled = _app.Features.ScreenEffects;
        BuildTiles();
        _app.SettingsChanged();
        Commit();
        Load();
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
        // The slider being dragged keeps its own position; pushing a rounded value back makes it stutter.
        RefreshEditor(except: parameter);
        RefreshCurrent();
    }

    internal void AddPart(LookPart part)
    {
        _shownParts.Add(part.Key);
        EffectsConfig effects = _app.Config.Effects;
        effects.Look = part.Add(effects.Look);
        Commit();
        RefreshEditor();
        RefreshCurrent();
        // Open, so its settings are right there to adjust.
        if (_partCache.TryGetValue(part.Key, out LookPartViewModel? added))
            added.IsExpanded = true;
    }

    /// <summary>An effect dropped on the look, by its key.</summary>
    internal void AddEffect(string key)
    {
        if (LookParts.Find(key) is { } part && !_shownParts.Contains(key))
            AddPart(part);
    }

    internal void RemovePart(LookPart part)
    {
        _shownParts.Remove(part.Key);
        EffectsConfig effects = _app.Config.Effects;
        effects.Look = part.Remove(effects.Look);
        Commit();
        RefreshEditor();
        RefreshCurrent();
    }

    internal void SelectLook(LookTile tile)
    {
        EffectsConfig effects = _app.Config.Effects;
        // Picking a look only selects it for the preview; the switch above decides whether the screen shows it.
        (effects.Preset, effects.Look, effects.Strength) = (tile.Id, tile.Look, 1);
        _shownParts.Clear();
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
        if (_loading)
            return;
        if (value && !_app.Features.ScreenEffects)
        {
            SetSilently(() => Enabled = false);
            _upgrade.ShowUpgrade(Loc.Get("Upgrade_Effects"));
            return;
        }
        _effects.SetEnabled(value, ChangeSource.Editor);
        Load();
    }

    partial void OnStrengthChanged(double value)
    {
        StrengthText = string.Create(CultureInfo.CurrentCulture, $"{value * 100:0} %");
        if (_loading)
            return;
        _app.Config.Effects.Strength = Math.Round(value, 2);
        Commit();
        RefreshCurrent();
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

    partial void OnInFullScreenChanged(FullScreenChoice? value) => SaveOptions();

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
        (effects.Look, effects.Strength) = (original, 1);
        _shownParts.Clear();
        Commit();
        Load();
    }

    /// <summary>Every setting back to neutral, to build a look from scratch.</summary>
    [RelayCommand]
    private void Clear()
    {
        EffectsConfig effects = _app.Config.Effects;
        (effects.Preset, effects.Look, effects.Strength) = (null, Look.Neutral, 1);
        _shownParts.Clear();
        Commit();
        Load();
    }

    [RelayCommand]
    private void ResetStrength() => Strength = 1;

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
            // The strength becomes part of the saved look.
            Look = effects.ShownLook,
            BasedOn = LookLibrary.Origin(effects, effects.Preset)?.Id,
        };
        effects.SavedLooks.Add(saved);
        (effects.Preset, effects.Look, effects.Strength) = (saved.Key, saved.Look, 1);
        NewLookName = "";
        BuildTiles();
        Commit();
        _app.SettingsChanged();
        SelectedCategory = Categories.FirstOrDefault(c => c.Key == SavedCategory);
        Load();
        _ = RenderThumbsAsync();
    }

    [RelayCommand]
    private void CopyLink()
    {
        EffectsConfig effects = _app.Config.Effects;
        string link = new SharedLook(_effects.NameOf(effects.Preset), effects.ShownLook, LookLibrary.Origin(effects, effects.Preset)?.Id).ToLink();
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
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            _effects.Apply();
            _app.EffectsChanged();
        }
        finally
        {
            _loading = wasLoading;
        }
        RenderPreview();
        UpdateAnimation();
    }

    /// <summary>The switch shows effects on only when they can be: Pro, turned on, and DispHub on.</summary>
    private bool ShowsEnabled => _app.Config.Effects.Enabled && _app.Features.ScreenEffects && !_app.Config.IsOff;

    private void SaveOptions()
    {
        if (_loading)
            return;
        EffectsConfig effects = _app.Config.Effects;
        effects.InFullScreen = InFullScreen?.Mode ?? FullScreenEffects.Smooth;
        effects.MainDisplayOnly = MainDisplayOnly;
        effects.MaxFps = FrameRate?.Fps ?? 0;
        Commit();
    }

    private void Load()
    {
        _loading = true;
        try
        {
            EffectsConfig effects = _app.Config.Effects;
            IsPro = _app.Features.ScreenEffects;
            Enabled = ShowsEnabled;
            Strength = effects.Strength;
            InFullScreen = FullScreenChoices.FirstOrDefault(c => c.Mode == effects.InFullScreen) ?? FullScreenChoices[0];
            MainDisplayOnly = effects.MainDisplayOnly;
            FrameRate = FrameRates.FirstOrDefault(f => f.Fps == effects.MaxFps) ?? FrameRates[0];
            if (_allTiles.Count(t => t.IsSaved) != effects.SavedLooks.Count)
                BuildTiles();
            foreach (LookTile tile in _allTiles)
                tile.IsSelected = tile.Id == effects.Preset;
            SelectedTile = Tiles.FirstOrDefault(t => t.IsSelected);
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

    /// <summary>
    /// Brings the editor in line with the look, changing the list of effects only where it differs, so open
    /// details and a slider being dragged stay as they are.
    /// </summary>
    private void RefreshEditor(LookParameter? except = null)
    {
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            Look look = CurrentLook;
            foreach (LookSettingViewModel setting in Basics)
                setting.Refresh(look, except);

            // Another look (picked here, from the quick panel or by a profile): show what it uses.
            if (_app.Config.Effects.Preset != _shownPartsLook)
            {
                _shownParts.Clear();
                _shownPartsLook = _app.Config.Effects.Preset;
            }
            List<LookPart> inLook = [.. LookParts.All.Where(p => p.IsIn(look) || _shownParts.Contains(p.Key))];
            _shownParts.UnionWith(inLook.Select(p => p.Key));
            for (int i = Parts.Count - 1; i >= 0; i--)
            {
                if (!inLook.Contains(Parts[i].Part))
                    Parts.RemoveAt(i);
            }
            for (int i = 0; i < inLook.Count; i++)
            {
                LookPart part = inLook[i];
                if (!_partCache.TryGetValue(part.Key, out LookPartViewModel? item))
                {
                    item = new LookPartViewModel(this, part);
                    _partCache[part.Key] = item;
                }
                int at = Parts.IndexOf(item);
                if (at < 0)
                    Parts.Insert(i, item);
                else if (at != i)
                    Parts.Move(at, i);
                item.Refresh(look, except);
            }

            foreach (LibraryEffect effect in Library.SelectMany(g => g.Effects))
                effect.IsAdded = _shownParts.Contains(effect.Key);
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
        IsModified = original is not null && (original != effects.Look || Math.Abs(effects.Strength - 1) > 0.005);
    }

    private void RefreshStatus()
    {
        (ShowStatus, StatusTitle, StatusText, StatusSeverity) = _effects.State switch
        {
            _ when !_app.Features.ScreenEffects => (true, Loc.Get("Effects_ProTitle"), Loc.Get("Effects_ProText"), InfoBarSeverity.Informational),
            _ when _app.Config.IsOff => (true, Loc.Get("Power_Off"), Loc.Get("Effects_OffText"), InfoBarSeverity.Informational),
            EffectsState.LayersOnly => (true, Loc.Get("Effects_LayersOnlyTitle"), Loc.Get("Effects_LayersOnlyText"), InfoBarSeverity.Warning),
            EffectsState.Waiting => (true, Loc.Get("Effects_WaitingTitle"), Loc.Get("Effects_WaitingText"), InfoBarSeverity.Informational),
            EffectsState.Failed => (true, Loc.Get("Effects_FailedTitle"), Loc.Get("Effects_FailedText"), InfoBarSeverity.Error),
            _ when _effects.FromProfile && _app.Current?.ProfileId is { } id && _app.FindProfile(id) is { } profile =>
                (true, Loc.Get("Effects_FromProfileTitle"), Loc.Format("Effects_FromProfileText", profile.Name), InfoBarSeverity.Informational),
            _ => (false, "", "", InfoBarSeverity.Informational),
        };
        if (!_loading)
            SetSilently(() => Enabled = _app.Config.Effects.Enabled && _app.Features.ScreenEffects);
    }

    private void SetSilently(Action change)
    {
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            change();
        }
        finally
        {
            _loading = wasLoading;
        }
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
        SetSilently(() =>
        {
            Tiles.Clear();
            foreach (LookTile tile in _allTiles.Where(t => key == AllCategory || t.Category == key))
                Tiles.Add(tile);
            SelectedTile = Tiles.FirstOrDefault(t => t.IsSelected);
        });
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
                await _previews.RenderLargeAsync(_app.Config.Effects.ShownLook, seconds, Preview).ConfigureAwait(true);
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

    internal static SymbolRegular GroupIcon(LookGroup group) => group switch
    {
        LookGroup.Colour => SymbolRegular.Color24,
        LookGroup.Light => SymbolRegular.WeatherSunny24,
        LookGroup.Lens => SymbolRegular.Camera24,
        LookGroup.Film => SymbolRegular.Filmstrip24,
        LookGroup.Retro => SymbolRegular.Games24,
        LookGroup.Comfort => SymbolRegular.WeatherMoon24,
        _ => SymbolRegular.Accessibility24,
    };
}

/// <summary>One effect in the current look: its main slider, and its details when opened.</summary>
public sealed partial class LookPartViewModel : ObservableObject
{
    private readonly EffectsViewModel _owner;

    public LookPartViewModel(EffectsViewModel owner, LookPart part)
    {
        _owner = owner;
        Part = part;
        Name = Loc.Get($"LookPart_{part.Key}");
        Icon = EffectsViewModel.GroupIcon(part.Group);
        Main = new LookSettingViewModel(owner, part.Main, canReset: false);
        Details = [.. part.Details.Select(d => new LookSettingViewModel(owner, d))];
    }

    public LookPart Part { get; }

    public string Name { get; }

    public SymbolRegular Icon { get; }

    public LookSettingViewModel Main { get; }

    public IReadOnlyList<LookSettingViewModel> Details { get; }

    public bool HasDetails => Details.Count > 0;

    [ObservableProperty] public partial bool IsExpanded { get; set; }

    internal void Refresh(Look look, LookParameter? except)
    {
        Main.Refresh(look, except);
        foreach (LookSettingViewModel detail in Details)
            detail.Refresh(look, except);
    }

    [RelayCommand]
    private void Remove() => _owner.RemovePart(Part);

    [RelayCommand]
    private void ToggleDetails() => IsExpanded = !IsExpanded;
}

public sealed record LibraryGroup(string Name, IReadOnlyList<LibraryEffect> Effects);

/// <summary>An effect in the box beside the look: clicked or dragged onto the look.</summary>
public sealed partial class LibraryEffect(EffectsViewModel owner, LookPart part) : ObservableObject
{
    public string Key { get; } = part.Key;

    public string Name { get; } = Loc.Get($"LookPart_{part.Key}");

    public string Hint { get; } = Loc.Get($"LookPart_{part.Key}Hint");

    public SymbolRegular Icon { get; } = EffectsViewModel.GroupIcon(part.Group);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    public partial bool IsAdded { get; set; }

    public bool CanAdd => !IsAdded;

    [RelayCommand]
    private void Add() => owner.AddPart(part);
}

/// <summary>A colour to pick for a tint, instead of a hue slider.</summary>
public sealed record HueChoice(double Degrees, string Name, SolidColorBrush Swatch)
{
    /// <summary>Ten clear colours around the colour wheel.</summary>
    public static IReadOnlyList<HueChoice> All { get; } =
    [
        .. new (double Degrees, string Key)[]
        {
            (0, "red"), (30, "orange"), (45, "amber"), (60, "yellow"), (120, "green"),
            (170, "teal"), (190, "cyan"), (215, "blue"), (270, "purple"), (320, "magenta"),
        }.Select(h => new HueChoice(h.Degrees, Loc.Get($"Hue_{h.Key}"), Brush(h.Degrees))),
    ];

    /// <summary>The choice closest to <paramref name="degrees"/> around the wheel.</summary>
    public static HueChoice Nearest(double degrees) =>
        All.MinBy(h => Math.Min(Math.Abs(h.Degrees - degrees), 360 - Math.Abs(h.Degrees - degrees)))!;

    private static SolidColorBrush Brush(double hue)
    {
        // A bright, slightly soft colour of this hue.
        double c = 0.78, x = c * (1 - Math.Abs((hue / 60 % 2) - 1)), m = 0.16;
        (double r, double g, double b) = (int)(hue / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        static byte B(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        var brush = new SolidColorBrush(Color.FromRgb(B(r + m), B(g + m), B(b + m)));
        brush.Freeze();
        return brush;
    }
}

/// <summary>One setting: a slider, or a list for choices such as the palette.</summary>
public sealed partial class LookSettingViewModel : ObservableObject
{
    private readonly EffectsViewModel _owner;
    private bool _refreshing;

    public LookSettingViewModel(EffectsViewModel owner, LookParameter parameter, string? name = null, bool canReset = true)
    {
        _owner = owner;
        Parameter = parameter;
        CanReset = canReset;
        Name = name ?? Loc.Get($"LookParam_{parameter.Key}");
        Options = parameter.Options?.Select(v => new LookOption(v, EffectsViewModel.OptionName(parameter, v))).ToList() ?? [];
    }

    public LookParameter Parameter { get; }

    public string Name { get; }

    public bool IsChoice => Parameter.Unit == LookUnit.Choice;

    /// <summary>False for an effect's main amount: removing the effect is how it goes back to neutral.</summary>
    public bool CanReset { get; }

    public bool ShowReset => CanReset && IsChanged;

    /// <summary>A tint's colour: picked from swatches rather than a slider.</summary>
    public bool IsHue => Parameter.Unit == LookUnit.Degrees;

    public bool IsSlider => !IsChoice && !IsHue;

    public IReadOnlyList<HueChoice> Hues => IsHue ? HueChoice.All : [];

    /// <summary>One step of the slider as a fraction of its length, for the arrow keys.</summary>
    public double Step => Range > 0 ? Math.Max(Parameter.Step / Range, 0.01) : 0.01;

    public IReadOnlyList<LookOption> Options { get; }

    /// <summary>
    /// Where the slider is, from 0 to 1. The slider never sees the setting's own range, so WPF can't clamp a value
    /// while the bindings are still arriving and write the clamped value back.
    /// </summary>
    [ObservableProperty] public partial double Position { get; set; }

    [ObservableProperty] public partial double Value { get; set; }
    [ObservableProperty] public partial LookOption? SelectedOption { get; set; }
    [ObservableProperty] public partial HueChoice? SelectedHue { get; set; }
    [ObservableProperty] public partial string DisplayValue { get; set; } = "";
    [ObservableProperty] public partial bool IsApplicable { get; set; } = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowReset))]
    public partial bool IsChanged { get; set; }

    private double Range => Parameter.Max - Parameter.Min;

    internal void Refresh(Look look, LookParameter? except = null)
    {
        double value = Parameter.Get(look);
        IsApplicable = Parameter.Applies(look);
        IsChanged = Math.Abs(value - Parameter.Default) > 1e-9;
        if (Parameter == except)
            return;
        _refreshing = true;
        try
        {
            Value = value;
            Position = Range > 0 ? (value - Parameter.Min) / Range : 0;
            SelectedOption = Options.FirstOrDefault(o => Math.Abs(o.Value - value) < 1e-6);
            SelectedHue = IsHue ? HueChoice.Nearest(value) : null;
            DisplayValue = EffectsViewModel.Format(Parameter, value);
        }
        finally
        {
            _refreshing = false;
        }
    }

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

    partial void OnSelectedHueChanged(HueChoice? value)
    {
        if (!_refreshing && value is not null)
            _owner.SetParameter(Parameter, value.Degrees);
    }

    [RelayCommand]
    private void Reset()
    {
        _owner.SetParameter(Parameter, Parameter.Default);
        // SetParameter skips refreshing the setting it came from; this one should move.
        _refreshing = true;
        Value = Parameter.Default;
        Position = Range > 0 ? (Parameter.Default - Parameter.Min) / Range : 0;
        SelectedHue = IsHue ? HueChoice.Nearest(Parameter.Default) : null;
        DisplayValue = EffectsViewModel.Format(Parameter, Parameter.Default);
        _refreshing = false;
    }
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

    [RelayCommand]
    private void Delete() => owner.DeleteSaved(this);
}

public sealed record FrameRateChoice(int Fps, string Name)
{
    public override string ToString() => Name;
}

public sealed record FullScreenChoice(FullScreenEffects Mode, string Name)
{
    public override string ToString() => Name;
}

public sealed record PictureChoice(string Key, string Name)
{
    public override string ToString() => Name;
}
