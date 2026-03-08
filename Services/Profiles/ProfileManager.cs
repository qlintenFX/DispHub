using System.Text.Json;
using DisplayHub.Constants;
using DisplayHub.Models;
using DisplayHub.Services.Logging;

namespace DisplayHub.Services.Profiles;

public class ProfileManager
{
    private List<Profile> _profiles;
    private readonly string _profilesFilePath;

    public IReadOnlyList<Profile> Profiles => _profiles;

    public int IndexOf(Profile profile) => _profiles.IndexOf(profile);

    public ProfileManager()
    {
        _profiles = new List<Profile>();

        string appDataPath = AppConstants.AppDataPath;
        if (!Directory.Exists(appDataPath))
            Directory.CreateDirectory(appDataPath);

        _profilesFilePath = Path.Combine(appDataPath, AppConstants.ProfilesFileName);

        if (!File.Exists(_profilesFilePath))
            CreateDefaultProfiles();
        else
            LoadProfiles();
    }

    public void AddProfile(Profile profile)
    {
        _profiles.Add(profile);
        SaveProfiles();
    }

    public void UpdateProfile(int index, Profile profile)
    {
        if (index >= 0 && index < _profiles.Count)
        {
            _profiles[index] = profile;
            SaveProfiles();
        }
    }

    public void RemoveProfile(int index)
    {
        if (index >= 0 && index < _profiles.Count)
        {
            _profiles.RemoveAt(index);
            SaveProfiles();
        }
    }

    public void SaveProfiles()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(_profiles, options);
            File.WriteAllText(_profilesFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save profiles", ex);
        }
    }

    private void LoadProfiles()
    {
        try
        {
            if (File.Exists(_profilesFilePath))
            {
                string json = File.ReadAllText(_profilesFilePath);
                var loaded = JsonSerializer.Deserialize<List<Profile>>(json);
                if (loaded != null && loaded.Count > 0)
                    _profiles = loaded;
                else
                    CreateDefaultProfiles();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load profiles, creating defaults", ex);
            CreateDefaultProfiles();
        }
    }

    private void CreateDefaultProfiles()
    {
        _profiles = new List<Profile>
        {
            new Profile("Default", AppConstants.GammaDefault, AppConstants.ContrastDefault, AppConstants.VibranceDefault),
            new Profile("Night Mode", 0.8, 0.4, 40),
            new Profile("Vivid", 1.1, 0.6, 75)
        };
        SaveProfiles();
    }
}
