using DispHub.Constants;
using DispHub.Models;
using DispHub.Services.Profiles;

namespace DispHub.Tests;

public class ProfileManagerTests
{
    private static readonly Lock EnvironmentLock = new();

    [Fact]
    public void ProfileManager_CreatesDefaults_AndPersistsCrudOperations()
    {
        lock (EnvironmentLock)
        {
            using var scope = new AppDataScope();

            var manager = new ProfileManager();
            Assert.Equal(3, manager.Profiles.Count);

            manager.AddProfile(new Profile("Cinema", 0.9, 0.45, 40));
            Assert.Equal(4, manager.Profiles.Count);
            Assert.Equal("Cinema", manager.Profiles[^1].Name);

            manager.UpdateProfile(0, new Profile("Updated Default", 1.15, 0.55, 65));
            Assert.Equal("Updated Default", manager.Profiles[0].Name);

            manager.RemoveProfile(manager.Profiles.Count - 1);
            Assert.Equal(3, manager.Profiles.Count);

            var managerReloaded = new ProfileManager();
            Assert.Equal(3, managerReloaded.Profiles.Count);
            Assert.Equal("Updated Default", managerReloaded.Profiles[0].Name);

            string profilesPath = Path.Combine(AppConstants.AppDataPath, AppConstants.ProfilesFileName);
            Assert.True(File.Exists(profilesPath));
        }
    }

    private sealed class AppDataScope : IDisposable
    {
        private readonly string? _originalAppData;
        private readonly string _tempAppData;

        public AppDataScope()
        {
            _originalAppData = Environment.GetEnvironmentVariable("APPDATA", EnvironmentVariableTarget.Process);
            _tempAppData = Path.Combine(Path.GetTempPath(), "DispHubTests_AppData_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempAppData);
            Environment.SetEnvironmentVariable("APPDATA", _tempAppData, EnvironmentVariableTarget.Process);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("APPDATA", _originalAppData, EnvironmentVariableTarget.Process);
            try
            {
                if (Directory.Exists(_tempAppData))
                {
                    Directory.Delete(_tempAppData, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

