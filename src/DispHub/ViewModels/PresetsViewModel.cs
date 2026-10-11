using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Model;
using DispHub.Core.Sharing;
using DispHub.Localization;
using DispHub.Services;

namespace DispHub.ViewModels;

public sealed partial class PresetsViewModel(AppController app, GalleryService gallery, IUpgradeService upgrade)
    : PageViewModel(Loc.Get("Nav_Presets"), Wpf.Ui.Controls.SymbolRegular.Library24)
{
    private bool _loaded;

    public ObservableCollection<PresetItemViewModel> Presets { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ImportText { get; set; } = "";

    [ObservableProperty]
    public partial string? Message { get; set; }

    public static string ContributeUrl => GalleryService.ContributeUrl;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        _loaded = true;
        IsLoading = true;
        try
        {
            foreach ((GalleryEntry entry, SharedPreset preset) in await gallery.LoadAsync())
                Presets.Add(new PresetItemViewModel(entry, preset, this));
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Adds a preset as a new profile and shows it straight away. Returns false if the profile limit was hit.</summary>
    public bool Add(SharedPreset preset)
    {
        if (app.AddProfile(preset.Color, preset.Name) is not { } profile)
        {
            upgrade.ShowUpgrade(Loc.Get("Upgrade_MoreProfiles"));
            return false;
        }
        app.ActivateProfile(profile.Id, ChangeSource.Editor);
        Message = Loc.Format("Presets_Added", profile.Name);
        return true;
    }

    [RelayCommand]
    private void Import()
    {
        if (!SharedPreset.TryParse(ImportText, out SharedPreset? preset))
        {
            Message = Loc.Get("Presets_InvalidLink");
            return;
        }
        if (Add(preset))
            ImportText = "";
    }

    [RelayCommand]
    private static void Contribute() => Process.Start(new ProcessStartInfo(ContributeUrl) { UseShellExecute = true });
}

public sealed partial class PresetItemViewModel(GalleryEntry entry, SharedPreset preset, PresetsViewModel owner) : ObservableObject
{
    public string Name => entry.Name;
    public string Description => entry.Description;
    public string Tags => string.Join(" · ", entry.Tags);
    public string Summary => ProfileItemViewModel.Describe(preset.Color);
    public ImageSource Preview { get; } = Controls.ColorPreview.Render(preset.Color);

    [ObservableProperty]
    public partial bool Copied { get; set; }

    [RelayCommand]
    private void Add() => owner.Add(preset);

    [RelayCommand]
    private void CopyLink()
    {
        Clipboard.SetText(preset.ToLink());
        Copied = true;
    }
}
