using DispHub.Core.Color;
using DispHub.Core.Config;
using DispHub.Core.Model;

namespace DispHub.Tests.Core;

public sealed class ConfigStoreTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Load_NoFile_CreatesDefaultsAndSavesThem()
    {
        var store = new ConfigStore(_dir.Path);

        ConfigLoadResult result = store.Load();

        Assert.Equal(ConfigLoadStatus.Created, result.Status);
        Assert.Equal(3, result.Config.Profiles.Count);
        Assert.Equal(3, result.Config.Profiles.Select(p => p.Id).Distinct().Count());
        Assert.True(File.Exists(store.ConfigPath));
    }

    [Fact]
    public void SaveAndLoad_RoundTripsEverything()
    {
        var store = new ConfigStore(_dir.Path);
        AppConfig config = store.Load().Config;
        config.Profiles[1].Color = new ColorSettings(10, -5, 1.25, 4200, 65);
        config.Profiles[1].MonitorOverrides["\\\\?\\display#abc"] = new ColorSettings(Gamma: 1.4);
        config.AppRules.Add(new AppRule { Executable = "cs2.exe", ProfileId = config.Profiles[1].Id });
        config.Schedule.Slots.Add(new ScheduleSlot { Start = new TimeOnly(21, 30), ProfileId = config.Profiles[2].Id, FadeMinutes = 20 });
        config.Ui.Theme = ThemePreference.Dark;
        store.Save(config);

        AppConfig loaded = new ConfigStore(_dir.Path).Load().Config;

        Assert.Equal(ConfigStore.Serialize(config), ConfigStore.Serialize(loaded));
    }

    [Fact]
    public void Save_LeavesNoTempFileAndKeepsBackup()
    {
        var store = new ConfigStore(_dir.Path);
        AppConfig config = store.Load().Config;

        store.Save(config);

        Assert.False(File.Exists(store.ConfigPath + ".tmp"));
        Assert.True(File.Exists(store.BackupPath));
    }

    [Fact]
    public void Load_CorruptFile_RecoversFromBackupAndKeepsTheBrokenFile()
    {
        var store = new ConfigStore(_dir.Path);
        AppConfig config = store.Load().Config;
        config.Profiles[0].Name = "Saved before corruption";
        store.Save(config);
        store.Save(config);
        File.WriteAllText(store.ConfigPath, "{ this is not json");

        ConfigLoadResult result = new ConfigStore(_dir.Path).Load();

        Assert.Equal(ConfigLoadStatus.RecoveredFromBackup, result.Status);
        Assert.Equal("Saved before corruption", result.Config.Profiles[0].Name);
        Assert.Single(Directory.GetFiles(_dir.Path, "config.json.corrupt-*"));
    }

    [Fact]
    public void Load_CorruptFileAndBackup_ResetsToDefaultsAndKeepsBoth()
    {
        var store = new ConfigStore(_dir.Path);
        File.WriteAllText(store.ConfigPath, "garbage");
        File.WriteAllText(store.BackupPath, "more garbage");

        ConfigLoadResult result = store.Load();

        Assert.Equal(ConfigLoadStatus.ResetToDefaults, result.Status);
        Assert.Equal(2, Directory.GetFiles(_dir.Path, "*.corrupt-*").Length);
    }

    [Fact]
    public void Load_NewerVersion_IsReadOnlyAndNeverOverwritten()
    {
        var store = new ConfigStore(_dir.Path);
        AppConfig config = DefaultConfig.Create();
        config.Version = AppConfig.CurrentVersion + 1;
        File.WriteAllText(store.ConfigPath, ConfigStore.Serialize(config));
        string before = File.ReadAllText(store.ConfigPath);

        ConfigLoadResult result = store.Load();
        store.Save(result.Config);

        Assert.True(result.IsReadOnly);
        Assert.Equal(before, File.ReadAllText(store.ConfigPath));
    }

    [Fact]
    public void Journal_RoundTrips()
    {
        string path = Path.Combine(_dir.Path, "restore.json");
        var journal = new RestoreJournalData();
        journal.Monitors["m1"] = new RestoreEntry { DeviceName = "\\\\.\\DISPLAY1", Ramp = GammaRamp.Identity().ToBase64(), VibranceLevel = 12 };

        JsonFiles.WriteJournal(path, journal);
        RestoreJournalData? loaded = JsonFiles.ReadJournal(path);

        Assert.NotNull(loaded);
        Assert.Equal(12, loaded.Monitors["M1"].VibranceLevel);
    }
}

public sealed class ConfigNormalizerTests
{
    [Fact]
    public void Normalize_EmptyConfig_GetsDefaultProfilesAndActiveProfile()
    {
        var config = new AppConfig();

        ConfigNormalizer.Normalize(config);

        Assert.NotEmpty(config.Profiles);
        Assert.Equal(config.Profiles[0].Id, config.ActiveProfileId);
    }

    [Fact]
    public void Normalize_DuplicateIds_AreReplaced()
    {
        var id = Guid.NewGuid();
        var config = new AppConfig { Profiles = [new Profile { Id = id }, new Profile { Id = id }] };

        IReadOnlyList<string> fixes = ConfigNormalizer.Normalize(config);

        Assert.NotEqual(config.Profiles[0].Id, config.Profiles[1].Id);
        Assert.NotEmpty(fixes);
    }

    [Fact]
    public void Normalize_DanglingReferences_AreRemoved()
    {
        var profile = new Profile();
        var config = new AppConfig
        {
            Profiles = [profile],
            ActiveProfileId = Guid.NewGuid(),
            AppRules = [new AppRule { Executable = "game.exe", ProfileId = Guid.NewGuid() }],
            Schedule = new ScheduleConfig { Slots = [new ScheduleSlot { ProfileId = Guid.NewGuid() }] },
        };

        ConfigNormalizer.Normalize(config);

        Assert.Equal(profile.Id, config.ActiveProfileId);
        Assert.Empty(config.AppRules);
        Assert.Empty(config.Schedule.Slots);
    }

    [Fact]
    public void Normalize_BadValues_AreRepaired()
    {
        var config = new AppConfig
        {
            Profiles = [new Profile { Name = "   ", Color = new ColorSettings(Gamma: 9), Hotkey = "Q" }],
        };
        config.Hotkeys.TogglePause = "alt + ctrl + p";

        ConfigNormalizer.Normalize(config);

        Profile profile = config.Profiles[0];
        Assert.Equal("Profile", profile.Name);
        Assert.Equal(ColorSettings.GammaMax, profile.Color.Gamma);
        Assert.Null(profile.Hotkey);
        Assert.Equal("Ctrl+Alt+P", config.Hotkeys.TogglePause);
    }
}

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("disphub-tests-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
