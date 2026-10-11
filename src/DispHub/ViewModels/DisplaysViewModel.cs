using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DispHub.Core.Model;
using DispHub.Localization;
using DispHub.Platform;
using DispHub.Platform.Displays;
using DispHub.Platform.Gamma;
using DispHub.Services;

namespace DispHub.ViewModels;

/// <summary>Every connected display: what DispHub can do on it, and whether to leave it alone.</summary>
public sealed partial class DisplaysViewModel : PageViewModel
{
    private readonly AppController _app;

    public DisplaysViewModel(AppController app)
        : base(Loc.Get("Nav_Displays"), Wpf.Ui.Controls.SymbolRegular.Desktop24)
    {
        _app = app;
        _app.MonitorsChanged += (_, _) => Load();
        Load();
    }

    public ObservableCollection<DisplayItem> Displays { get; } = [];

    [ObservableProperty] public partial bool ExtendedGammaRange { get; set; }
    [ObservableProperty] public partial bool AnyLimited { get; set; }

    public override void OnShown() => Load();

    [RelayCommand]
    private void EnableExtendedGammaRange()
    {
        if (GammaRange.TryEnableExtended())
            _app.OnDisplayChanged();
        ExtendedGammaRange = GammaRange.IsExtended;
    }

    private void Load()
    {
        Displays.Clear();
        foreach (MonitorStatus status in _app.Monitors)
            Displays.Add(new DisplayItem(_app, status));
        ExtendedGammaRange = GammaRange.IsExtended;
        AnyLimited = _app.Monitors.Any(m => m.State == MonitorColorState.Limited);
    }
}

public sealed partial class DisplayItem : ObservableObject
{
    private readonly AppController _app;
    private readonly string _key;

    public DisplayItem(AppController app, MonitorStatus status)
    {
        _app = app;
        _key = status.Monitor.Key;
        MonitorInfo monitor = status.Monitor;
        Name = monitor.Name;
        IsPrimary = monitor.IsPrimary;
        DisplayMode? mode = DisplayModes.Current(monitor.DeviceName);
        string kind = Loc.Get(monitor.IsInternal ? "Displays_BuiltIn" : "Displays_External");
        string resolution = mode is { } m ? string.Create(CultureInfo.CurrentCulture, $"{m.Width} × {m.Height} · {m.RefreshHz} Hz") : "";
        // A built-in panel without an EDID name is already called "Built-in display".
        Summary = string.Join(" · ", new[] { string.Equals(kind, Name, StringComparison.OrdinalIgnoreCase) ? "" : kind, resolution }.Where(s => s.Length > 0));
        Colours = Loc.Get(status.State switch
        {
            MonitorColorState.Applied or MonitorColorState.Original => "Displays_ColoursYes",
            MonitorColorState.Limited => "Displays_ColoursLimited",
            MonitorColorState.Hdr => "Displays_ColoursHdr",
            _ => "Displays_ColoursNo",
        });
        ColoursOk = status.State is MonitorColorState.Applied or MonitorColorState.Original;
        Vibrance = Loc.Get(status.SupportsVibrance ? "Displays_VibranceYes" : "Displays_VibranceNo");
        IsHdr = monitor.IsHdr;
        Details = string.Join(" · ", new[] { monitor.DeviceName.TrimStart('\\', '.'), monitor.EdidId }.Where(s => s.Length > 0));
        Excluded = app.Config.Monitors.TryGetValue(_key, out MonitorConfig? config) && config.Excluded;
    }

    public string Name { get; }
    public bool IsPrimary { get; }
    public string Summary { get; }
    public string Colours { get; }
    public bool ColoursOk { get; }
    public string Vibrance { get; }
    public bool IsHdr { get; }
    public string Details { get; }

    [ObservableProperty] public partial bool Excluded { get; set; }

    partial void OnExcludedChanged(bool value)
    {
        if (!_app.Config.Monitors.TryGetValue(_key, out MonitorConfig? config))
        {
            config = new MonitorConfig { Name = Name };
            _app.Config.Monitors[_key] = config;
        }
        if (config.Excluded == value)
            return;
        config.Excluded = value;
        _app.SettingsChanged();
    }
}
