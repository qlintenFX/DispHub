// SPDX-License-Identifier: GPL-3.0-or-later
using DispHub.Constants;
using DispHub.Models;
using DispHub.Services.Profiles;

namespace DispHub.Tests.Services.Profiles;

public class ProfileManagerTests : IDisposable
{
    private readonly string _testDirPath;
    private readonly string _testFilePath;

    public ProfileManagerTests()
    {
        _testDirPath = Path.Combine(Path.GetTempPath(), "DispHub_Test_Profiles_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirPath);
        _testFilePath = Path.Combine(_testDirPath, AppConstants.ProfilesFileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirPath))
        {
            Directory.Delete(_testDirPath, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_CreatesDefaultProfiles_WhenNoFileExists()
    {
        var manager = new ProfileManager(_testDirPath);

        Assert.True(File.Exists(_testFilePath));
        Assert.Equal(3, manager.Profiles.Count); // Default profiles
        Assert.Equal("Default", manager.Profiles[0].Name);
    }

    [Fact]
    public void AddProfile_IncreasesProfileCountAndSaves()
    {
        var manager = new ProfileManager(_testDirPath);
        var initialCount = manager.Profiles.Count;

        var newProfile = new Profile("TestProfile", 1.0, 0.5, 50);
        manager.AddProfile(newProfile);

        Assert.Equal(initialCount + 1, manager.Profiles.Count);
        Assert.Contains(newProfile, manager.Profiles);

        // Verify it was saved by creating a new manager instance reading the same directory
        var manager2 = new ProfileManager(_testDirPath);
        Assert.Equal(initialCount + 1, manager2.Profiles.Count);
        Assert.Equal("TestProfile", manager2.Profiles[initialCount].Name);
    }

    [Fact]
    public void UpdateProfile_ModifiesExistingProfileAndSaves()
    {
        var manager = new ProfileManager(_testDirPath);
        var updatedProfile = new Profile("UpdatedProfile", 1.2, 0.6, 60);

        manager.UpdateProfile(0, updatedProfile);

        Assert.Equal("UpdatedProfile", manager.Profiles[0].Name);
        Assert.Equal(1.2, manager.Profiles[0].Gamma);

        var manager2 = new ProfileManager(_testDirPath);
        Assert.Equal("UpdatedProfile", manager2.Profiles[0].Name);
    }

    [Fact]
    public void RemoveProfile_DecreasesProfileCountAndSaves()
    {
        var manager = new ProfileManager(_testDirPath);
        var initialCount = manager.Profiles.Count;

        manager.RemoveProfile(0);

        Assert.Equal(initialCount - 1, manager.Profiles.Count);

        var manager2 = new ProfileManager(_testDirPath);
        Assert.Equal(initialCount - 1, manager2.Profiles.Count);
    }
}
