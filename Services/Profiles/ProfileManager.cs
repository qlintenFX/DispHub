using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DisplayHub.Constants;
using DisplayHub.Models;
using DisplayHub.Services.Logging;

namespace DisplayHub.Services.Profiles
{
    public class ProfileManager
    {
        private List<Profile> profiles;
        private readonly string profilesFilePath;

        public IReadOnlyList<Profile> Profiles => profiles;

        public ProfileManager()
        {
            profiles = new List<Profile>();

            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppConstants.ProfilesFolderName);

            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            profilesFilePath = Path.Combine(appDataPath, AppConstants.ProfilesFileName);

            if (!File.Exists(profilesFilePath))
            {
                CreateDefaultProfiles();
            }
            else
            {
                LoadProfiles();
            }
        }

        public void AddProfile(Profile profile)
        {
            profiles.Add(profile);
            SaveProfiles();
        }

        public void UpdateProfile(int index, Profile profile)
        {
            if (index >= 0 && index < profiles.Count)
            {
                profiles[index] = profile;
                SaveProfiles();
            }
        }

        public void RemoveProfile(int index)
        {
            if (index >= 0 && index < profiles.Count)
            {
                profiles.RemoveAt(index);
                SaveProfiles();
            }
        }

        public void SaveProfiles()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(profiles, options);
                File.WriteAllText(profilesFilePath, jsonString);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save profiles", ex);
                System.Windows.MessageBox.Show($"Error saving profiles: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void LoadProfiles()
        {
            try
            {
                if (File.Exists(profilesFilePath))
                {
                    string jsonString = File.ReadAllText(profilesFilePath);
                    var loadedProfiles = JsonSerializer.Deserialize<List<Profile>>(jsonString);

                    if (loadedProfiles != null && loadedProfiles.Count > 0)
                    {
                        profiles = loadedProfiles;
                    }
                    else
                    {
                        CreateDefaultProfiles();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to load profiles, creating defaults", ex);
                System.Windows.MessageBox.Show($"Error loading profiles: {ex.Message}\r\nDefault profiles will be created.",
                    "Load Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                CreateDefaultProfiles();
            }
        }

        private void CreateDefaultProfiles()
        {
            profiles = new List<Profile>
            {
                new Profile("Default", AppConstants.GammaDefault, AppConstants.ContrastDefault, AppConstants.VibranceDefault),
                new Profile("Dark", 0.8, AppConstants.ContrastDefault, AppConstants.VibranceDefault),
                new Profile("Night Vision", AppConstants.GammaMax, 0.6, 65)
            };

            SaveProfiles();
        }
    }
}
